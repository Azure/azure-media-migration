﻿using AMSMigrate.Contracts;
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
        private readonly ILogger _logger;

        public AccountMigrator(
            GlobalOptions options,
            IAnsiConsole console,
            ILogger<AccountMigrator> logger,
            TokenCredential credential) : 
            base(options, console, credential)
        {
            _logger = logger;
        }

        public override async Task MigrateAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Begin migration for media account: {name}", _globalOptions.AccountName);
            var account = await _resourceProvider.GetMediaAccountAsync(cancellationToken);
            //TODO: Migrate everything like assets, keys, transforms etc.
            _logger.LogInformation("Finished migration for account {name}", _globalOptions.AccountName);
        }
    }
}
