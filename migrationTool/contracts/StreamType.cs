using System.Xml.Serialization;

namespace AMSMigrate.Contracts
{
    [XmlType(IncludeInSchema = false)]
    public enum StreamType
    {
        [XmlEnum("audio")]
        Audio,
        [XmlEnum("video")]
        Video,
        [XmlEnum("text")]
        Text
    }
}

