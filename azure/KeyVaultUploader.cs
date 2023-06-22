using AMSMigrate.Contracts;
using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;

namespace AMSMigrate.Azure
{
    internal class KeyVaultUploader : ISecretUploader
    {
        private readonly ILogger _logger;
        private readonly SecretClient _secretClient;
        private readonly KeyOptions _keyOptions;

        public KeyVaultUploader(
            KeyOptions options,
            TokenCredential credential,
            ILogger<KeyVaultUploader> logger)
        {
            _keyOptions = options;
            _logger = logger;
            _secretClient = new SecretClient(options.KeyVaultUri, credential);
        }

        public async Task UploadAsync(string secretName, string secretValue, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Saving secret {name} to key valut {vault}", secretName, _keyOptions.KeyVaultUri);
            await _secretClient.SetSecretAsync(secretName, secretValue, cancellationToken);
        }
    }
}
