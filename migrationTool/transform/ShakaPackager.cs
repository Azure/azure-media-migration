using AMSMigrate.Contracts;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace AMSMigrate.Transform
{
    enum TextTrackRole
    {
        Main,
        Alternate,
        Supplementary
    }

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
            else if (manifest.Format == "vod-fmp4")
            {
                if (assetDetails.ClientManifest != null)
                {
                    var clientManifest = assetDetails.ClientManifest!;

                    MediaStream? audioStream = null;
                    MediaStream? videoStream = null;

                    int numAudioStreams = 0;
                    int numVideoStreams = 0;
                    foreach (var stream in clientManifest.Streams)
                    {
                        if (stream.Type == StreamType.Video)
                        {
                            videoStream = stream;
                            numVideoStreams++;
                        }
                        else if (stream.Type == StreamType.Audio)
                        {
                            audioStream = stream;
                            numAudioStreams++;
                        }
                    }

                    if (numAudioStreams > 1 || numVideoStreams > 1)
                    {
                        _logger.LogError("audio streams {audioStreams} > 1 or video streams {videoStreams} > 1 found, current this is not supported",
                            numAudioStreams, numVideoStreams);
                        throw new InvalidDataException("Multiple audio streams or video streams is not currently supported.");
                    }

                    if (numAudioStreams == 1 && numVideoStreams == 1 && audioStream != null && videoStream != null)
                    {
                        LiveArchiveStreamInfoData.AudioStartTime = audioStream.GetStartTimeStamp();
                        LiveArchiveStreamInfoData.AudioTimeScale = audioStream.TimeScale;
                        LiveArchiveStreamInfoData.AudioStreamHasDiscontinuities = audioStream.HasDiscontinuities();

                        LiveArchiveStreamInfoData.VideoStartTime = videoStream.GetStartTimeStamp();
                        LiveArchiveStreamInfoData.VideoTimeScale = videoStream.TimeScale;
                        LiveArchiveStreamInfoData.VideoStartTimeInAudioTimeScale = LiveArchiveStreamInfoData.VideoStartTime * LiveArchiveStreamInfoData.AudioTimeScale / LiveArchiveStreamInfoData.VideoTimeScale;

                        _logger.LogDebug("Audio start time: {time}, audio time scale: {timeScale}, audio discontinuity: {flag}", LiveArchiveStreamInfoData.AudioStartTime,
                            LiveArchiveStreamInfoData.AudioTimeScale, LiveArchiveStreamInfoData.AudioStreamHasDiscontinuities);
                        _logger.LogDebug("Video start time: {time}, video time scale: {timeScale}", LiveArchiveStreamInfoData.VideoStartTime, LiveArchiveStreamInfoData.VideoTimeScale);
                        _logger.LogDebug("video start time in audio time scale: {time}", LiveArchiveStreamInfoData.VideoStartTimeInAudioTimeScale);

                        if (Math.Abs(LiveArchiveStreamInfoData.AudioStartTime - LiveArchiveStreamInfoData.VideoStartTimeInAudioTimeScale) <= 0.1 * LiveArchiveStreamInfoData.AudioTimeScale
                            && !LiveArchiveStreamInfoData.AudioStreamHasDiscontinuities)
                        {
                            ProcessLiveArchiveAudio = false;
                        }
                        else
                        {
                            _logger.LogDebug("video / audio tracks start time not within 0.1 sec or audio stream has discontinuities, transcode required");
                            ProcessLiveArchiveAudio = true;
                        }

                        if (videoStream.HasDiscontinuities())
                        {
                            ProcessLiveArchiveVideo = true;
                        }

                        ProcessLiveArchiveVTT = true;
                    }
                }
            }

            UsePipeForInput = false;

            //TODO: Shaka packager write to Windows named pipe fails due to path issue.
            UsePipeForOutput = false;
        }

        private IEnumerable<string> GetArguments(IList<string> inputs, IList<string> outputs, IList<string> manifests)
        {
            const string DRM_LABEL = "cenc";
            var drm_label = _options.EncryptContent ? $",drm_label={DRM_LABEL}" : string.Empty;
            var values = Enum.GetValues<TextTrackRole>();
            var text_tracks = 0;
            List<string> arguments = new(SelectedTracks.Select((t, i) =>
            {
                if (!string.IsNullOrEmpty(manifests[i]))
                {
                    var source = t.Parameters?.SingleOrDefault(p => p.Name == TRANSCRIPT_SOURCE)?.Value ?? t.Source;
                    var ext = t.IsMultiFile ? (t is TextTrack ? VTT_FILE : MEDIA_FILE) : string.Empty;
                    var file = $"{source}{ext}";
                    var index = Inputs.IndexOf(file);
                    var multiTrack = TransmuxedSmooth && FileToTrackMap[file].Count > 1;
                    var inputFile = multiTrack ?
                        Path.Combine(Path.GetDirectoryName(inputs[index])!, $"{Path.GetFileNameWithoutExtension(file)}_{t.TrackID}{Path.GetExtension(file)}") :
                        inputs[index];
                    var stream = t.Type.ToString().ToLowerInvariant();
                    var language = string.IsNullOrEmpty(t.SystemLanguage) || t.SystemLanguage == "und" ? string.Empty : $",language={t.SystemLanguage},";
                    var role = t is TextTrack ? $",dash_role={values[text_tracks++ % values.Length].ToString().ToLowerInvariant()}" : string.Empty;
                    var useType = SelectedTracks.Count(x => x.Source == t.Source && x.Type == t.Type) == 1;
                    return $"stream={(useType ? stream: t.TrackID - 1)},in={inputFile},out={outputs[i]},playlist_name={manifests[i]}{language}{drm_label}{role}";
                }
                else
                {
                    return "";
                }
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
            arguments.Add("--mpd_output");
            arguments.Add(dash);
            arguments.Add("--hls_master_playlist_output");
            arguments.Add(hls);
            return arguments;
        }

        /// <summary>
        /// Adjust the list of SelectedTracks, Output and Manifests property after all the input files are downloaded/Preprocessed.
        /// Give the packager a chance to remove some tracks, especially for SubTitle tracks related to vtt files.
        /// </summary>
        /// <param name="workingDirectory">The working directory in local machine for packager.</param>
        /// <returns></returns>
        public override void AdjustPackageFiles(string workingDirectory)
        {
            // The disableTracks contains a list of tracks that can be disabled for the ShakaPackager.
            // each item has the track index in SelectedTracks and the matching vtt file name.
            var disabledTracks = new Dictionary<int, string>();

            var cmftTracks = new Dictionary<int, string>();
            int ti = -1;

            // Hold the cmft track which has the largest size of vtt file on each language.
            // The key is the language, the value is the pair of track index and the maximum file size of vtt file.
            // if the language is not set, use empty string "" as the key.
            var largestCmftVtt = new Dictionary<string, Tuple<int, long>>();

            foreach (var t in SelectedTracks)
            {
                ti++;

                if (t is TextTrack)
                {
                    if (t.Source.EndsWith(VTT_FILE))
                    {
                        // Only NBest close caption track takes .vtt file as source,
                        // And there must be a single NBest track in an asset.
                        // This track will be disabled for ShakaPackager.

                        disabledTracks.Add(ti, t.Source);
                    }
                    else if (t.Source.ToLower().EndsWith(CMFT_FILE))
                    {
                        var vttSource = t.Parameters.SingleOrDefault(p => p.Name == TRANSCRIPT_SOURCE)?.Value;

                        if (!string.IsNullOrEmpty(vttSource))
                        {
                            string filePath = Path.Combine(workingDirectory, vttSource!);
                            long length = new FileInfo(filePath).Length;
                            string lang = t.SystemLanguage ?? "";

                            if (largestCmftVtt.TryGetValue(lang, out var largeCmft))
                            {
                                if (largeCmft.Item2 < length)
                                {
                                    largestCmftVtt[lang] = new Tuple<int, long>(ti, length);
                                }
                            }
                            else
                            {
                                largestCmftVtt.Add(lang, new Tuple<int, long>(ti, length));
                            }

                            cmftTracks.Add(ti, vttSource);
                        }
                    }
                }
            }

            if (cmftTracks.Count > 0)
            {
                // There are several text tracks with .cmft files which are generated from vtt source files.
                // Hold the track with the largest file size of vtt file for each language.
                // disable other text tracks for ShakaPackager.
                foreach (var cmft in cmftTracks)
                {
                    Track t = SelectedTracks[cmft.Key];
                    string lang = t.SystemLanguage ?? "";

                    if (cmft.Key != largestCmftVtt[lang].Item1)
                    {
                        disabledTracks.Add(cmft.Key, cmft.Value);
                    }
                }
            }

            foreach (var dt in disabledTracks)
            {
                // For each disabled subtitle track,
                // no .m3u8 file is generated for HLS,
                // no stream is added for DASH manifest,
                // But the vtt file with the adjusted timestamps is still copied over to the destination folder.
                // Update the settings in Outputs and Manifests list appropriately.
                var si = dt.Key;

                Manifests[si] = "";

                Outputs[si] = dt.Value;

                // Copy over the source vtt files into the output folder.
                var src = Path.Combine(workingDirectory, dt.Value!);
                var outputDirectory = Path.Combine(workingDirectory, "output");
                var dest = Path.Combine(outputDirectory, dt.Value!);

                File.Copy(src, dest, true);
            }

            if (disabledTracks.Count > 0)
            {
                UsePipeForManifests = false;
            }
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

        private void LogStandardError(string? line)
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
