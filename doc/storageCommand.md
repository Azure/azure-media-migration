# Post AMS shutdown migration

To migrate from storage account, you'll need to use 'storage' command instead of 'assets' command.  An example of the command line is

```
AMSMigrate.exe storage -s <subscription> -g <Resource group for the storage account to be migrated> -n <the name of storage account to be migrated> -o <output storage account uri>
```

AES encryption is also supported with storage command,

```
AMSMigrate.exe storage -s <subscription> -g <resource group of media service> -n <the name of storage account to be migrated> -o <output storage account uri> -e --key-vault-uri https://<your azure key vault name>.vault.azure.net
```

The 'reset' and 'analyze' command works as well with storage account,

e.g.

```
AMSMigrate.exe reset -s <subscription> -g <Resource group for the storage account to be migrated> -n <the name of storage account to be migrated>
```
and

```
AMSMigrate.exe analyze -s <subscription> -g <Resource group for the storage account to be migrated> -n <the name of storage account to be migrated>
```

The 'cleanup' command only works with AMS account, so if you need to do 'cleanup' on a storage container, you'll have to do it manually yourself using Azure storage API / CLI.
