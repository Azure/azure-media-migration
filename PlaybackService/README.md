Azure Media Services Playback Proxy & ClearKey Delivery Server
==

## Overview

This server is an addon service to [Azure Media Services Migration Tool](https://github.com/Azure/azure-media-migration).  Azure Media Services Migration tool supports statically encrypting (ClearKey DRM) the content while packaging and upload to an azure blob storage container that has no public access permissions by default.  In order for user to be able to view the encrypted content, we provide this sample server that demonstrates the following tasks needed for playback:
- Authorize the request for media fragments from players.
- Read the encrypted media fragments out from the private storage account, and return back to players.
- Respond to the DRM key requests from players.

## Features

* Supports ClearKey DRM for both DASH and HLS format.
* Supports token based customized authorization.
* Supports Azure keyVault service (for ClearKey DRM storage).
* Supports Azure storage service (for DASH/HLS manifests and encrypted media fragments).

## Identity and Permissions

The tool uses Azure Identity library for authentication. See [here](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme?view=azure-dotnet) for various ways to authenticate and the settings needed.

You'll need to have the following permissions:

* The identity must have ['Storage Blob Data Reader'](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#storage-blob-data-reader) role for the azure storage account where the media data stored by the [Azure Media Services Migration Tool](https://github.com/Azure/azure-media-migration).
* The identity must have ['Key Vault Secrets User'](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#key-vault-secrets-user) role for the azure key vault account where the ClearKeys stored by the [Azure Media Services Migration Tool](https://github.com/Azure/azure-media-migration).

_NOTE: If the storage account or key vault account doesn't use Azure RBAC, then you will have to create access policies and grant the read permissions on them._

## Quick Start

### Customize the authorization

To customize the authorization, you can modify the file `src/Middlewares/CustomAuthenticationHandler.cs` and add your own token validation logic inside.

**NOTE: You must add a `token` claim in the final `ClaimsIdentity`. This token will be injected into the DASH/HLS manifests returned to the player. It will be used to authorize the key requests sent by client later.**

### Build project

To build the project, you can run the following command:

    dotnet build -c Release
    dotnet publish -c Release

You can deploy the binaries in your publish folder (e.g. src\bin\Release\net7.0\publish) by yourself or build a docker image:

    docker build -t ams-migration-tool-playback-service:latest src/

### Deploy and run

If you deploy the binaries by yourself, you might need modify some settings in `appsettings.json` file. The most important settings is `AzureKeyVaultAccountName` which is the key vault account name used by [Azure Media Services Migration Tool](https://github.com/Azure/azure-media-migration) to store the ClearKeys.

If you build a docker image, you can set `AzureKeyVaultAccountName` from the environment variable, e.g:

    docker run -e PlaybackService__AzureKeyVaultAccountName=<your_azure_key_vault_account_name> -p 80:80 ams-migration-tool-playback-service:latest

If you want to deploy the service in your kubernetes cluster, you can take a look at the configuration file `deployment/playbackservices.yaml` as an example.

## Request URI pattern

This service uses the following URI pattern to retrieve DASH/HLS manifests and media fragments from azure storage account:

    https://xxx.xxx.xxxx/<azure-storage-account-name>/<path-to-manifests-or-media-fragments-file>

For example, if you have a dash manifest in an azure storage account called `'amsmigrate'`, and its path is `'ams-migration-output/encrypted/dash.mpd'`, then the URI used for this proxy service will be: `'https://xxx.xxx.xxxx/amsmigrate/ams-migration-output/encrypted/dash.mpd'`.

If you use custom domain for your azure storage account, the `'<azure-storage-account-name>'` can be your custom domain also, e.g: `'https://xxx.xxx.xxxx/amsmigrate.storage.custom.domain/ams-migration-output/encrypted/dash.mpd'`.
