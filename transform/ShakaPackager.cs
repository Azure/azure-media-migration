using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;

namespace AMSMigrate.Transform
{
    internal class ShakaPackager : BasePackager
    {
        private readonly TaskCompletionSource<bool> _taskCompletionSource;

        public static readonly string Packager;

        static ShakaPackager()
        {
            var executable = $"packager-{(OperatingSystem.IsLinux() ? "linux-x64" : OperatingSystem.IsMacOS() ? "osx-x64" : "win-x64.exe")}";
            Packager = Path.Combine(
                   Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                   executable);
        }

        public ShakaPackager(AssetDetails assetDetails, TransMuxer transMuxer, ILogger<ShakaPackager> logger)
            : base(assetDetails, transMuxer, logger)
        {
            _taskCompletionSource = new TaskCompletionSource<bool>();
            var manifest = assetDetails.Manifest!;
            var baseName = Path.GetFileNameWithoutExtension(manifest.FileName);
            var manifests = SelectedTracks
                .Select((t, i) => $"{baseName}_{i}{HLS_MANIFEST}")
                .ToList();

            var outputManifest = assetDetails.OutputManifest ?? baseName;
            manifests.Add($"{outputManifest}{HLS_MANIFEST}");
            manifests.Add($"{outputManifest}{DASH_MANIFEST}");
            Manifests = manifests;

            // Shaka packager cannot handle smooth input.
            if (manifest.Format == "fmp4")
            {
                _logger.LogDebug("Transmuxing FMP4 asset with multiple tracks in a single file into regular MP4 file.");
                TransmuxedDownload = true;
            }
            else if (assetDetails.ClientManifest != null && assetDetails.ClientManifest.HasDiscontinuities(_logger))
            {
                // mux to a single file.
                Inputs.Clear();
                Inputs.Add($"{baseName}.mp4");
            }
            else if (!TransmuxedDownload)
            {
                UsePipeForInput = false;
            }

            //TODO: Shaka packager write to Windows named pipe fails due to path issue.
            UsePipeForOutput = false;
        }

        private IEnumerable<string> GetArguments(IList<string> inputs, IList<string> outputs, IList<string> manifests)
        {
            List<string> arguments = new(SelectedTracks.Select((t, i) =>
            {
                var ext = t.IsMultiFile ? MEDIA_FILE : string.Empty;
                var index = Inputs.Count == 1 ? 0 : Inputs.IndexOf($"{t.Source}{ext}");
                var stream = Inputs.Count == 1? i.ToString(): t.Type.ToString().ToLowerInvariant();
                var language = string.IsNullOrEmpty(t.SystemLanguage) || t.SystemLanguage == "und" ? string.Empty : $"language={t.SystemLanguage},";
                return $"stream={stream},in={inputs[index]},out={outputs[i]},{language}playlist_name={manifests[i]}";
            }));
            var dash = manifests[manifests.Count - 1];
            var hls = manifests[manifests.Count - 2];
            var logging = false;
            if (logging)
            {
                arguments.Add("--vmodule=*=1");
            }
            if (UsePipeForInput)
            {
                arguments.Add("--io_block_size");
                arguments.Add("65536");
            }
            arguments.Add("--segment_duration");
            arguments.Add("2");
            arguments.Add("--mpd_output");
            arguments.Add(dash);
            arguments.Add("--hls_master_playlist_output");
            arguments.Add(hls);
            return arguments;
        }

        public override Task<bool> RunAsync(
            string workingDirectory,
            string[] inputs,
            string[] outputs,
            string[] manifests,
            CancellationToken cancellationToken)
        {
            var arguments = GetArguments(inputs, outputs, manifests);
            var process = StartProcess(Packager, arguments, 
                process =>
                {
                    if (process.ExitCode == 0)
                    {
                        _taskCompletionSource.SetResult(true);
                    }
                    else
                    {
                        _taskCompletionSource.SetException(new Win32Exception(process.ExitCode, $"{Packager} failed"));
                    }
                },
                s => { },
                LogStandardError);
            return _taskCompletionSource.Task;
        }

        const string ShakaLogPattern = @"\d+/\d+:(?<level>\w+):";
        static readonly Regex ShakaLogRegEx = new Regex(ShakaLogPattern, RegexOptions.Compiled);
        static readonly IDictionary<string, LogLevel> LogLevels = new Dictionary<string, LogLevel>
        {
            { "FATAL", LogLevel.Critical },
            { "ERROR", LogLevel.Error },
            { "WARN", LogLevel.Warning },
            { "INFO", LogLevel.Information },
            { "VERBOSE1", LogLevel.Trace },
            { "VERBOSE2", LogLevel.Trace },
        };

        private void LogStandardError(string? line )
        {
            if (line != null)
            {
                var logLevel = LogLevel.Information;
                var match = ShakaLogRegEx.Match(line);
                var group = match.Groups["level"];
                _ = match.Success && group.Success && LogLevels.TryGetValue(group.Value, out logLevel);
                _logger.Log(logLevel, line);
            }
        }
    }
}
