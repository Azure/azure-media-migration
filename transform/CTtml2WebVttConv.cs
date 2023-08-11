
using Microsoft.Extensions.Logging;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Xml;
namespace AMSMigrate.Transform
{
    public class TtmlToVttConverter
    {
        public byte[]? Ttml { get; set; }

        public TtmlToVttConverter(byte[]? ttmlText)
        {
            Ttml = ttmlText;
        }


        public string? Convert()
        {
            if (Ttml == null)
            {
                throw new InvalidOperationException("TtmlToVttConverter: Ttml is null");
            }

            string? webVttContentRes = null;

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Prohibit;

            using (MemoryStream mdatStream = new MemoryStream(Ttml))
            using (XmlReader reader = XmlReader.Create(mdatStream, settings))
            {
                StringBuilder webVttContent = new StringBuilder();
                //add header
                webVttContent.Append("WEBVTT");
                webVttContent.AppendLine();  

                while (!reader.EOF)
                {
                    string? strStartTime = null, strEndTime = null, strStartCue = null, strCueSize = null, strAlign = null, strEntry = null;
                    StringBuilder strText = new StringBuilder();

                    reader.Read();
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            if (reader.Name.Equals("region", StringComparison.OrdinalIgnoreCase))
                            {
                                ParseRegionAttributes(reader, out strStartCue, out strCueSize, out strAlign);
                                continue;
                            }
                            else if (!reader.Name.Equals("p", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                            break;

                        default:
                            continue;
                    }

                    bool isEOF = reader.MoveToFirstAttribute();
                    while (isEOF)
                    {
                        string pszName = reader.Name;
                        string pszValue = reader.Value;

                        if (pszName.Equals("begin", StringComparison.OrdinalIgnoreCase))
                        {
                            strStartTime = pszValue;
                        }
                        else if (pszName.Equals("end", StringComparison.OrdinalIgnoreCase))
                        {
                            strEndTime = pszValue;
                        }
                        isEOF = reader.MoveToNextAttribute();
                    }

                    if (string.IsNullOrEmpty(strStartTime) || string.IsNullOrEmpty(strEndTime))
                    {
                        throw new InvalidOperationException("Missing begin or end attributes.");
                    }

                    XmlNodeType nodeType;
                    bool fDone = false;

                    do
                    {
                        reader.Read();
                        nodeType = reader.NodeType;

                        switch (nodeType)
                        {
                            case XmlNodeType.Text:
                                string pszValue = reader.Value;
                                strText.Append(pszValue);
                                break;

                            case XmlNodeType.Element:
                                string pszName = reader.Name;
                                if (pszName.Equals("br", StringComparison.OrdinalIgnoreCase))
                                {
                                    strText.AppendLine();
                                }
                                break;

                            case XmlNodeType.EndElement:
                                string endElementName = reader.Name;
                                if (endElementName.Equals("p", StringComparison.OrdinalIgnoreCase))
                                {
                                    fDone = true;
                                }
                                break;

                            default:
                                break;
                        }
                    } while (!fDone);

                    if (!string.IsNullOrEmpty(strAlign) && !string.IsNullOrEmpty(strStartCue) && !string.IsNullOrEmpty(strCueSize))
                    {
                        strEntry = string.Format(
                            "\n\n{0} --> {1} position:{2} align:{3} size:{4}\n{5}",
                            strStartTime, strEndTime, strStartCue, strAlign, strCueSize, strText.ToString());
                    }
                    else
                    {
                        strEntry = string.Format(
                            "\n\n{0} --> {1}\n{2}",
                            strStartTime, strEndTime, strText.ToString());
                    }
                    webVttContent.Append(strEntry);
                }
                webVttContentRes = webVttContent.ToString();
            }
            return webVttContentRes;
        }



        public void ParseRegionAttributes(XmlReader pReader, out string strStartCue, out string strCueSize, out string strAlign)
        {
            strStartCue = strCueSize = strAlign = string.Empty;

            while (pReader.MoveToNextAttribute())
            {
                string pszName = pReader.Name;
                string pszValue = pReader.Value;
                string? pszTmp;

                if (pszName.Equals("tts:origin", StringComparison.OrdinalIgnoreCase))
                {
                    pszTmp = pszValue.IndexOf('%') >= 0 ? pszValue.Substring(0, pszValue.IndexOf('%') + 1) : null;
                    if (pszTmp != null)
                    {
                        strStartCue = pszTmp;
                    }
                }
                else if (pszName.Equals("tts:extent", StringComparison.OrdinalIgnoreCase))
                {
                    pszTmp = pszValue.IndexOf('%') >= 0 ? pszValue.Substring(0, pszValue.IndexOf('%') + 1) : null;
                    if (pszTmp != null)
                    {
                        strCueSize = pszTmp;
                    }
                }
                else if (pszName.Equals("tts:textAlign", StringComparison.OrdinalIgnoreCase))
                {
                    strAlign = pszValue;
                }
            }
        }

    }
}
