using AMSMigrate.Contracts;
using AMSMigrate.Pipes;
using Azure.Storage.Blobs.Specialized;
using FFMpegCore.Pipes;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AMSMigrate.Transform
{
    abstract class BasePackager : IPackager
    {
        public const string MEDIA_FILE = ".mp4";
        public const string DASH_MANIFEST = ".mpd";
        public const string HLS_MANIFEST = ".m3u8";
        public const string VTT_FILE = ".vtt";
        public const string TRANSCRIPT_SOURCE = "transcriptsrc";

        protected readonly TransMuxer _transMuxer;
        protected readonly ILogger _logger;
        protected readonly AssetDetails _assetDetails;
        private readonly Dictionary<string, IList<Track>> _fileToTrackMap = new Dictionary<string, IList<Track>>();

        public bool UsePipeForInput { get; protected set; } = false;

        public bool UsePipeForOutput { get; protected set; } = false;

        public bool UsePipeForManifests { get; protected set; } = false;

        public IList<string> Inputs {get; protected set; }

        public IList<string> Outputs { get; protected set; } = new List<string>();

        public IList<string> Manifests { get; protected set; } = new List<string>();

        public List<Track> SelectedTracks { get; }

        public bool TransmuxedDownload { get; protected set; }

        public IDictionary<string, IList<Track>> FileToTrackMap => _fileToTrackMap;

        public BasePackager(AssetDetails assetDetails, TransMuxer transMuxer, ILogger logger)
        {
            _assetDetails = assetDetails;
            _transMuxer = transMuxer;
            _logger = logger;

            if (assetDetails.ClientManifest != null && assetDetails.ClientManifest.HasDiscontinuities())
            {
                TransmuxedDownload = true;
            }

            var manifest = assetDetails.Manifest!;
            // For text tracks pick VTT since it is supported by both shaka and ffmpeg.
            SelectedTracks = manifest.Tracks.Where(t  => {
                if (t is TextTrack)
                {
                    return !TransmuxedDownload && !t.IsMultiFile && (t.Source.EndsWith(VTT_FILE) || t.Parameters.Any(t => t.Name == TRANSCRIPT_SOURCE));
                }
                return true;
            }).ToList();
            
            var inputs = new List<string>();
            foreach (var track in SelectedTracks)
            {
                var extension = track.IsMultiFile ? MEDIA_FILE : string.Empty;
                string input;
                if (track is TextTrack)
                {
                    input = track.Source.EndsWith(VTT_FILE) ? track.Source : track.Parameters.Single(p => p.Name == TRANSCRIPT_SOURCE).Value;
                }
                else
                {
                    input = $"{track.Source}{extension}";
                }
                if (_fileToTrackMap.TryGetValue(input, out var list))
                {
                    list.Add(track);
                }
                else
                {
                    _fileToTrackMap.Add(input, new List<Track>{ track });
                }
            }

            Inputs = _fileToTrackMap.Keys.ToList();
            // outputs
            var baseName = Path.GetFileNameWithoutExtension(manifest.FileName);
            Outputs = SelectedTracks.Select((t, i) =>
            {
                var ext = t is TextTrack ? VTT_FILE : MEDIA_FILE;
                // TODO: if you want to keep original file names.
                // var baseName = Path.GetFileNameWithoutExtension(t.Source);
                return $"{baseName}_{i}{ext}";
            }).ToList();
        }

        public abstract Task<bool> RunAsync(
            string workingDirectory,
            string[] inputs,
            string[] outputs,
            string[] manifests,
            CancellationToken cancellationToken);

        public Process StartProcess(string command, string arguments, Action<Process> onExit, Action<string?> stdOut, Action<string?> stdError)
        {
            _logger.LogDebug("Starting packager {command}...", command);
            _logger.LogTrace("Packager arguments: {args}", arguments);
            var processStartInfo = new ProcessStartInfo(command, arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true
            };

            var process = new Process
            {
                StartInfo = processStartInfo,
                EnableRaisingEvents = true
            };
            process.OutputDataReceived += (s, args) => stdOut(args.Data);
            process.ErrorDataReceived += (s, args) => stdError(args.Data);
            process.Exited += (s, args) =>
            {
                if (process.ExitCode != 0)
                    _logger.LogError("Packager {} finished with exit code {code}", command, process.ExitCode);
                onExit(process);
                process.Dispose();
            };
            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                return process;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to start process {command} with error: {ex}", command, ex);
                throw;
            }
        }

        public Pipe[] GetInputPipes(string workingDirectory)
        {
            return FileToTrackMap.Select(item =>
            {
                var (file, tracks) = item;
                if (tracks.Count == 1 && tracks[0].IsMultiFile)
                {
                    var track = tracks[0];
                    var multiFileStream = new MultiFileStream(_assetDetails.Container, track, _assetDetails.ClientManifest!, _logger);
                    return new MultiFilePipe(file, multiFileStream);
                }
                else
                {
                    return new BlobPipe(Path.Combine(workingDirectory, file), _assetDetails.Container, _logger) as Pipe;
                }
            }).ToArray();
        }

        public async Task DownloadInputsAsync(string workingDirectory, CancellationToken cancellationToken)
        {
            if (_assetDetails.ClientManifest != null &&
                _assetDetails.ClientManifest.HasDiscontinuities() &&
                _assetDetails is AssetRecord assetRecord)
            {
                await StreamingTransMuxAsync(workingDirectory, assetRecord, cancellationToken);
            }
            else
            {
                await Task.WhenAll(FileToTrackMap.Select(async item =>
                {
                    var (file, tracks) = item;
                    var filePath = Path.Combine(workingDirectory, file);
                    await DownloadAsync(filePath, tracks, cancellationToken);
                }));
            }
        }


        public async Task StreamingTransMuxAsync(string workingDirectory, AssetRecord assetRecord, CancellationToken cancellationToken) 
        {
            Debug.Assert(Inputs.Count == 1);
            var uri = await _transMuxer.GetStreamingUrlAsync(assetRecord, cancellationToken);
            if (uri == null)
            {
                _logger.LogWarning("No streaming locator found for asset {name}", assetRecord.AssetName);
                throw new NotImplementedException("Failed to get locator");
            }
            var filePath = Path.Combine(workingDirectory, Inputs[0]);
            await _transMuxer.TransmuxUriAsync(uri, filePath, cancellationToken);
        }
        
        private async Task DownloadAsync(string filePath, IList<Track> tracks, CancellationToken cancellationToken)
        {
            IPipeSource source;
            if (tracks.Count == 1 && tracks[0].IsMultiFile)
            {
                var track = tracks[0];
                var multiFileStream = new MultiFileStream(_assetDetails.Container, track, _assetDetails.ClientManifest!, _logger);
                source = new MultiFilePipe(filePath, multiFileStream);
            }
            else
            {
                var blob = _assetDetails.Container.GetBlockBlobClient(Path.GetFileName(filePath));
                source = new BlobSource(blob, _logger);
            }

            if (TransmuxedDownload)
            {
                await _transMuxer.TransMuxAsync(source, filePath, cancellationToken);
            }
            else if (source is MultiFilePipe pipe)
            {
                await pipe.DownloadAsync(filePath, cancellationToken);
            }
            else if (source is BlobSource blobSource)
            {
                await blobSource.DownloadAsync(filePath, cancellationToken);
            }
        }
    }
}
