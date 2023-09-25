using AMSMigrate.Transform;
using System.Text;

namespace AMSMigrate.Ams
{
    sealed class ReportGenerator : IDisposable
    {
        private readonly TextWriter _writer;

        public ReportGenerator(Stream stream)
        {
            _writer = new StreamWriter(stream, Encoding.UTF8);
        }

        public void Dispose()
        {
            _writer.Dispose();
        }

        public void WriteHeader()
        {
            _writer.WriteLine(@"
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

        public void WriteTrailer()
        {
            _writer.WriteLine(@"
      </tbody>
    </table>
  </body>
</html>");
        }

        public void WriteRow(AnalysisResult result)
        {
            lock (this)
            {
                _writer.Write($"<tr><td>{result.AssetName}</td><td>{result.AssetType}</td><td>{result.LocatorIds}</td><td>{result.Status}</td><td>");
                if (result.OutputHlsUrl != null)
                    _writer.Write($"<a href=\"{result.OutputHlsUrl}\">{result.OutputHlsUrl}</a>");

                _writer.Write($"</td><td>");

                if (result.OutputDashUrl != null)
                    _writer.Write($"<a href=\"{result.OutputDashUrl}\">{result.OutputDashUrl}</a>");

                _writer.Write($"</td>");

                _writer.WriteLine($"</tr>");
            }
        }
    }
}
