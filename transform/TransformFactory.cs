using AMSMigrate.Ams;
using AMSMigrate.Contracts;
using Azure.ResourceManager.Media;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AMSMigrate.Transform
{
    internal class TransformFactory<TOption>
    {
        private readonly IList<StorageTransform> _storageTransforms;
        private readonly List<AssetTransform> _assetTransforms 
            = new List<AssetTransform>();

        public TransformFactory(
            ILoggerFactory loggerFactory,
            TOption options,
            TemplateMapper templateMapper,
            ICloudProvider cloudProvider)
        {
            _storageTransforms = new List<StorageTransform>();

            if (options is MigratorOptions migratorOption)
            {
                var uploader = cloudProvider.GetStorageProvider(migratorOption);
                var packagerFactory = new PackagerFactory(loggerFactory, migratorOption);

                if (migratorOption.Packager != Packager.None)
                {
                    _storageTransforms.Add(
                        new PackageTransform<TOption>(
                            migratorOption,
                            loggerFactory.CreateLogger<PackageTransform<TOption>>(),
                            templateMapper,
                            uploader,
                            packagerFactory));
                }
                if (migratorOption.CopyNonStreamable || migratorOption.Packager == Packager.None)
                {
                    _storageTransforms.Add(new UploadTransform(
                        migratorOption, uploader, loggerFactory.CreateLogger<UploadTransform>(), templateMapper));
                }
            }
            else
            {
                throw new ArgumentException("Paramter 'options' must be for MigratorOptions.");
            }

            // There should be at least one transform.
            Debug.Assert(_storageTransforms.Count > 0, "No transform selected based on the options provided");

            if (options is AssetOptions assetOptions)
            {
                _assetTransforms.AddRange(_storageTransforms.Select(
                    t => new AssetTransform(assetOptions, templateMapper, t, loggerFactory.CreateLogger<AssetTransform>()))
                    );
            }
        }

        public IEnumerable<StorageTransform> StorageTransforms => _storageTransforms;

        public IEnumerable<AssetTransform> AssetTransforms => _assetTransforms;

        public ITransform<MediaTransformResource> TransformTransform => throw new NotImplementedException();
    }
}
