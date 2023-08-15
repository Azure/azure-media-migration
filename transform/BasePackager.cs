using AMSMigrate.Contracts;
using AMSMigrate.Fmp4;
using AMSMigrate.Pipes;
using Azure.Storage.Blobs.Specialized;
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
        private readonly SortedDictionary<string, IList<Track>> _fileToTrackMap = new();

        public bool UsePipeForInput { get; protected set; } = false;

        public bool UsePipeForOutput { get; protected set; } = false;

        public bool UsePipeForManifests { get; protected set; } = false;

        public IList<string> Inputs { get; protected set; }

        public IList<string> Outputs { get; protected set; } = new List<string>();

        public IList<string> Manifests { get; protected set; } = new List<string>();

        public List<Track> SelectedTracks { get; }

        public bool TransmuxedSmooth { get; protected set; }

        public bool TranscodeAudio { get; protected set; }

        public IDictionary<string, IList<Track>> FileToTrackMap => _fileToTrackMap;

        public BasePackager(AssetDetails assetDetails, TransMuxer transMuxer, ILogger logger)
        {
            _assetDetails = assetDetails;
            _transMuxer = transMuxer;
            _logger = logger;

            var manifest = assetDetails.Manifest!;
            // For text tracks pick VTT since it is supported by both shaka and ffmpeg.
            SelectedTracks = manifest.Tracks.Where(t =>
            {
                if (t is TextTrack)
                {
                    bool pickThisTextTrack = !t.IsMultiFile && (t.Source.EndsWith(VTT_FILE) || t.Parameters.Any(t => t.Name == TRANSCRIPT_SOURCE));

                    if (manifest.IsLiveArchive)
                    {
                        pickThisTextTrack = false;

                        if (t.IsMultiFile)
                        {
                            // Choose the text track with a list of fragblobs for close captions.
                            pickThisTextTrack = assetDetails.ClientManifest!.Streams.Any(
                                                              stream => (stream.Type == StreamType.Text &&
                                                                         stream.SubType == "SUBT") &&
                                                                         stream.Name == t.TrackName);
                        }
                    }
                    return pickThisTextTrack;
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
                    if (manifest.IsLiveArchive)
                    {
                        input = $"{track.Source}{VTT_FILE}";
                    }
                    else
                    {
                        input = track.Source.EndsWith(VTT_FILE) ? track.Source : track.Parameters.Single(p => p.Name == TRANSCRIPT_SOURCE).Value;
                    }
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
                    _fileToTrackMap.Add(input, new List<Track> { track });
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

        private static string Escape(string argument)
        {
            if (argument.Contains(' '))
            {
                return $"\"{argument}\"";
            }
            return argument;
        }

        public Process StartProcess(string command, IEnumerable<string> arguments, Action<Process> onExit, Action<string?> stdOut, Action<string?> stdError)
        {
            _logger.LogDebug("Starting packager {command}...", command);
            var argumentString = string.Join(" ", arguments.Select(Escape));
            _logger.LogTrace("Packager arguments: {args}", argumentString);
            var processStartInfo = new ProcessStartInfo(command, argumentString)
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
                    var multiFileStream = new MultiFileStream(_assetDetails.Container, track, _assetDetails.ClientManifest!, _assetDetails.DecryptInfo, _logger);
                    return new MultiFilePipe(file, multiFileStream);
                }
                else
                {
                    return new BlobPipe(Path.Combine(workingDirectory, file), _assetDetails.Container, _assetDetails.DecryptInfo, _logger) as Pipe;
                }
            }).ToArray();
        }

        public async Task DownloadInputsAsync(string workingDirectory, CancellationToken cancellationToken)
        {
            await Task.WhenAll(FileToTrackMap.Select(async item =>
            {
                var (file, tracks) = item;
                await DownloadAsync(workingDirectory, file, tracks, cancellationToken);
            }));
        }


        private async Task DownloadAsync(string workingDirectory, string file, IList<Track> tracks, CancellationToken cancellationToken)
        {
            var tempDirectory = Path.Combine(workingDirectory, "input");
            Directory.CreateDirectory(tempDirectory);

            if (tracks.Count == 1 && tracks[0].IsMultiFile)
            {
                var filePath = Path.Combine(workingDirectory, file);
                var track = tracks[0];
                // TranscodeAudio = true;
                if (TranscodeAudio && track.Type == StreamType.Audio)
                {
                    filePath = Path.Combine(tempDirectory, file);
                }
                var multiFileStream = new MultiFileStream(_assetDetails.Container, track, _assetDetails.ClientManifest!, _assetDetails.DecryptInfo, _logger);
                var source = new MultiFilePipe(file, multiFileStream);
                await source.DownloadAsync(filePath, cancellationToken);
                if (TranscodeAudio && track.Type == StreamType.Audio)
                {
                    await Task.Run(() => _transMuxer.TranscodeAudioAsync(filePath, Path.Combine(workingDirectory, Path.GetFileName(filePath)), cancellationToken));
                }
            }
            else
            {
                var filePath = Path.Combine(TransmuxedSmooth ? tempDirectory : workingDirectory, file);
                var blob = _assetDetails.Container.GetBlockBlobClient(file);
                var source = new BlobSource(blob, _assetDetails.DecryptInfo, _logger);
                await source.DownloadAsync(filePath, cancellationToken);
                if (TransmuxedSmooth)
                {
                    await Task.WhenAll(tracks.Select(async track =>
                    {
                        using var sourceFile = File.OpenRead(filePath);
                        var filename = tracks.Count == 1 ? file : $"{Path.GetFileNameWithoutExtension(file)}_{track.TrackID}{Path.GetExtension(file)}";
                        using var destFile = File.OpenWrite(Path.Combine(workingDirectory, filename));
                        await Task.Run(() => _transMuxer.TransmuxSmooth(sourceFile, destFile, track.TrackID));
                    }));
                }
            }
        }
    }
}
