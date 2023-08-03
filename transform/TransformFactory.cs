using AMSMigrate.Ams;
using AMSMigrate.Contracts;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AMSMigrate.Transform
{
    internal class TransformFactory
    {
        private readonly ICloudProvider _cloudProvider;
        private readonly TemplateMapper _templateMapper;
        private readonly ILoggerFactory _loggerFactory;

        public TransformFactory(
            ILoggerFactory loggerFactory,
            TemplateMapper templateMapper,
            ICloudProvider cloudProvider)
        {
            _loggerFactory = loggerFactory;
            _templateMapper = templateMapper;
            _cloudProvider = cloudProvider;            
        }

        public IEnumerable<StorageTransform> GetTransforms(GlobalOptions globalOptions, MigratorOptions options)
        {
            var packagerFactory = new PackagerFactory(_loggerFactory, options);
            var uploader = GetUploader(options);
            var transformCount  = 0;
            if (options.Packager != Packager.None)
            {
                ++transformCount;
                yield return new PackageTransform(
                        globalOptions,
                        options,
                        _loggerFactory.CreateLogger<PackageTransform>(),
                        _templateMapper,
                        _cloudProvider,
                        packagerFactory);
            }
            
            if (options.CopyNonStreamable || options.Packager == Packager.None)
            {
                ++transformCount;
                yield return new UploadTransform(
                    globalOptions, options, uploader, _loggerFactory.CreateLogger<UploadTransform>(), _templateMapper);
            }

            // There should be at least one transform.
            Debug.Assert(transformCount > 0, "No transform selected based on the options provided");
        }

        public IEnumerable<AssetTransform> GetTransforms(GlobalOptions globalOptions, AssetOptions assetOptions)
        {
            return GetTransforms(globalOptions, assetOptions as MigratorOptions)
                .Select(t => new AssetTransform(assetOptions, _templateMapper, t, _loggerFactory.CreateLogger<AssetTransform>()));
        }

        public IFileUploader GetUploader(MigratorOptions options) => _cloudProvider.GetStorageProvider(options);

        public TemplateMapper TemplateMapper => _templateMapper;
    }
}
