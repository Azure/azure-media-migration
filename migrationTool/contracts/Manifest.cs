using Microsoft.Extensions.Logging;
using System.Text;
using System.Xml.Serialization;

namespace AMSMigrate.Contracts
{
    class Constants
    {
        public const string Namespace = "http://www.w3.org/2001/SMIL20/Language";
    }

    public class Metadata
    {
        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;

        [XmlAttribute("content")]
        public string Value { get; set; } = string.Empty;
    }

    public class Property
    {
        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;

        [XmlAttribute("value")] public string Value { get; set; } = string.Empty;

        [XmlAttribute("valuetype")] public string ValueType { get; set; } = string.Empty;
    }

    abstract public class Track
    {
        public Track(StreamType type)
        {
            Type = type;
        }

        [XmlAttribute("src")] public string Source { get; set; } = string.Empty;

        [XmlAttribute("systemBitrate")]
        public int SystemBitrate { set; get; }

        [XmlAttribute("systemLanguage")]
        public string? SystemLanguage { get; set; }

        [XmlElement("param", Namespace = Constants.Namespace)]
        public Property[] Parameters { get; set; } = Array.Empty<Property>();

        public StreamType Type { get; }

        // Check if the track is stored as one file per fragment.
        public bool IsMultiFile => string.IsNullOrEmpty(Path.GetExtension(Source));

        public uint TrackID => uint.Parse(Parameters?.SingleOrDefault(p => p.Name == "trackID")?.Value ?? "1");

        public string TrackName => Parameters?.SingleOrDefault(p => p.Name == "trackName")?.Value ?? Type.ToString().ToLower();
    }

    public class VideoTrack : Track
    {
        public VideoTrack() : base(StreamType.Video) { }
    }

    public class AudioTrack : Track
    {
        public AudioTrack() : base(StreamType.Audio) { }
    }

    public class TextTrack : Track
    {
        public TextTrack() : base(StreamType.Text) { }
    }


    [XmlType("body", Namespace = Constants.Namespace)]
    public class ManifestBody
    {
        [XmlArray("switch", Namespace = Constants.Namespace)]
        [XmlArrayItem("video", Type = typeof(VideoTrack), Namespace = Constants.Namespace)]
        [XmlArrayItem("audio", Type = typeof(AudioTrack), Namespace = Constants.Namespace)]
        [XmlArrayItem("textstream", Type = typeof(TextTrack), Namespace = Constants.Namespace)]
        [XmlArrayItem("ref", Namespace = Constants.Namespace)]
        public Track[] Tracks { get; set; } = Array.Empty<Track>();
    }

    [XmlRoot("smil", Namespace = Constants.Namespace)]
    public class Manifest
    {
        [XmlArray("head", Namespace = Constants.Namespace)]
        [XmlArrayItem("meta", Namespace = Constants.Namespace)]
        public Metadata[] Metadata { get; set; } = Array.Empty<Metadata>();

        [XmlElement("body", Namespace = Constants.Namespace)]
        public ManifestBody Body { get; set; } = new ManifestBody();

        public string Format => Metadata.SingleOrDefault(s => s.Name == "formats")?.Value ?? "fmp4";

        public bool IsLiveArchive => Format == "vod-cmaf" || Format == "vod-fmp4";

        public bool IsLive => Format == "ll-cmaf" || Format == "live-fmp4";

        public string? ClientManifest => Metadata.SingleOrDefault(m => m.Name.Equals("clientManifestRelativePath", StringComparison.InvariantCultureIgnoreCase))?.Value;

        public Track[] Tracks => Body.Tracks;

        [XmlIgnore]
        public string? FileName { get; private set; }

        public static Manifest Parse(Stream stream, string filename, ILogger logger)
        {
            var serializer = new XmlSerializer(typeof(Manifest));
            serializer.UnknownElement += (s, args) =>
            {
                logger.LogTrace("Unknown element in manifest {args}", args.Element.Name);
            };

            serializer.UnknownAttribute += (s, args) =>
            {
                logger.LogTrace("Unknown attribute in manifest {args}", args.Attr.Name);
            };
            var manifest = serializer.Deserialize(new StreamReader(stream, Encoding.UTF8)) as Manifest;
            if (manifest == null) throw new ArgumentException("Invalid data", nameof(stream));
            manifest.FileName = filename;

            // fix missing SystemBitrate, try to determine from source name
            foreach (var track in manifest.Body.Tracks.Where(x => x.SystemBitrate <= 0))
            {
                string[] sourceParts = Path.GetFileNameWithoutExtension(track.Source).Split('_', StringSplitOptions.RemoveEmptyEntries);
                if (sourceParts.Length > 0 && int.TryParse(sourceParts.Last(), out int systemBitrate))
                {
                    track.SystemBitrate = systemBitrate;
                }
            }

            return manifest;
        }
    }
}
