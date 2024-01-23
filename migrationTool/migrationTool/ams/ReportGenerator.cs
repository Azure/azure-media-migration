
using AMSMigrate.Transform;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace AMSMigrate.Ams
{
    internal class ReportRecord
    {
        public string? AssetName { get; set; }
        public string? AssetType { get; set; }
        public List<String>? LocatorIds { get; set; }

        public string? MigrationStatus { get; set; }

        public string? OutputHlsUrl { get; set; }

        public string? OutputDashUrl { get; set; }
    }

    sealed class ReportGenerator : IDisposable
    {
        private readonly string _htmlFile;
        private readonly string _jsonFile;
        private readonly TextWriter _htmlWriter;
        private readonly TextWriter _jsonWriter;
        private readonly ILogger _logger;

        private UInt32 _recordIndex = 0;

        public ReportGenerator(string htmlFile, string jsonFile, ILogger logger)
        {
            _htmlFile = htmlFile;
            _jsonFile = jsonFile;
            _htmlWriter = GetTextWriter(htmlFile);
            _jsonWriter = GetTextWriter(jsonFile);

            _logger = logger;
        }

        public void Dispose()
        {
            _htmlWriter.Dispose();
            _logger.LogInformation("See file {file} for detailed html report.", _htmlFile);

            _jsonWriter.Dispose();
            _logger.LogInformation("See file {file} for detailed json report.", _jsonFile);
        }

        public void WriteHeader()
        {
            _logger.LogDebug("Writing html report to {file}", _htmlFile);
            WriteHtmlHeader();

            _logger.LogDebug("Writing json report to {file}", _jsonFile);
            WriteJsonHeader();
        }

        public void WriteRecord(AnalysisResult result)
        {
            lock (this)
            {
                var record = new ReportRecord
                {
                    AssetName = result.AssetName,
                    AssetType = result.AssetType,
                    MigrationStatus = result.Status.ToString(),
                    LocatorIds = result.LocatorIds,
                    OutputDashUrl = result.OutputDashUrl?.ToString(),
                    OutputHlsUrl = result.OutputHlsUrl?.ToString()
                };

                WriteHtmlRow(record);

                WriteJsonRecord(record);
            }
        }

        public void WriteTrailer()
        {
            WriteHtmlTrailer();

            WriteJsonTrailer();
        }

        private TextWriter GetTextWriter(string outputFile)
        {
            var outputStream = File.OpenWrite(outputFile);

            var writer = new StreamWriter(outputStream, Encoding.UTF8);

            return writer;
        }

        private void WriteJsonHeader()
        {
            _jsonWriter.WriteLine("{");
            _jsonWriter.WriteLine(@"    ""Asset Migration Report"" : [");
        }
        private void WriteJsonTrailer()
        {
            _jsonWriter.WriteLine("");
            _jsonWriter.WriteLine("    ]");
            _jsonWriter.WriteLine("}");
        }

        private void WriteJsonRecord(ReportRecord record)
        {
            var jsonString = JsonSerializer.Serialize(record);

            if (_recordIndex > 0)
            {
                _jsonWriter.WriteLine(",");
            }

            _jsonWriter.Write("        " + jsonString);

            _recordIndex++;
        }

        private void WriteHtmlHeader()
        {
            _htmlWriter.WriteLine(@"
<html>
  <head>
    <style>
  table {
    font-family: ""Courier New"", monospace;
  }
  table, tr, td, th {
    border: solid black 2px;
    border-collapse: collapse;
  }
  th {
    text-align: left;
  }
  tr:nth-child(even) {
    background-color: #D6EEEE;
  }
  h1 {
    color: blue;
  }
    </style>
  </head>
  <body>
    <h1>Asset Migration Report</h1>
    <table>
      <thead>
      <tr>
        <th style=""width:10%"">Asset Name</t>
        <th style=""width:5%"">AssetType</th>
        <th style=""width:10%"">LocatorIds</th>
        <th style=""width:7%"">MigrateStatus</th>
        <th style=""width:34%"">OutputHlsUrl</th>
        <th style=""width:34%"">OutputDashUrl</th>
      </tr>
      </thead>
      <tbody>");
        }

        private void WriteHtmlTrailer()
        {
            _htmlWriter.WriteLine(@"
      </tbody>
    </table>
  </body>
</html>");
        }

        private void WriteHtmlRow(ReportRecord record)
        {
            string locatorIds = "";

            if (record.LocatorIds != null)
            {
                foreach (var locId in record.LocatorIds)
                {
                    if (!string.IsNullOrEmpty(locatorIds))
                    {
                        locatorIds += ";\n";
                    }

                    locatorIds += locId;
                }
            }

            _htmlWriter.Write($"<tr><td>{record.AssetName}</td><td>{record.AssetType}</td><td>{locatorIds}</td><td>{record.MigrationStatus}</td><td>");
            if (record.OutputHlsUrl != null)
                _htmlWriter.Write($"<a href=\"{record.OutputHlsUrl}\">{record.OutputHlsUrl}</a>");

            _htmlWriter.Write($"</td><td>");

            if (record.OutputDashUrl != null)
                _htmlWriter.Write($"<a href=\"{record.OutputDashUrl}\">{record.OutputDashUrl}</a>");

            _htmlWriter.Write($"</td>");

            _htmlWriter.WriteLine($"</tr>");
        }
    }
}
