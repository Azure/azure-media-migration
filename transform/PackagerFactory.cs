using AMSMigrate.Contracts;
using Microsoft.Extensions.Logging;

namespace AMSMigrate.Transform
{
    internal class PackagerFactory
    {
        private readonly AssetOptions _options;
        private readonly ILoggerFactory _loggerFactory;
        private readonly TransMuxer _transMuxer;

        public PackagerFactory(ILoggerFactory factory, AssetOptions options, TransMuxer transMuxer) 
        {
            _transMuxer = transMuxer;
            _options = options;
            _loggerFactory = factory;
        }

        public BasePackager GetPackager(AssetDetails details, CancellationToken cancellationToken)
        {
            BasePackager packager;
            if (_options.Packager == Packager.Ffmpeg)
            {
                packager = new FfmpegPackager(details, _transMuxer, _loggerFactory.CreateLogger<FfmpegPackager>());
            }
            else
            {
                packager = new ShakaPackager(details, _transMuxer, _loggerFactory.CreateLogger<ShakaPackager>());
            }
            return packager;
        }
    }
}
