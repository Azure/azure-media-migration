using AMSMigrate.Ams;
using AMSMigrate.Contracts;
using AMSMigrate.Decryption;
using AMSMigrate.Pipes;
using Microsoft.Extensions.Logging;

namespace AMSMigrate.Transform
{
    internal class PackageTransform : StorageTransform
    {
        private readonly PackagerFactory _packagerFactory;
        private readonly ISecretUploader? _secretUploader = default;

        public PackageTransform(
            GlobalOptions globalOptions,
            MigratorOptions options,
            ILogger<PackageTransform> logger,
            TemplateMapper templateMapper,
            ICloudProvider cloudProvider,
            PackagerFactory factory)
            : base(globalOptions, options, templateMapper, cloudProvider.GetStorageProvider(options), logger)
        {
            if (options.EncryptContent)
            {
                var vaultOptions = new KeyVaultOptions(options.KeyVaultUri!);
                _secretUploader = cloudProvider.GetSecretProvider(vaultOptions);
            }
            _packagerFactory = factory;
        }

        // If manifest is present then we can package it.
        protected override bool IsSupported(AssetDetails details)
        {
            if (details.Manifest == null)
            {
                return false;
            }
            return details.Manifest.Format.StartsWith("mp4")
                || details.Manifest.Format.Equals("fmp4")
                || (details.Manifest.Format == "vod-fmp4");
        }

        static string EscapeName(string name)
        {
            char[] specialCharacters = { ':', '/', '\\', '>', '<', '|', '&' };
            foreach (var c in specialCharacters)
            {
                name = name.Replace(c, '_');
            }
            return name;
        }


        protected override async Task<string> TransformAsync(
            AssetDetails details,
            (string Container, string Prefix) outputPath,
            CancellationToken cancellationToken = default)
        {
            var (assetName, container, manifest, clientManifest, outputManifest, decryptor) = details;
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));

            // create a linked source which when disposed cancels all tasks.
            using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cancellationToken = source.Token;

            if (_options.EncryptContent)
            {
                details.GetEncryptionDetails(_options, _templateMapper);
            }


            // temporary space for either pipes or files.
            var folderName = EscapeName(assetName);
            var workingDirectory = Path.Combine(_options.WorkingDirectory, folderName);
            Directory.CreateDirectory(workingDirectory);
            var outputDirectory = Path.Combine(workingDirectory, "output");
            Directory.CreateDirectory(outputDirectory);

