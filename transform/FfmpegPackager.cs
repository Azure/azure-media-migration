using AMSMigrate.Contracts;
using FFMpegCore;
using FFMpegCore.Enums;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace AMSMigrate.Transform
{
    internal class FfmpegPackager : BasePackager, IPackager
    {
        public FfmpegPackager(
            AssetDetails assetDetails,
            TransMuxer transMuxer,
            ILogger<FfmpegPackager> logger)
            : base(assetDetails, transMuxer, logger)
        {
            var manifest = assetDetails.Manifest!;
            var manifestName = Path.GetFileNameWithoutExtension(manifest.FileName);
            var manifestFiles = SelectedTracks
                .Select((t, i) => $"media_{i}{HLS_MANIFEST}")
                .ToList();
            manifestFiles.Add($"{manifestName}{HLS_MANIFEST}");
            manifestFiles.Add($"{manifestName}{DASH_MANIFEST}");
            Manifests = manifestFiles;

            UsePipeForInput = !manifest.Format.StartsWith("mp4") && !manifest.Format.Equals("fmp4");
            UsePipeForOutput = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }

        public override async Task<bool> RunAsync(
            string workingDirectory,
            string[] inputFiles,
            string[] outputFiles,
            string[] manifests,
            CancellationToken cancellationToken = default)
        {
            var manifest = _assetDetails.Manifest!;
            try
            {
                GlobalFFOptions.Configure(options => options.WorkingDirectory = workingDirectory);
                var args = FFMpegArguments.FromFileInput(inputFiles[0], verifyExists: false);
                foreach (var file in inputFiles.Skip(1))
                {
                    args.AddFileInput(file, verifyExists: false);
                }

                args.WithGlobalOptions(options => options.WithVerbosityLevel(FFMpegCore.Arguments.VerbosityLevel.Debug));
                var manifestName = Path.GetFileNameWithoutExtension(manifest.FileName);
                var dash = manifests[manifests.Length - 1];
                var hls = manifests[manifests.Length - 2];
                var processor = args.OutputToFile(dash, overwrite: true, options =>
                {
                    foreach (var track in SelectedTracks)
                    {
                        var ext = track.IsMultiFile ? MEDIA_FILE : string.Empty;
                        var index = Inputs.IndexOf($"{track.Source}{ext}");
                        options.SelectStream(0, index, track is VideoTrack ? Channel.Video : Channel.Audio);
                    }

                    options
                    .CopyChannel()
                    .ForceFormat("dash")
                    .WithCustomArgument($"-single_file 1 -single_file_name {manifestName}_$RepresentationID$.mp4 -hls_playlist 1 -hls_master_name {hls} -use_timeline 1");
                    if (manifest.Format == "m3u8-aapl")
                    {
                        options.WithCustomArgument("-vtag avc1 -atag mp4a");
                    }
                });
                _logger.LogDebug("Ffmpeg process started with args {args}", args.Text);

                var result = await processor
                    .CancellableThrough(cancellationToken)
                    .NotifyOnOutput(line => _logger.LogDebug("{line}", line))
                    .NotifyOnError(line => _logger.LogDebug("{line}", line))
                    .ProcessAsynchronously(throwOnError: true);
                _logger.LogDebug("Ffmpeg process completed successfully");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError("Ffmpeg failed with error:{ex}", ex);
                throw;
            }
        }
    }
}
