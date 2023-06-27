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
    font-family: ""Courier New"", monspaced;
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
        <th style=""width:30%"">Asset Name</t>
        <th style=""width:10%"">Migration Status</th>
        <th style=""width:60%"">Migration URL</th>
      </tr>
      </thead>
      <tbody>");
        }

        private void WriteTrailer()
        {
            _writer.WriteLine(@"
      </tbody>
    </table>
  </body>
</html>");
        }

        public void WriteRows(AnalysisResult[] results)
        {
            foreach (var result in results)
            {
                _writer.Write($"<tr><td>{result.AssetName}</td><td>{result.Status}</td><td>");
                if (result.OutputPath != null)
                    _writer.Write($"<a href=\"{result.OutputPath}\">{result.OutputPath}</a>");
                _writer.WriteLine($"</td></tr>");
            }
        }
    }
}
