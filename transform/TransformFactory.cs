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

        private readonly IFileUploader _uploader;
        private readonly TemplateMapper _templateMapper;

        public TransformFactory(
            ILoggerFactory loggerFactory,
            TOption options,
            TemplateMapper templateMapper,
            ICloudProvider cloudProvider)
        {
            _storageTransforms = new List<StorageTransform>();
            
            if (options is MigratorOptions migratorOption)
            {
                _uploader = cloudProvider.GetStorageProvider(migratorOption);
                _templateMapper = templateMapper;
                
                var packagerFactory = new PackagerFactory(loggerFactory, migratorOption);

                if (migratorOption.Packager != Packager.None)
                {
                    _storageTransforms.Add(
                        new PackageTransform(
                            migratorOption,
                            loggerFactory.CreateLogger<PackageTransform>(),
                            templateMapper,
                            _uploader,
                            packagerFactory));
                }
                if (migratorOption.CopyNonStreamable || migratorOption.Packager == Packager.None)
                {
                    _storageTransforms.Add(new UploadTransform(
                        migratorOption, _uploader, loggerFactory.CreateLogger<UploadTransform>(), templateMapper));
                }
            }
            else
            {
                throw new ArgumentException("Parameter 'options' must be for MigratorOptions.");
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

        public IFileUploader Uploader => _uploader;

        public TemplateMapper TemplateMapper => _templateMapper;

        public ITransform<MediaTransformResource> TransformTransform => throw new NotImplementedException();
    }
}
