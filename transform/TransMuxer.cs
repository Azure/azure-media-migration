using AMSMigrate.Ams;
using AMSMigrate.Contracts;
using Azure.ResourceManager.Media.Models;
using FFMpegCore;
using FFMpegCore.Pipes;
using Microsoft.Extensions.Logging;

namespace AMSMigrate.Transform
{
    internal class TransMuxer
    {
        static readonly StreamingPolicyStreamingProtocol Protocol = StreamingPolicyStreamingProtocol.Hls;

        private readonly AssetOptions _options;
        private readonly ILogger _logger;
        private string? _hostName = null;

        public TransMuxer(
            AssetOptions options,
            ILogger<TransMuxer> logger)
        {
            _logger = logger;
            _options = options;
        }

        public async Task<Uri?> GetStreamingUrlAsync(AssetRecord record, CancellationToken cancellationToken)
        {
            var hostName = await GetHostNameAsync(record, cancellationToken);
            var locator = await record.Account.GetStreamingLocatorAsync(record.Asset, cancellationToken);
            if (locator == null)
            {
                return null;
            }

            StreamingPathsResult pathResult = await locator.GetStreamingPathsAsync(cancellationToken);
            var path = pathResult.StreamingPaths.SingleOrDefault(p => p.StreamingProtocol == Protocol);
            if (path == null)
            {
                _logger.LogWarning("The locator {locator} has no HLS streaming support.", locator.Id);
                return null;
            }
            var uri = new UriBuilder("https", _hostName)
            {
                Path = path.Paths[0] + ".m3u8"
            }.Uri;
            return uri;
        }

        private async Task<string> GetHostNameAsync(AssetRecord record, CancellationToken cancellationToken)
        {
            if (_hostName == null)
            {
                _hostName = await record.Account.GetStreamingEndpointAsync(cancellationToken: cancellationToken);
            }
            return _hostName;
        }

        public async Task<IMediaAnalysis> AnalyzeAsync(Uri uri, CancellationToken cancellationToken)
        {
            return await FFProbe.AnalyseAsync(uri, null, cancellationToken);
        }

        public async Task TransmuxUriAsync(Uri uri, FFMpegCore.MediaStream stream, string filePath, CancellationToken cancellationToken)
        {
            var processor = FFMpegArguments.FromUrlInput(uri)
                .OutputToFile(filePath, overwrite: true, options =>
                {
                    options.CopyChannel()
                    .SelectStream(stream.Index, 0)
                    .WithCustomArgument("-movflags faststart");
                });
            _logger.LogInformation("Running ffmpeg {args}", processor.Arguments);
            await processor
                .CancellableThrough(cancellationToken)
                .ProcessAsynchronously(true);
        }

        public async Task TransmuxUriAsync(Uri uri, string filePath, CancellationToken cancellationToken)
        {
            var result = await FFProbe.AnalyseAsync(uri, null, cancellationToken);
            var processor = FFMpegArguments.FromUrlInput(uri)
                .OutputToFile(filePath, overwrite: true, options =>
                {
                    foreach (var stream in result.AudioStreams)
                    {
                        options.SelectStream(stream.Index, 0);
                    }
                    foreach (var stream in result.VideoStreams)
                    {
                        options.SelectStream(stream.Index, 0);
                    }

                    options.CopyChannel()
                    .WithCustomArgument("-movflags faststart");
                });
            _logger.LogDebug("Running ffmpeg {args}", processor.Arguments);
            await processor
                .CancellableThrough(cancellationToken)
                .ProcessAsynchronously(true);
        }

        public async Task TransMuxAsync(IPipeSource source, string destination, CancellationToken cancellationToken)
        {
            await FFMpegArguments
                .FromPipeInput(source)
                //.WithGlobalOptions(options => options.WithVerbosityLevel(FFMpegCore.Arguments.VerbosityLevel.Verbose))
                .OutputToFile(destination, overwrite: false, options =>
                {
                    if (Path.GetExtension(destination) == BasePackager.VTT_FILE)
                    {
                        options
                        .ForceFormat("webvtt")
                        .WithCustomArgument("-map s -c:s copy");
                    }
                    else
                    {
                        options
                        .CopyChannel()
                        .ForceFormat("mp4")
                        .WithCustomArgument("-movflags faststart");
                    }
                }).ProcessAsynchronously(throwOnError: true);
        }
    }
}
