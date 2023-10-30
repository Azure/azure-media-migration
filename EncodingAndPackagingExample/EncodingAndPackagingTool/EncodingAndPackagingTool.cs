using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FFMpegCore;
using FFMpegCore.Enums;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EncodingAndPackagingTool;

internal class EncodingAndPackagingTool
{
    private readonly ILogger _logger;
    private readonly TokenCredential _azureCredential;

    public EncodingAndPackagingTool(ILogger<EncodingAndPackagingTool> logger, TokenCredential azureCredential)
    {
        _logger = logger;
        _azureCredential = azureCredential;
    }

    public async Task EncodeAndPackageAsync(Uri mp4BlobUri, Uri outputStorageUri, CancellationToken cancellationToken = default)
    {
        var logger = _logger;
        var azureCredential = _azureCredential;

        // Prepare a tmp path.
        var tmpPath = Path.Combine(Path.GetTempPath(), $"{DateTime.Now.ToString("yyyyMMMdddhhssmm")}-{Guid.NewGuid()}");
        logger.LogInformation($"Create tmp path: {tmpPath}");

        try
        {
            // Create tmp folder.
            Directory.CreateDirectory(tmpPath);

            // Get the blob client.
            var blob = new BlobClient(mp4BlobUri, azureCredential);

            // Get the extension from the blob name.
            var ext = Path.GetExtension(blob.Name);
            if (string.IsNullOrEmpty(ext))
            {
                throw new Exception("Can't get extension from the blob name.");
            }

            // Download the blob to a local tmp folder
            var inputFile = Path.Combine(tmpPath, $"{blob.Name}");
            logger.LogInformation($"Download blob {blob.Name} to {inputFile}");
            using (var inputFileStream = System.IO.File.OpenWrite(inputFile))
            using (var inputStream = await blob.OpenReadAsync(new BlobOpenReadOptions(allowModifications: false), cancellationToken).ConfigureAwait(false))
                await inputStream.CopyToAsync(inputFileStream).ConfigureAwait(false);

            // Run ffprobe to analystics this file.
            var ffprobeAnalyse = await FFProbe.AnalyseAsync(inputFile, ffOptions: null, cancellationToken);

            // Prepare output folder
            var outputDir = Path.Combine(tmpPath, "output");
            Directory.CreateDirectory(outputDir);

            // Prepare ffmpeg command lines.
            var mpdFile = $"{Path.GetFileNameWithoutExtension(inputFile)}.mpd";
            FFMpegArgumentProcessor ffmpegCommand;

            if (ffprobeAnalyse.VideoStreams != null)
            {
                // Generate ffmpeg command.
                ffmpegCommand = FFMpegArguments
                    .FromFileInput(inputFile)
                    .OutputToFile(mpdFile, overwrite: true, args => args
                        .WithCustomArgument("-map 0 -map 0:0 -map 0:0")
                        .WithAudioCodec(AudioCodec.Aac)
                        .WithVideoCodec(VideoCodec.LibX264)
                        .WithCustomArgument("-s:v:0 640x360")
                        .WithCustomArgument("-s:v:1 1280x720")
                        .WithCustomArgument("-s:v:2 1920x1080")
                        .WithCustomArgument("-adaptation_sets \"id=0,streams=v id=1,streams=a\"")
                        .ForceFormat("dash"));
            }
            else
            {
                // For audio only stream.
                ffmpegCommand = FFMpegArguments
                   .FromFileInput(inputFile)
                   .OutputToFile(mpdFile, overwrite: true, args => args
                       .WithAudioCodec(AudioCodec.Aac)
                       .WithCustomArgument("-adaptation_sets \"id=0,streams=a\"")
                       .ForceFormat("dash"));
            }

            // Run ffmpeg.
            var args = ffmpegCommand.Arguments;
            logger.LogInformation($"Run ffmpeg: {args}");
            await ffmpegCommand.Configure(config => config.WorkingDirectory = outputDir).ProcessAsynchronously().ConfigureAwait(false);

            // Get output blob container
            var blobContainer = new BlobContainerClient(outputStorageUri, azureCredential);

            // Upload.
            await Task.WhenAll(Directory.GetFiles(outputDir).Select(async file =>
            {
                var filename = Path.GetFileName(file);
                logger.LogInformation($"Uploading {filename} ...");
                using var stream = File.OpenRead(file);
                await blobContainer.GetBlobClient($"{filename}").UploadAsync(stream, cancellationToken);
                logger.LogInformation($"{filename} is uploaded.");
            }));
        }
        finally
        {
            if (Path.Exists(tmpPath))
            {
                Directory.Delete(tmpPath, recursive: true);
            }
        }
    }
}
