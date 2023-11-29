using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EncodingAndPackagingTool.Test;

public class EncodingAndPackagingTool
{
    private static string _storageServiceUri;
    private static string _inputContainerUri;
    private static string _testDataPath;
    private static DefaultAzureCredential _azureCrendentail;

    static EncodingAndPackagingTool()
    {
        _storageServiceUri = "https://127.0.0.1:10000/devstoreaccount1";
        _inputContainerUri = $"{_storageServiceUri}/encodingandpackagingtooltest";
        _testDataPath = Environment.GetEnvironmentVariable("TEST_DATA") ?? throw new Exception("TEST_DATA environment variable is missing.");
        _azureCrendentail = new DefaultAzureCredential();

        // Upload test video clip.
        var container = new BlobContainerClient(new Uri(_inputContainerUri), _azureCrendentail);
        container.CreateIfNotExists();

        Task.WhenAll(Directory.GetFiles(_testDataPath).Select(async file =>
        {
            var blob = container.GetBlockBlobClient($"input/{Path.GetFileName(file)}");
            using var stream = System.IO.File.OpenRead(file);
            await container.GetBlobClient($"input/{Path.GetFileName(file)}").UploadAsync(stream, overwrite: true);
        })).Wait();
    }

    [Fact]
    public async Task EncodingAndPackagingToolTest()
    {
        var inputUri = $"{_inputContainerUri}/input/bunny.640x480.15fps.mp4";
        var outputContainerUri = $"{_storageServiceUri}/output{Guid.NewGuid().ToString("n")}";

        // Invoke EncodingAndPackagingTool
        var psi = new ProcessStartInfo()
        {
            FileName = "dotnet",
            Arguments = $"EncodingAndPackagingTool.dll --input {inputUri} --output {outputContainerUri}"
        };
        var process = Process.Start(psi) ?? throw new Exception("Start process failed.");
        await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);

        // We should have the output mpd file.
        var blob = new BlobClient(new Uri($"{outputContainerUri}/bunny.640x480.15fps.mpd"), _azureCrendentail);
        using (var stream = await blob.OpenReadAsync())
        {
            Assert.True(stream.Length > 2000);
        }

        // We should have 13 chunk files for stream 0
        for (var i = 1; i <= 13; ++i)
        {
            blob = new BlobClient(new Uri($"{outputContainerUri}/chunk-stream0-{i.ToString("00000")}.m4s"), _azureCrendentail);
            using (var stream = await blob.OpenReadAsync())
            {
                Assert.True(stream.Length > 2000);
            }
        }

        // We should have 24 chunk files for stream 1
        for (var i = 1; i <= 24; ++i)
        {
            blob = new BlobClient(new Uri($"{outputContainerUri}/chunk-stream1-{i.ToString("00000")}.m4s"), _azureCrendentail);
            using (var stream = await blob.OpenReadAsync())
            {
                Assert.True(stream.Length > 2000);
            }
        }

        // We should have 13 chunk files for stream 2
        for (var i = 1; i <= 13; ++i)
        {
            blob = new BlobClient(new Uri($"{outputContainerUri}/chunk-stream2-{i.ToString("00000")}.m4s"), _azureCrendentail);
            using (var stream = await blob.OpenReadAsync())
            {
                Assert.True(stream.Length > 2000);
            }
        }

        // We should have 13 chunk files for stream 3
        for (var i = 1; i <= 13; ++i)
        {
            blob = new BlobClient(new Uri($"{outputContainerUri}/chunk-stream3-{i.ToString("00000")}.m4s"), _azureCrendentail);
            using (var stream = await blob.OpenReadAsync())
            {
                Assert.True(stream.Length > 2000);
            }
        }

        // We should have 4 init chunk files.
        for (var i = 0; i < 4; ++i)
        {
            blob = new BlobClient(new Uri($"{outputContainerUri}/init-stream{i}.m4s"), _azureCrendentail);
            using (var stream = await blob.OpenReadAsync())
            {
                Assert.True(stream.Length > 500);
            }
        }

        // Delete the container if success.
        var container = new BlobContainerClient(new Uri(outputContainerUri), _azureCrendentail);
        await container.DeleteAsync();
    }
}
