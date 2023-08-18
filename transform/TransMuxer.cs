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
            while (source.Position < source.Length)
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

        static void AddOffsetToTfdtBox(string fileName, ulong decodeTimeOffset)
        {
            using var stream = File.Open(fileName, FileMode.Open, FileAccess.ReadWrite);
            var reader = new MP4Reader(stream);
            var writer = new MP4Writer(stream);
            stream.Position = 0;

            while (stream.Position < stream.Length)
            {
                Int64 startPosition = reader.BaseStream.Position;
                Int64 size = reader.ReadUInt32();   // size of current box
                UInt32 type = reader.ReadUInt32();  // type of current box

                // Parse extended size
                if (size == 0)
                {
                    throw new InvalidDataException("Invalid size.");
                }
                else if (size == 1)
                {
                    size = reader.ReadInt64();
                }

                if (type == MP4BoxType.moof)
                {
                    stream.Position = startPosition; // rewind
                    var moofBox = MP4BoxFactory.ParseSingleBox<moofBox>(reader);

                    ulong offsetToTfdt = moofBox.ComputeBaseSizeBox();
                    foreach (var c in moofBox.Children)
                    {
                        if (c.Type == MP4BoxType.traf)
                        {
                            offsetToTfdt += moofBox.ComputeBaseSizeBox();
                            foreach (var cc in c.Children)
                            {
                                if (cc.Type == MP4BoxType.tfdt)
                                {
                                    tfdtBox tfdtBox = (tfdtBox)cc; // will throw
                                    long tfdtPosition = startPosition + (long)offsetToTfdt;
                                    tfdtBox.DecodeTime += decodeTimeOffset;
                                    stream.Position = tfdtPosition;
                                    tfdtBox.WriteTo(writer);
                                    break;
                                }
                                offsetToTfdt += cc.Size.Value;
                            }
                        }
                        else
                        {
                            offsetToTfdt += c.Size.Value;
                        }
                    }
                }
                //skip till the beginning of the next box.
                stream.Position = startPosition + size;
            }
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
                        if (transcodeAudioInfo.AudioStreamHasDiscontinuities)
                        {
                            audioFilterOptions.Arguments.Add(new AudioResample());
                        }
                        audioFilterOptions.Arguments.Add(new AudioDelayFilterArgument(delayInMs));
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
            // FFmpeg will zero out tdft baseMediaDecodeTime, add it back in place.
            AddOffsetToTfdtBox(destination, (ulong)transcodeAudioInfo.VideoStartTimeInAudioTimeScale);
        }

        private static List<ulong> GetDecodeMediaTime(string fileName)
        {
            using var stream = File.Open(fileName, FileMode.Open, FileAccess.Read);
            var reader = new MP4Reader(stream);
            stream.Position = 0;
            List<ulong> decodeTimes = new();
            while (stream.Position < stream.Length)
            {
                Int64 startPosition = reader.BaseStream.Position;
                Int64 size = reader.ReadUInt32();   // size of current box
                UInt32 type = reader.ReadUInt32();  // type of current box

                // Parse extended size
                if (size == 0)
                {
                    throw new InvalidDataException("Invalid size.");
                }
                else if (size == 1)
                {
                    size = reader.ReadInt64();
                }

                if (type == MP4BoxType.moof)
                {
                    stream.Position = startPosition; // rewind
                    var moofBox = MP4BoxFactory.ParseSingleBox<moofBox>(reader);

                    foreach (var c in moofBox.Children)
                    {
                        if (c.Type == MP4BoxType.traf)
                        {
                            foreach (var cc in c.Children)
                            {
                                if (cc.Type == MP4BoxType.tfdt)
                                {
                                    tfdtBox tfdtBox = (tfdtBox)cc; // will throw
                                    decodeTimes.Add(tfdtBox.DecodeTime);
                                    break;
                                }
                            }
                        }
                    }
                }
                //skip till the beginning of the next box.
                stream.Position = startPosition + size;
            }
            return decodeTimes;
        }

        private void UpdateTrackRunDuration(string fileName)
        {
            List<ulong> decodeTimes = GetDecodeMediaTime(fileName);

            using var stream = File.Open(fileName, FileMode.Open, FileAccess.ReadWrite);
            var reader = new MP4Reader(stream);
            var writer = new MP4Writer(stream);
            stream.Position = 0;


            int totalDecodeTime = decodeTimes.Count;
            int curDecodeTimesIndex = 0;

            while (stream.Position < stream.Length)
            {
                Int64 startPosition = reader.BaseStream.Position;
                Int64 size = reader.ReadUInt32();   // size of current box
                UInt32 type = reader.ReadUInt32();  // type of current box

                // Parse extended size
                if (size == 0)
                {
                    throw new InvalidDataException("Invalid size.");
                }
                else if (size == 1)
                {
                    size = reader.ReadInt64();
                }

                if (type == MP4BoxType.moof)
                {
                    stream.Position = startPosition; // rewind
                    var moofBox = MP4BoxFactory.ParseSingleBox<moofBox>(reader);

                    ulong offsetToTrun = moofBox.ComputeBaseSizeBox();
                    foreach (var c in moofBox.Children)
                    {
                        if (c.Type == MP4BoxType.traf)
                        {
                            offsetToTrun += moofBox.ComputeBaseSizeBox();
                            foreach (var cc in c.Children)
                            {
                                if (cc.Type == MP4BoxType.trun)
                                {
                                    trunBox trunBox = (trunBox)cc; // will throw
                                    trunBox.TrunFlags flag = (trunBox.TrunFlags) trunBox.Flags;
                                    if ((flag & trunBox.TrunFlags.SampleDurationPresent) != trunBox.TrunFlags.SampleDurationPresent)
                                    {
                                        throw new InvalidDataException("Unexpected, sampleDurationPresent must be present");
                                    }

                                    long trunPosition = startPosition + (long)offsetToTrun;
                                    stream.Position = trunPosition;
                                    ulong totalDuration = 0;
                                    for (int i = 0; i < trunBox.Entries.Count; ++i)
                                    {
                                        totalDuration += (ulong)trunBox.Entries[i].SampleDuration!;
                                    }
                                    if (curDecodeTimesIndex + 1 < decodeTimes.Count)
                                    {
                                        if (decodeTimes[curDecodeTimesIndex] + totalDuration < decodeTimes[curDecodeTimesIndex + 1])
                                        {
                                            ulong offset = decodeTimes[curDecodeTimesIndex + 1] - decodeTimes[curDecodeTimesIndex] - totalDuration;
                                            _logger.LogTrace("Update duration due to discontinuity at dt {0}, offset {1}.", decodeTimes[curDecodeTimesIndex], offset);
                                            trunBox.Entries[trunBox.Entries.Count - 1].SampleDuration += (uint)offset;
                                        }
                                    }
                                    trunBox.WriteTo(writer);
                                    curDecodeTimesIndex++;
                                    break;
                                }
                                offsetToTrun += cc.Size.Value;
                            }
                        }
                        else
                        {
                            offsetToTrun += c.Size.Value;
                        }
                    }
                }
                //skip till the beginning of the next box.
                stream.Position = startPosition + size;
            }
        }

        public void TranscodeVideo(string destination)
        {
            UpdateTrackRunDuration(destination);
        }
    }
}
