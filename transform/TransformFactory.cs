using AMSMigrate.Ams;
using AMSMigrate.Contracts;
using Azure.ResourceManager.Media;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AMSMigrate.Transform
{
    internal class TransformFactory
    {
        private readonly IList<StorageTransform> _storageTransforms;
        private readonly List<AssetTransform> _assetTransforms 
            = new List<AssetTransform>();

        public TransformFactory(
            ILoggerFactory loggerFactory,
            AssetOptions options,
            TemplateMapper templateMapper,
            PackagerFactory packagerFactory,
            ICloudProvider cloudProvider)
        {
            _storageTransforms = new List<StorageTransform>();
            var uploader = cloudProvider.GetStorageProvider(options);
            if (options.Packager != Packager.None)
            {
                _storageTransforms.Add(
                    new PackageTransform(
                        options,
                        loggerFactory.CreateLogger<PackageTransform>(),
                        templateMapper,
                        uploader,
                        packagerFactory));
            }
            if (options.CopyNonStreamable || options.Packager == Packager.None)
            {
                _storageTransforms.Add(new UploadTransform(
                    options, uploader, loggerFactory.CreateLogger<UploadTransform>(), templateMapper));
            }


            // There should be atleast one transform.
            Debug.Assert(_storageTransforms.Count > 0, "No transform selected based on the options provided");
            _assetTransforms.AddRange(_storageTransforms.Select(
                t => new AssetTransform(options, templateMapper, t, loggerFactory.CreateLogger<AssetTransform>()))
                );
        }

        public IEnumerable<StorageTransform> StorageTransforms => _storageTransforms;

        public IEnumerable<AssetTransform> AssetTransforms => _assetTransforms;

        public ITransform<MediaTransformResource> TransformTransform => throw new NotImplementedException();
    }
}
