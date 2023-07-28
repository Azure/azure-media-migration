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
        <th style=""width:20%"">Asset Name</t>
        <th style=""width:6%"">AssetType</th>
        <th style=""width:8%"">MigrateStatus</th>
        <th style=""width:56%"">OutputPath</th>
        <th style=""width:10%"">ManifestName</th>
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
            lock(this)
            {
                _writer.Write($"<tr><td>{result.AssetName}</td><td>{result.AssetType}</td><td>{result.Status}</td><td>");
                if (result.OutputPath != null)
                    _writer.Write($"<a href=\"{result.OutputPath}\">{result.OutputPath}</a>");

                _writer.Write($"</td><td>{result.ManifestName}</td>");

                _writer.WriteLine($"</tr>");
            }
        }
    }
}
