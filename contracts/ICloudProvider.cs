
namespace AMSMigrate.Contracts
{
    interface ICloudProvider
    {
        IFileUploader GetStorageProvider(MigratorOptions migratorOptions);

        ISecretUploader GetSecretProvider(KeyOptions keyOptions);
    }
}