            // TODO: Don't assume implementation.
            var packager = _packagerFactory.GetPackager(details, cancellationToken);
            try
            {
                var allTasks = new List<Task>();
                var inputFiles = packager.Inputs;
                var inputPaths = inputFiles.Select(f => Path.Combine(workingDirectory, f))
                    .ToArray();

                // Anything not packaged and can be uploaded is uploaded directly.
                var blobs = await container.GetListOfBlobsRemainingAsync(manifest, cancellationToken);
                allTasks.Add(Task.WhenAll(blobs.Select(async blob =>
                {
                    using var aesTransform = AssetDecryptor.GetAesCtrTransform(details.DecryptInfo, blob.Name, false);
                    await UploadBlobAsync(blob, aesTransform, outputPath, cancellationToken);
                })));

                if (packager.UsePipeForInput)
                {
                    var inputPipes = packager.GetInputPipes(workingDirectory);
                    inputPaths = inputPipes.Select(p => p.PipePath).ToArray();
                    _logger.LogTrace("Created pipes to download blobs {pipes}", string.Join(",", inputPaths));
                    var download = Task.WhenAll(inputPipes.Select(async p =>
                    {
                        using (p)
                            await p.RunAsync(cancellationToken);
                    }));
                    allTasks.Add(download);
                }
                else
                {
                    await packager.DownloadInputsAsync(workingDirectory, cancellationToken);
                }

                // Adjust the package files after the input files are all downloaded.
                packager.AdjustPackageFiles(workingDirectory);

                var outputFiles = packager.Outputs;
                var (outputContainerName, prefix) = outputPath;
                var uploadHelper = new UploadHelper(outputContainerName, prefix, _fileUploader);
                var uploadPaths = new List<string>();
                var outputPaths = outputFiles.Select(f => Path.Combine(outputDirectory, f)).ToArray();
                if (packager.UsePipeForOutput)
                {
                    var outputPipes = outputPaths.Select(file => CreateUpload(file, uploadHelper))
                        .ToList();
                    outputPaths = outputPipes.Select(p => p.PipePath).ToArray();
                    _logger.LogTrace("Created pipes to upload to blobs {pipes}", string.Join(",", outputPaths));
                    var uploads = outputPipes.Select(async p =>
                    {
                        using (p)
                        {
                            await p.RunAsync(cancellationToken);
                        }
                    });
                    allTasks.AddRange(uploads);
                }
                else
                {
                    uploadPaths.AddRange(outputPaths);
                }

                var manifests = packager.Manifests;
                var manifestPaths = manifests.Select(f => string.IsNullOrEmpty(f) ? "" : Path.Combine(outputDirectory, f)).ToArray();
                if (packager.UsePipeForManifests)
                {
                    var manifestPipes = manifestPaths.Select(file => CreateUpload(file, uploadHelper)).ToList();
                    manifestPaths = manifestPipes.Select(p => p.PipePath).ToArray();
                    allTasks.AddRange(manifestPipes.Select(async p =>
                    {
                        using (p)
                            await p.RunAsync(cancellationToken);
                    }));
                }
                else
                {
                    var manifestsForUpload = manifestPaths.Where(f => f != "");

                    uploadPaths.AddRange(manifestsForUpload);
                }

                _logger.LogTrace("Starting static packaging for asset {name}...", assetName);
                var task = packager.RunAsync(outputDirectory, inputPaths, outputPaths, manifestPaths, cancellationToken);
                allTasks.Add(task);

                while (allTasks.Count > 0)
                {
                    var currentTask = await Task.WhenAny(allTasks);
                    // throw if any tasks fails.
                    await currentTask;
                    allTasks.Remove(currentTask);
                }

                _logger.LogTrace("Packager task finished successfully!");
                // Upload any files pending to be uploaded.
                await Task.WhenAll(
                    uploadPaths.Select(file => UploadFile(file, uploadHelper, cancellationToken)));

                if (_options.EncryptContent)
                {
                    _logger.LogDebug("Saving key with id id: {keyId} for asset: {name} to key vault {vault}", details.KeyId, details.AssetName, _options.KeyVaultUri); ;
                    await _secretUploader!.UploadAsync(details.KeyId, details.EncryptionKey, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to migrate asset {name} Error:{ex}", assetName, ex);
                throw;
            }
            finally
            {
                if (!_options.KeepWorkingFolder)
                {
                    Directory.Delete(workingDirectory, true);
                }
            }

            // Mark the output container appropriately so that it won't be used as an input asset in new run.
            await UpdateOutputStatus(outputPath.Container, cancellationToken);

            return outputPath.Prefix;
        }

        private UploadPipe CreateUpload(string filePath, UploadHelper helper)
        {
            var file = Path.GetFileName(filePath);
            var progress = new Progress<long>(bytes => _logger.LogTrace("Uploaded {bytes} bytes to {name}", bytes, file));
            return new UploadPipe(filePath, helper, _logger, GetHeaders(file), progress);
        }

        private async Task UploadFile(string filePath, UploadHelper uploadHelper, CancellationToken cancellationToken)
        {
            var file = Path.GetFileName(filePath);
            // Report update for every 1MB.
            long update = 0;
            var progress = new Progress<long>(p =>
            {
                if (p >= update)
                {
                    lock (this)
                    {
                        if (p >= update)
                        {
                            _logger.LogTrace("Uploaded {byte} bytes to {file}", p, file);
                            update += 1024 * 1024;
                        }
                    }
                }
            });

            using var content = File.OpenRead(filePath);
            await uploadHelper.UploadAsync(Path.GetFileName(file), content, GetHeaders(file), progress, cancellationToken);
        }
    }
}
