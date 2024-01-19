using AMSMigrate.Contracts;
using Azure.Core;
using Azure.ResourceManager.Media;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.Threading.Channels;

namespace AMSMigrate.Ams
{
    internal class KeysMigrator : BaseMigrator
    {
        private readonly KeyOptions _keyOptions;
        private readonly ISecretUploader _secretUploader;
        private readonly TemplateMapper _templateMapper;

        public KeysMigrator(
            GlobalOptions globalOptions,
            KeyOptions keyOptions,
            IAnsiConsole console,
            ILogger<KeysMigrator> logger,
            TemplateMapper templateMapper,
            ICloudProvider cloudProvider,
            TokenCredential credential) :
            base(globalOptions, console, credential, logger)
        {
            _keyOptions = keyOptions;
            _templateMapper = templateMapper;
            _secretUploader = cloudProvider.GetSecretProvider(keyOptions);
        }

        public override async Task MigrateAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Begin migration of keys for account: {name}", _keyOptions.AccountName);
            var (isAMSAcc, account) = await IsAMSAccountAsync(_keyOptions.AccountName, cancellationToken);
            if (!isAMSAcc || account == null)
            {
                _logger.LogInformation("No valid media account was found.");
                throw new Exception("No valid media account was found.");
            }

            var locators = account.GetStreamingLocators().GetAllAsync(_keyOptions.ResourceFilter, cancellationToken: cancellationToken);
            var channel = Channel.CreateBounded<double>(1);
            var progress = ShowProgressAsync("Migrate content keys", "Locators", 1.0, channel.Reader, cancellationToken);
            int count = 0;
            await MigrateInParallel(locators, null, async (locator, cancellationToken) =>
            {
                await MigrateLocatorAsync(locator, cancellationToken);
                Interlocked.Increment(ref count);
                await channel.Writer.WriteAsync(count, cancellationToken);
            },
            _keyOptions.BatchSize,
            cancellationToken);

            _logger.LogInformation("Finished migration of keys for account: {name}", _keyOptions.AccountName);
            channel.Writer.Complete();
            await progress;
        }

        private async Task MigrateLocatorAsync(StreamingLocatorResource locator, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Try migrating keys for locator: {locator} = {id}", locator.Data.Name, locator.Data.StreamingLocatorId);
            try
            {
                var keys = locator.GetContentKeysAsync(cancellationToken: cancellationToken);
                await foreach (var key in keys)
                {
                    _logger.LogInformation("Migrating content key {id}", key.Id);
                    var secretName = _templateMapper.ExpandKeyTemplate(key, _keyOptions.KeyTemplate);
                    await _secretUploader.UploadAsync(secretName, key.Value, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to migrate asset {name}. Error: {ex}", locator.Data.Name, ex);
            }
        }
    }
}
