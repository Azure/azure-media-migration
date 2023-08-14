using AMSMigrate.Contracts;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

public static class TtmlToVttConverter
{
    public static string? Convert(byte[]? ttmlText)
    {
        if (ttmlText == null)
        {
            throw new InvalidOperationException("TtmlToVttConverter: Ttml is null");
        }

        string? webVttContentRes = null;

        XmlReaderSettings settings = new XmlReaderSettings();
        settings.DtdProcessing = DtdProcessing.Prohibit;

        using (MemoryStream mdatStream = new MemoryStream(ttmlText))
        using (XmlReader reader = XmlReader.Create(mdatStream, settings))
        {
            StringBuilder webVttContent = new StringBuilder();
           
            string strStartCue = string.Empty, strCueSize = string.Empty, strAlign = string.Empty;

            while (!reader.EOF)
            {
                StringBuilder strText = new StringBuilder();
                string? strStartTime = null, strEndTime = null, strEntry = null;
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
                var formattedString = strText.ToString();
                if (!string.IsNullOrEmpty(strAlign) && !string.IsNullOrEmpty(strStartCue) && !string.IsNullOrEmpty(strCueSize)&& !string.IsNullOrEmpty(formattedString))
                {
                    strEntry = string.Format(
                        "\n\n{0} --> {1} position:{2} align:{3} size:{4}\n{5}",
                        strStartTime, strEndTime, strStartCue, strAlign, strCueSize, formattedString);
                }
                else if(!string.IsNullOrEmpty(formattedString))
                {
                    strEntry = string.Format(
                        "\n\n{0} --> {1}\n{2}",
                        strStartTime, strEndTime, formattedString);
                }
                if (!string.IsNullOrEmpty(strEntry))
                 {
                    webVttContent.Append(strEntry);
                }
            }
           
             webVttContentRes= webVttContent.ToString();
        }
        return !string.IsNullOrEmpty(webVttContentRes) ? webVttContentRes: null; //webVttContentRes;Regex.Replace(webVttContentRes, @"[\x00-\x1F\x7F]", "\n")
    }

    private static void ParseRegionAttributes(XmlReader pReader, out string strStartCue, out string strCueSize, out string strAlign)
    {
        strStartCue = strCueSize = strAlign = string.Empty;

        while (pReader.MoveToNextAttribute())
        {
            string pszName = pReader.Name;
            string pszValue = pReader.Value;
            string? pszTmp = null;

            if (pszName.Equals("tts:origin", StringComparison.OrdinalIgnoreCase))
            {
                int index = pszValue.IndexOf('%');
                if (index >= 0)
                {
                    pszTmp = pszValue.Substring(0, index + 1);
                }
            }
            else if (pszName.Equals("tts:extent", StringComparison.OrdinalIgnoreCase))
            {
                int index = pszValue.IndexOf('%');
                if (index >= 0)
                {
                    pszTmp = pszValue.Substring(0, index + 1);
                }
            }
            else if (pszName.Equals("tts:textAlign", StringComparison.OrdinalIgnoreCase))
            {
                strAlign = pszValue;
            }

            if (pszTmp != null)
            {
                if (pszName.Equals("tts:origin", StringComparison.OrdinalIgnoreCase))
                {
                    strStartCue = pszTmp;
                }
                else if (pszName.Equals("tts:extent", StringComparison.OrdinalIgnoreCase))
                {
                    strCueSize = pszTmp;
                }
            }
        }
    }
}
