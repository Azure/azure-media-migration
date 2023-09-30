As the default behavior, the program will initially search for an AMS account; if none is located, it will then proceed to seek a storage account for the migration process.

# avaiable commands for migrating assets from Azure blob storage:

1. storage command:
                                                                                        
        AMSMigrate.exe storage -s <subscription> -g <Resource group for the storage account to be migrated> -n <the name of storage account to be migrated> -o <output storage account uri>


3. reset command:
                                                                                           
        AMSMigrate.exe reset -s <subscription> -g <Resource group for the storage account to be migrated> -n <the name of storage account to be migrated> -o <output storage account uri>

4. analyze command:
                                                                                        
        AMSMigrate.exe analyze -s <subscription> -g <Resource group for the storage account to be migrated> -n <the name of storage account to be migrated>

5. AES Encryption:

        AMSMigrate.exe assets -s <subscription> -g <resource group of media service> -n <the name of storage account to be migrated>  -o <output storage account uri> -e --key-vault-uri https://<your azure key vault name>.vault.azure.net
