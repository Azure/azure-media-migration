using System.Text;
using System.Text.RegularExpressions;
using System.Xml;


namespace AMSMigrate.Transform;

public static class VttConverter
{

    private static ulong ParseVTTTimestampInMs(string timestamp)
    {
        ulong ts = 0;

        // match hour form
        var matchHour = Regex.Match(timestamp, "([0-9]{2,}):([0-9]{2}):([0-9]{2})[\\.]([0-9]{3})");
        if (matchHour.Success)
        {
            ulong h = ulong.Parse(matchHour.Groups[1].Value);
            ulong m = ulong.Parse(matchHour.Groups[2].Value);
            ulong s = ulong.Parse(matchHour.Groups[3].Value);
            ulong ms = ulong.Parse(matchHour.Groups[4].Value);
            ts = h * 60 * 60 * 1000 + m * 60 * 1000 + s * 1000 + ms;
            return ts;
        }

        var match = Regex.Match(timestamp, "([0-9]{2}):([0-9]{2})[,\\.]([0-9]{3})");
        if (match.Success)
        {
            ulong m = ulong.Parse(match.Groups[1].Value);
            ulong s = ulong.Parse(match.Groups[2].Value);
            ulong ms = ulong.Parse(match.Groups[3].Value);
            ts = m * 60 * 1000 + s * 1000 + ms;
            return ts;
        }

        throw new InvalidDataException($"Unsupported VTT time stamp {timestamp}");
    }

    private static string VTTTimestampToString(ulong timestamp)
    {
        ulong ms = timestamp;
        ulong s = ms / 1000;
        ms = ms - 1000 * s;
        ulong m = s / 60;
        s -= m * 60;
        ulong h = m / 60;
        m -= 60 * h;

        if (h > 0)
        {
            return $"{h:00}:{m:00}:{s:00}.{ms:000}";
        }
        return $"00:{m:00}:{s:00}.{ms:000}";
    }

    public static byte[]? ConvertTTMLtoVTT(byte[]? ttmlText, long offsetInMs)
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

                long startTime = (long)ParseVTTTimestampInMs(strStartTime);
                long endTime = (long)ParseVTTTimestampInMs(strEndTime);

                startTime -= offsetInMs;
                endTime -= offsetInMs;

                if (endTime > 0)
                {
                    if (startTime < 0)
                    {
                        startTime = 0;
                    }

                    var startTimeStr = VTTTimestampToString((ulong)startTime);
                    var endTimeStr = VTTTimestampToString((ulong)endTime);

                    if (!string.IsNullOrEmpty(strAlign) && !string.IsNullOrEmpty(strStartCue) && !string.IsNullOrEmpty(strCueSize) && !string.IsNullOrEmpty(formattedString))
                    {
                        strEntry = string.Format(
                            "\n\n{0} --> {1} position:{2} align:{3} size:{4}\n{5}",
                            startTimeStr, endTimeStr, strStartCue, strAlign, strCueSize, formattedString);
                    }
                    else if (!string.IsNullOrEmpty(formattedString))
                    {
                        strEntry = string.Format(
                            "\n\n{0} --> {1}\n{2}",
                            startTimeStr, endTimeStr, formattedString);
                    }
                    if (!string.IsNullOrEmpty(strEntry))
                    {
                        webVttContent.Append(strEntry);
                    }
                }
            }

            webVttContentRes = webVttContent.ToString();
        }
        byte[]? contentBytes = null;
        if (!string.IsNullOrEmpty(webVttContentRes))
        {
            contentBytes = Encoding.UTF8.GetBytes(webVttContentRes);
        }
        return contentBytes;
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
