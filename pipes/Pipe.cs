using FFMpegCore.Pipes;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace AMSMigrate.Pipes
{
    public interface IPipe: IPipeSource, IPipeSink
    {
        string PipePath { get; }

        Task RunAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// A class to abstract platform specific pipe.
    /// </summary>
    abstract class Pipe : IDisposable, IPipe
    {
        protected readonly NamedPipeServerStream? _server;
        protected readonly PipeDirection _direction;

        public string PipePath { get; }

        public string FilePath { get; }

        public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public Pipe(string filePath, PipeDirection direction = PipeDirection.In)
        {
            FilePath = filePath;
            _direction = direction;

            if (IsWindows)
            {
                var pipeName = $"pipe_{Guid.NewGuid().ToString("N").Substring(0, 8)}{Path.GetExtension(filePath)}";
                _server = new NamedPipeServerStream(pipeName, direction, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                PipePath = $@"\\.\pipe\{pipeName}";
            }
            else
            {
                PipePath = filePath;
                CreatePipe(filePath);
            }
        }

        private void CreatePipe(string filePath)
        {
            var startInfo = new ProcessStartInfo("mkfifo", PipePath)
            {
                RedirectStandardError = false,
                RedirectStandardOutput = false,
            };
            using var process = new Process
            {
                StartInfo = startInfo,
            };
            process.Start();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"failed to create pipe: {filePath}");
            }
        }

        public async Task RunAsync(Func<Stream, CancellationToken, Task> action, CancellationToken cancellationToken)
        {
            if (_server == null)
            {
                var access = _direction == PipeDirection.Out ? FileAccess.Write : FileAccess.Read;
                var share = _direction == PipeDirection.In ? FileShare.Read : FileShare.Write;
                var copy = async () =>
                {
                    using var file = File.Open(PipePath, FileMode.Open, access, share);
                    await action(file, cancellationToken);
                };

                // File.Open a linux named pipe blocks. So do it in a task.
                await Task.Run(copy, cancellationToken);
            }
            else
            {
                await _server.WaitForConnectionAsync(cancellationToken);
                if (!_server.IsConnected)
                {
                    throw new OperationCanceledException();
                }
                await action(_server, cancellationToken);
            }
        }

        public void Dispose()
        {
            if (_server != null)
            {
                _server?.Dispose();
            }
            else
            {
                File.Delete(PipePath);
            }
        }

        public abstract Task RunAsync(CancellationToken cancellationToken);

        public virtual string GetStreamArguments()
        {
            return "-seekable 0";
        }

        public virtual async Task WriteAsync(Stream outputStream, CancellationToken cancellationToken)
        {
            await RunAsync(async (stream, cancellationToken) =>
            {
                await stream.CopyToAsync(outputStream, cancellationToken);
            }, cancellationToken);
        }

        public async Task ReadAsync(Stream inputStream, CancellationToken cancellationToken)
        {
            await RunAsync(async (stream, cancellationToken) =>
            {
                await inputStream.CopyToAsync(stream, cancellationToken);
            }, cancellationToken);
        }

        public string GetFormat()
        {
            return string.Empty;
        }
    }
}
