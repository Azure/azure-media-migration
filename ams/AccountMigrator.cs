using AMSMigrate.Contracts;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace AMSMigrate.Ams
{
    /// <summary>
    /// A dummy account level migrator. Does nothing at this point.
    /// </summary>
    internal class AccountMigrator : BaseMigrator
    {
        private string _accountName;

        public AccountMigrator(
            GlobalOptions options,
            string accountName,
            IAnsiConsole console,
            ILogger<AccountMigrator> logger,
            TokenCredential credential) : 
            base(options, console, credential, logger)
        {
            _accountName = accountName;
        }

        public override async Task MigrateAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Begin migration for media account: {name}", _accountName);
            var account = await _resourceProvider.GetMediaAccountAsync(_accountName, cancellationToken);
            //TODO: Migrate everything like assets, keys, transforms etc.
            _logger.LogInformation("Finished migration for account {name}", _accountName);
        }
    }
}
