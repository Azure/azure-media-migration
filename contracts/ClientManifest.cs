using Microsoft.Extensions.Logging;
using System.Xml.Serialization;

namespace AMSMigrate.Contracts
{
    public class NameValuePair
    {
        [XmlAttribute]
        public string? Name { get; set; }

        [XmlAttribute]
        public string? Value { get; set; }
    }

    [XmlType("QualityLevel", Namespace = "")]
    public class QualityLevel
    {
        [XmlAttribute]
        public int Index { get; set; }

        [XmlAttribute]
        public string? Id { get; set; }

        [XmlAttribute]
        public int Bitrate { set; get; }

        [XmlAttribute]
        public string? FourCC { get; set; }

        [XmlAttribute]
        public string? CodecPrivateData { get; set; }

        [XmlAttribute]
        public int MaxWidth { get; set; }

        [XmlAttribute]
        public int MaxHeight { get; set; }

        [XmlAttribute]
        public int AudioTag { get; set; }

        [XmlAttribute]
        public int Channels { get; set; }

        [XmlAttribute]
        public int BitsPerSample { get; set; }

        [XmlAttribute]
        public int SamplingRate { get; set; }

        [XmlAttribute]
        public int PacketSize { get; set; }

        [XmlArray("CustomAttributes")]
        [XmlArrayItem("Attribute")]
        public List<NameValuePair> CustomAttributes { get; set; } = new List<NameValuePair>();

        [XmlArray("Discontinuities")]
        [XmlArrayItem("d")]
        public Chunk[] Discontinuities { get; set; } = Array.Empty<Chunk>();
    }

    [XmlType("c", Namespace = "")]
    public class Chunk
    {
        [XmlAttribute("t")]
        public long Time { get; set; }

        [XmlAttribute("d")]
        public long Duration { get; set; }

        [XmlAttribute("r")]
        public int Repeat { get; set; } = 0;

        [XmlAttribute("n")]
        public int Index { get; set; }
    }

    [XmlType("StreamIndex", Namespace = "")]
    public class MediaStream
    {
        [XmlAttribute]
        public StreamType Type { get; set; }

        [XmlAttribute]
        public string? Name { get; set; }

        [XmlAttribute("Subtype")]
        public string? SubType { get; set; }

        [XmlAttribute]
        public string? Language { get; set; }

        [XmlAttribute("Chunks")]
        public int ChunkCount { get; set; }

        [XmlAttribute]
        public long TimeScale { get; set; }

        [XmlAttribute]
        public long StreamStartTimestamp { get; set; }

        [XmlAttribute]
        public string? ParentStreamIndex { get; set; }

        [XmlAttribute]
        public int OutputFlag { get; set; }

        [XmlAttribute]
        public string? ManifestOutput { get; set; }

        [XmlAttribute]
        public int LastIndexOutOfDvr { get; set; }

        [XmlAttribute]
        public string? Url { get; set; }

        [XmlAttribute]
        public int Index { get; set; }

        [XmlAttribute]
        public int DisplayWidth { get; set; }

        [XmlAttribute]
        public int DisplayHeight { get; set; }

        [XmlAttribute]
        public int MaxWidth { get; set; }

        [XmlAttribute]
        public int MaxHeight { get; set; }

        [XmlAttribute("QualityLevels")]
        public int TrackCount { get; set; }

        [XmlAttribute]
        public int TargetFragmentDuration { get; set; }

        [XmlAttribute]
        public int TargetPartialFragmentDuration { get; set; }

        [XmlElement("QualityLevel")]
        public QualityLevel[] Tracks { get; set; } = Array.Empty<QualityLevel>();

        [XmlElement("c")]
        public Chunk[] Chunks { get; set; } = Array.Empty<Chunk>();

        public IEnumerable<long> GetChunks()
        {
            if (ChunkCount == 0)
                yield break;
            long time = 0;
            foreach (var chunk in Chunks)
            {
                if (chunk.Time != 0)
                {
                    time = chunk.Time;
                }
                var repeat = chunk.Repeat == 0 ? 1 : chunk.Repeat;
                for (var r = 0; r < repeat; r++)
                {
                    yield return time;
                    time += chunk.Duration;
                }
            }
        }

        public bool HasDiscontinuities()
        {
            if (ChunkCount == 0)
                return false;
            long time = 0;
            foreach (var chunk in Chunks)
            {
                if (chunk.Time != time && time != 0)
                {
                    return true;
                }
                else
                {
                    time = chunk.Time;
                }
                var repeat = chunk.Repeat == 0 ? 1 : chunk.Repeat;
                time += repeat * chunk.Duration;
            }
            return false;
        }
    }

    [XmlRoot("SmoothStreamingMedia", Namespace = "")]
    public class ClientManifest
    {
        [XmlAttribute]
        public int MajorVersion { get; set; }

        [XmlAttribute]
        public int MinorVersion { get; set; }

        [XmlAttribute]
        public long TimeScale { get; set; } = 10000000;

        [XmlAttribute]
        public long Duration { get; set; }

        [XmlElement("StreamIndex")]
        public MediaStream[] Streams { get; set; } = Array.Empty<MediaStream>();

        [XmlIgnore]
        public string? FileName { get; private set; }

        public (MediaStream Stream, int QualityLevelIndex) GetStream(Track track)
        {
            foreach (var stream in Streams)
            {
                if (stream.Type == track.Type)
                {
                    for (var i = 0; i < stream.TrackCount; ++i)
                    {
                        if (stream.Tracks[i].Bitrate == track.SystemBitrate)
                            return (stream, i);
                    }
                }
            }
            throw new ArgumentException("No matching stream found for track {0}", track.Source);
        }

        public bool HasDiscontinuities()
        {
            return Streams.Any(
                stream => (stream.Type == StreamType.Video || stream.Type == StreamType.Audio) && stream.HasDiscontinuities());
        }

        internal static ClientManifest Parse(Stream content, string filename, ILogger logger)
        {
            var serializer = new XmlSerializer(typeof(ClientManifest));
            serializer.UnknownElement += (s, args) =>
            {
                logger.LogWarning("Unknown element when parsing client manifest {element}", args.Element);
            };

            serializer.UnknownAttribute += (s, args) =>
            {
                logger.LogWarning("Unknown attribute when parsing client manifest {attr}", args.Attr);
            };
            var manifest = serializer.Deserialize(content) as ClientManifest;
            if (manifest == null) throw new ArgumentException("Invalid data", nameof(content));
            manifest.FileName = filename;
            return manifest;
        }
    }
}
