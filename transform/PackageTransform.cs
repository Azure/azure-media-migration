using AMSMigrate.Ams;
using AMSMigrate.Contracts;
using AMSMigrate.Pipes;
using Microsoft.Extensions.Logging;

namespace AMSMigrate.Transform
{
    internal class PackageTransform : StorageTransform
    {
        private readonly IFileUploader _fileUploader;
        private readonly PackagerFactory _packagerFactory;

        public PackageTransform(
            AssetOptions options,
            ILogger<PackageTransform> logger,
            TemplateMapper templateMapper,
            IFileUploader uploader,
            PackagerFactory factory)
            : base(options, templateMapper, logger)
        {
            _fileUploader = uploader;
            _packagerFactory = factory;
        }

        // If manifest is present then we can package it.
        public override bool IsSupported(AssetDetails details)
        {
            if (details.Manifest == null)
            {
                return false;
            }
            if (details.ClientManifest != null && details.ClientManifest.HasDiscontinuities())
            {
                if (details is AssetRecord)
                {
                    return true;
                }
                else
                {
                    _logger.LogWarning("Asset {asset} which is a live archive has discontinuities cannot be converted!", details.AssetName);
                    return false;
                }
            }
            if (!details.Manifest.Tracks.All(t => t is TextTrack && 
                (t.IsMultiFile || !t.Source.EndsWith(".vtt") || t.Parameters.Any(p => p.Name == BasePackager.TRANSCRIPT_SOURCE))))
            {
                _logger.LogWarning("Asset {asset} has No VTT text track present. Captions wont be converted.", details.AssetName);
            }

            return true;
        }

        public override async Task<bool> TransformAsync(
            AssetDetails details,
            (string Container, string Prefix) outputPath,
            CancellationToken cancellationToken = default)
        {
            var (assetName, container, manifest, clientManifest) = details;
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            
            // temporary space for either pipes or files.
            var workingDirectory = Path.Combine(_options.WorkingDirectory, assetName);
            Directory.CreateDirectory(workingDirectory);
            var outputDirectory = Path.Combine(workingDirectory, "output");
            Directory.CreateDirectory(outputDirectory);

            // TODO: Dont assume implementation.
            var packager = _packagerFactory.GetPackager(details, cancellationToken);
            try
            {
                var allTasks = new List<Task>();
                var inputFiles = packager.Inputs;
                var inputPaths = inputFiles.Select(f => Path.Combine(workingDirectory, f))
                    .ToArray();

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
                var manifestPaths = manifests.Select(f => Path.Combine(outputDirectory, f)).ToArray();
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
                    uploadPaths.AddRange(manifestPaths);
                }

                _logger.LogTrace("Starting static packaging for asset {name}...", assetName);
                var task = packager.RunAsync(outputDirectory, inputPaths, outputPaths, manifestPaths, cancellationToken);
                allTasks.Add(task);

                await task;

                await Task.WhenAll(allTasks);
                _logger.LogTrace("Packager task finished successfully!");

                // Upload any files pending to be uploaded.
                await Task.WhenAll(
                    uploadPaths.Select(file => UploadFile(file, uploadHelper, cancellationToken)));
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to migrate asset {name} Error:{ex}", container.Name, ex);
                return false;
            }
            finally
            {
                Directory.Delete(workingDirectory, true);
            }

            return true;
        }

        private UploadPipe CreateUpload(string filePath, UploadHelper helper)
        {
            var file = Path.GetFileName(filePath);
            var progress = new Progress<long>(bytes => _logger.LogTrace("Uploaded {bytes} bytes to {name}", bytes, file));
            return new UploadPipe(filePath, helper, _logger, progress);
        }

        private async Task UploadFile(string filePath, UploadHelper uploadHelper, CancellationToken cancellationToken)
        {
            var file = Path.GetFileName(filePath);
            var progress = new Progress<long>(p =>
                _logger.LogTrace("Uploaded {bytes} bytes to {file}", p, file));
            using var content = File.OpenRead(filePath);
            await uploadHelper.UploadAsync(Path.GetFileName(file), content, progress, cancellationToken);
        }
    }
}
