using AMSMigrate.Contracts;
using Microsoft.Extensions.Logging;

namespace AMSMigrate.Transform
{
    internal class PackagerFactory
    {
        private readonly MigratorOptions _options;
        private readonly ILoggerFactory _loggerFactory;
        private readonly TransMuxer _transMuxer;

        public PackagerFactory(ILoggerFactory factory, MigratorOptions options) 
        {          
            _loggerFactory = factory;
            _options = options;
            _transMuxer = new TransMuxer(options, _loggerFactory.CreateLogger<TransMuxer>());
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
