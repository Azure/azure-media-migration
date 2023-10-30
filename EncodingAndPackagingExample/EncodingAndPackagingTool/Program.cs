using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Azure.Identity;

namespace EncodingAndPackagingTool;

public class Program
{
    /// <summary>
    /// Azure Media Services Encoding and Packaging tool
    /// </summary>
    /// <param name="input">The azure blob uri for the input mp4 file</param>
    /// <param name="output">The azure blob container uri for the output files</param>
    /// <param name="loglevel">The log level for logging</param>
    public static async Task<int> Main(
        Uri? input,
        Uri? output,
        LogLevel loglevel = LogLevel.Information)
    {
        if (input == null)
        {
            Console.Error.WriteLine("--input is missing.");
            return -1;
        }

        if (output == null)
        {
            Console.Error.WriteLine("--output is missing.");
            return -1;
        }

        var logger = LoggerFactory.Create(builder => builder.AddConsole())
                        .CreateLogger<EncodingAndPackagingTool>();

        await new EncodingAndPackagingTool(logger, new DefaultAzureCredential())
            .EncodeAndPackageAsync(input, output);

        return 0;
    }
}
