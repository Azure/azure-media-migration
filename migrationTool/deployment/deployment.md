# Deploying the tool to the cloud.

## Create a Resource Group
Create it in the same region as the media services account being migrated.

```bash
az group create --location location --name migration
```

## Update the parameters.
The parameters for the deployment are in the file [parameters.bicepparam](parameters.bicepparam).
```bicep
// The media account being migrated.
param mediaAccountName = 'accountname'
param mediaAccountRG = 'resourcegroup'

// The storage account details where the migrated data is written.
param storageAccountName = 'storeagaccountname'
param storageAccountRG = 'storageresourcegroup'
```

## Deploy the resource.
```bash
az deployment group create --template-file deployment.bicep --resource-group migration --parameters parameters.bicepparam
```

## Monitor progress
While the ACI is running you can monitor the progress of the tool by running.
```bash
az container logs --follow -g migration -n azure-media-migration
```
