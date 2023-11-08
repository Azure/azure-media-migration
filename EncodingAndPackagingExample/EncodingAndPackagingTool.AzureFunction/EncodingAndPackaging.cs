using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace EncodingAndPackagingTool
{
    public class EncodingAndPackaging
    {
        private readonly ILogger _logger;
        private readonly EncodingAndPackagingTool _encodingAndPackagingTool;

        public EncodingAndPackaging(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<EncodingAndPackaging>();
            _encodingAndPackagingTool = new EncodingAndPackagingTool(loggerFactory.CreateLogger<EncodingAndPackagingTool>(), new DefaultAzureCredential());
        }


        [Function("EncodingAndPackaging")]
        public async Task Run([QueueTrigger("encodingandpackagingtasks", Connection = "")] string myQueueItem)
        {
            var message = JsonSerializer.Deserialize<Message>(myQueueItem);
            if (message == null || message.Mp4BlobUri == null || message.OutputStorageUri == null)
            {
                _logger.LogError("Invalid message.");
                return;
            }

            try
            {
                await _encodingAndPackagingTool.EncodeAndPackageAsync(message.Mp4BlobUri, message.OutputStorageUri);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EncodeAndPackage failed.");
            }
        }

        public class Message
        {
            [JsonPropertyName("mp4_blob_uri")]
            public Uri? Mp4BlobUri { get; set; }

            [JsonPropertyName("output_storage_uri")]
            public Uri? OutputStorageUri { get; set; }
        }
    }
}
