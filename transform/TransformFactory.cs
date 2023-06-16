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
        private readonly List<ITransform<AssetRecord>> _assetTransforms = new List<ITransform<AssetRecord>>();

        public TransformFactory(
            ILoggerFactory loggerFactory,
            AssetOptions options,
            TemplateMapper templateMapper,
            PackagerFactory packagerFactory,
            IFileUploader uploader)
        {
            _storageTransforms = new List<StorageTransform>();

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
                .Cast<ITransform<AssetRecord>>());
        }

        public IEnumerable<ITransform<AssetDetails>> StorageTransforms => _storageTransforms;

        public IEnumerable<ITransform<AssetRecord>> AssetTransforms => _assetTransforms;

        public ITransform<MediaTransformResource> TransformTransform => throw new NotImplementedException();
    }
}
