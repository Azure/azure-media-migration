﻿using AMSMigrate.Contracts;
using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;

namespace AMSMigrate.Azure
{
    internal class KeyVaultUploader : ISecretUploader
    {
        private readonly ILogger _logger;
        private readonly SecretClient _secretClient;
        private readonly KeyVaultOptions _options;

        public KeyVaultUploader(
            KeyVaultOptions options,
            TokenCredential credential,
            ILogger<KeyVaultUploader> logger)
        {
            _options = options;
            _logger = logger;
            _secretClient = new SecretClient(options.KeyVaultUri, credential);
        }

        public async Task UploadAsync(string secretName, string secretValue, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Saving secret {name} to key vault {vault}", secretName, _options.KeyVaultUri);
            await _secretClient.SetSecretAsync(secretName, secretValue, cancellationToken);
        }
    }
}
