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
        private readonly ILogger _logger;
        private readonly KeyOptions _keyOptions;
        private readonly ISecretUploader _secretUplaoder;
        private readonly TemplateMapper _templateMapper;

        public KeysMigrator(
            GlobalOptions globalOptions,
            KeyOptions keyOptions,
            IAnsiConsole console,
            ILogger<AccountMigrator> logger,
            TemplateMapper templateMapper,
            ICloudProvider cloudProvider,
            TokenCredential credential) : 
            base(globalOptions, console, credential)
        {
            _logger = logger;
            _keyOptions = keyOptions;
            _templateMapper = templateMapper;
            _secretUplaoder = cloudProvider.GetSecretProvider(keyOptions);
        }

        public override async Task MigrateAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Begin migration of keys for account: {name}", _globalOptions.AccountName);
            var account = await GetMediaAccountAsync(cancellationToken);

            var locators = account.GetStreamingLocators().GetAllAsync(_globalOptions.ResourceFilter, cancellationToken: cancellationToken);
            var channel = Channel.CreateBounded<double>(1);
            var progress = ShowProgressAsync("Migrate content keys", "Locators", 1.0, channel.Reader, cancellationToken);
            double count = 0;
            await MigrateInBatches(locators, async locators =>
            {
                var tasks = locators.Select(locator => MigrateLocatorAsync(locator, cancellationToken));
                await Task.WhenAll(tasks);
                count += locators.Length;
                await channel.Writer.WriteAsync(count);
            },
            _keyOptions.BatchSize,
            cancellationToken);
            _logger.LogInformation("Finished migration of keys for account: {name}", _globalOptions.AccountName);
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
                    await _secretUplaoder.UploadAsync(secretName, key.Value, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to migrate asset {name}. Error: {ex}", locator.Data.Name, ex);
            }
        }
    }
}
