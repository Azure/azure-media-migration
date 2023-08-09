using AMSMigrate.Contracts;
using AMSMigrate.Fmp4;
using Azure.ResourceManager.Media.Models;
using FFMpegCore;
using FFMpegCore.Pipes;
using Microsoft.Extensions.Logging;
using System.Text;

namespace AMSMigrate.Transform
{
    internal class TransMuxer
    {
        static readonly StreamingPolicyStreamingProtocol Protocol = StreamingPolicyStreamingProtocol.Hls;

        private readonly MigratorOptions _options;
        private readonly ILogger _logger;

        public TransMuxer(
            MigratorOptions options,
            ILogger<TransMuxer> logger)
        {
            _logger = logger;
            _options = options;
        }

        public async Task<IMediaAnalysis> AnalyzeAsync(Uri uri, CancellationToken cancellationToken)
        {
            return await FFProbe.AnalyseAsync(uri, null, cancellationToken);
        }

        private async Task RunAsync(FFMpegArgumentProcessor processor, CancellationToken cancellationToken)
        {
            _logger.LogDebug(Events.Ffmpeg, "Running ffmpeg {args}", processor.Arguments);
            await processor.CancellableThrough(cancellationToken)
                .NotifyOnError(line => _logger.LogTrace(Events.Ffmpeg, line))
                .NotifyOnOutput(line => _logger.LogTrace(Events.Ffmpeg, line))
                .ProcessAsynchronously(throwOnError: true);      
        }

        public async Task TransMuxAsync(IPipeSource source, string destination, CancellationToken cancellationToken)
        {
            var processor = FFMpegArguments
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
                });
            await RunAsync(processor, cancellationToken);
        }

        /// <summary>
        /// Transmux smooth input and filter by track id.
        /// </summary>
        /// <param name="trackId">The track id to filter by.</param>
        public void TransmuxSmooth(Stream source, Stream destination, uint trackId)
        {
            using var reader = new MP4Reader(source, Encoding.UTF8, leaveOpen: true);
            using var writer = new MP4Writer(destination, Encoding.UTF8, leaveOpen: true);
            bool skip = false;
            while(source.Position < source.Length)
            {
                var box = MP4BoxFactory.ParseSingleBox(reader);
                if (box is moofBox moof)
                {
                    var traf = moof.GetExactlyOneChildBox<trafBox>();
                    var tfhd = traf.GetExactlyOneChildBox<tfhdBox>();
                    skip = tfhd.TrackId != trackId;
                    if (skip)
                    {
                        continue;
                    }

                    // Expression Encoder sets version to 0 even for signed CTS. Always set version to 1
                    var trun = traf.GetExactlyOneChildBox<trunBox>();
                    trun.Version = (byte)1;
                    moof.WriteTo(writer);
                }
                else if (box.Type == MP4BoxType.mfra)
                {
                    break;
                }
                else if (!skip)
                {
                    box.WriteTo(writer);
                }
            }
        }
    }
}
