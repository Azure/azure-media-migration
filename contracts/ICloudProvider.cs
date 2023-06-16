
namespace AMSMigrate.Contracts
{
    interface ICloudProvider
    {
        IFileUploader GetStorageProvider(AssetOptions assetOptions);

        ISecretUploader GetSecretProvider(KeyOptions keyOptions);
    }
}
