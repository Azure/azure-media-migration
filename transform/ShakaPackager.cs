using AMSMigrate.Contracts;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace AMSMigrate.Transform
{
    internal class ShakaPackager : BasePackager
    {
        private readonly TaskCompletionSource<bool> _taskCompletionSource;

        static readonly string PackagerDirectory = AppContext.BaseDirectory;
        static readonly string Executable = $"packager-{(OperatingSystem.IsLinux() ? "linux-x64" : OperatingSystem.IsMacOS() ? "osx-x64" : "win-x64.exe")}";
        public static readonly string Packager = Path.Combine(PackagerDirectory, Executable);

        private readonly MigratorOptions _options;

        public ShakaPackager(MigratorOptions options, AssetDetails assetDetails, TransMuxer transMuxer, ILogger<ShakaPackager> logger)
            : base(assetDetails, transMuxer, logger)
        {
            _options = options;
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
                TransmuxedSmooth = true;
            }
            else if (assetDetails.ClientManifest != null && assetDetails.ClientManifest.HasDiscontinuities(_logger))
            {
                // mux to a single file.
                Inputs.Clear();
                Inputs.Add($"{baseName}.mp4");
            }

            UsePipeForInput = false;

            //TODO: Shaka packager write to Windows named pipe fails due to path issue.
            UsePipeForOutput = false;
        }

        private IEnumerable<string> GetArguments(IList<string> inputs, IList<string> outputs, IList<string> manifests)
        {
            const string DRM_LABEL = "cenc";
            var drm_label = _options.EncryptContent ? $",drm_label={DRM_LABEL}" : string.Empty;

            List<string> arguments = new(SelectedTracks.Select((t, i) =>
            {
                var ext = t.IsMultiFile ? (t is TextTrack ? VTT_FILE : MEDIA_FILE) : string.Empty;
                var file = $"{t.Source}{ext}";
                var index = Inputs.IndexOf(file);
                var multiTrack = TransmuxedSmooth && FileToTrackMap[file].Count > 1;
                var inputFile = multiTrack ? 
                    Path.Combine(Path.GetDirectoryName(inputs[index])!, $"{Path.GetFileNameWithoutExtension(file)}_{t.TrackID}{Path.GetExtension(file)}") :
                    inputs[index];
                var stream = t.Type.ToString().ToLowerInvariant();
                var language = string.IsNullOrEmpty(t.SystemLanguage) || t.SystemLanguage == "und" ? string.Empty : $"language={t.SystemLanguage},";
                return $"stream={stream},in={inputFile},out={outputs[i]},{language}playlist_name={manifests[i]}{drm_label}";
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

            if (_options.EncryptContent)
            {
                arguments.Add("--enable_raw_key_encryption");
                arguments.Add("--protection_scheme");
                arguments.Add("cbcs");
                arguments.Add("--keys");
                arguments.Add($"label={DRM_LABEL}:key_id={_assetDetails.KeyId}:key={_assetDetails.EncryptionKey}");
                arguments.Add("--hls_key_uri");
                arguments.Add($"{_assetDetails.LicenseUri}");
                arguments.Add("--clear_lead");
                arguments.Add("0");
            }

            arguments.Add("--temp_dir");
            arguments.Add(_options.WorkingDirectory);
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
                _logger.Log(logLevel, Events.ShakaPackager, line);
            }
        }
    }
}
