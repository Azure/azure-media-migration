using AMSMigrate.Contracts;
using AMSMigrate.Fmp4;
using Azure.ResourceManager.Media.Models;
using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using Microsoft.Extensions.Azure;
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
        public class AudioDelayFilterArgument : IAudioFilterArgument
        {
            private readonly long _delay = 0;

            public AudioDelayFilterArgument(long delay)
            {
                _delay = delay;
            }
            public string Key { get; } = "adelay";
            public string Value => $"{_delay}:all=1";
        }

        public class AudioResample : IAudioFilterArgument
        {
            public string Key { get; } = "aresample";
            public string Value => $"async=1";
        }

        public async Task TranscodeAudioAsync(string source, string destination, TranscodeAudioInfo transcodeAudioInfo, CancellationToken cancellationToken)
        {
            if (transcodeAudioInfo.AudioStartTime > transcodeAudioInfo.VideoStartTimeInAudioTimeScale)
            {
                long delayInMs = (transcodeAudioInfo.AudioStartTime - transcodeAudioInfo.VideoStartTimeInAudioTimeScale) * 1000 / transcodeAudioInfo.AudioTimeScale;
                var processor = FFMpegArguments
                .FromFileInput(source)
                //.WithGlobalOptions(options => options.WithVerbosityLevel(FFMpegCore.Arguments.VerbosityLevel.Verbose))
                .OutputToFile(destination, overwrite: true, options =>
                options
                .WithAudioFilters(
                    audioFilterOptions =>
                    {
                        audioFilterOptions.Arguments.Add(new AudioDelayFilterArgument(delayInMs));
                        if (transcodeAudioInfo.AudioStreamHasDiscontinuities)
                        {
                            audioFilterOptions.Arguments.Add(new AudioResample());
                        }
                    }
                )
                .WithAudioCodec(AudioCodec.Aac)
                .ForceFormat("mp4")
                .WithCustomArgument("-movflags cmaf"));

                await RunAsync(processor, cancellationToken);

            }
            else if (transcodeAudioInfo.AudioStartTime <= transcodeAudioInfo.VideoStartTimeInAudioTimeScale)
            {
                if (transcodeAudioInfo.AudioStreamHasDiscontinuities)
                {
                    long trimInMs = (transcodeAudioInfo.VideoStartTimeInAudioTimeScale - transcodeAudioInfo.AudioStartTime) * 1000 / transcodeAudioInfo.AudioTimeScale;
                    var processor = FFMpegArguments
                    .FromFileInput(source, false, opt => opt.Seek(TimeSpan.FromMilliseconds(trimInMs)))
                    //.WithGlobalOptions(options => options.WithVerbosityLevel(FFMpegCore.Arguments.VerbosityLevel.Verbose))
                    .OutputToFile(destination, overwrite: true, options =>
                    options
                    .WithAudioFilters(
                        audioFilterOptions =>
                        {
                            audioFilterOptions.Arguments.Add(new AudioResample());
                        }
                    )
                    .WithAudioCodec(AudioCodec.Aac)
                    .ForceFormat("mp4")
                    .WithCustomArgument("-movflags cmaf"));
                    await RunAsync(processor, cancellationToken);
                }
                else
                {
                    double trim = (transcodeAudioInfo.VideoStartTimeInAudioTimeScale - transcodeAudioInfo.AudioStartTime) * 1000.0 / transcodeAudioInfo.AudioTimeScale;
                    var processor = FFMpegArguments
                    .FromFileInput(source, false, opt => opt.Seek(TimeSpan.FromMilliseconds(trim)))
                    //.WithGlobalOptions(options => options.WithVerbosityLevel(FFMpegCore.Arguments.VerbosityLevel.Verbose))
                    .OutputToFile(destination, overwrite: false, options =>
                    options
                    .CopyChannel()
                    .ForceFormat("mp4")
                    .WithCustomArgument("-movflags cmaf"));
                    await RunAsync(processor, cancellationToken);
                }
            } 
            // TODO: rewrite tfdt box from 0 based to video start time in audio time scale.
        }
    }
}
