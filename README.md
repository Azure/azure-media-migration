# Azure Media Services Migration Tool

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft
trademarks or logos is subject to and must follow
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.

## Reporting Issues

If you encounter any issues or bugs that you'd like to report, please feel free to open an issue.  For bugs, it would be extremely helpful if you attach the output log file from the tool.

## Overview
A command line tool to migrate your data from Azure Media Services.
This tool helps you to migrate your media data from Azure Media Services (AMS).
It can be packaged to be streamed directly from Azure storage without any service.

The tool uses [shaka-packager](https://github.com/shaka-project/shaka-packager) to convert the videos to directly streamable format.
The content is converted to CMAF format with both a DASH and HLS manifest to support a wide range of devices.

## Features
* Cross-Platform. Works on all platforms where .NET core is available.  Tested platforms are Windows and Linux.
* Simple command line interface. Intuitive and easy to use.
* Support for packaging VOD assets.
* Support for copying non-streamable assets.
* Marks migrated assets and provides HTML summary on analyze

## Open Issues
* Live assets are not supported but will be in a future version of this tool.
* Storage encrypted VOD contents are not supported but will be in a future version of this tool.
* Direct migration from an Azure Storage account without using the AMS API is not supported but will be in a future version of this tool.

# Types of Migration
The tool supports various types of migration depending on the asset format and the command line options.
* For non-streamable assets, It can simply upload the files to the new storage account.
* For VOD assets, it can convert the assets to CMAF files with a DASH and HLS manifest and upload the files to the new storage account.

# Temporary storage needed
The tool uses temporary storage space for format conversion. Aside from testing purposes, for performance reasons we recommend running the tool in the same Azure region as your AMS account, for example in an Azure VM.
- The assets are downloaded from source container to local disk.
- Shaka packager is invoked to generate the statically packaged files and write it to local disk.

# Identity and Permissions
The tools uses Azure Identity library for authentication.
See [here](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme?view=azure-dotnet) for various ways to authenticate and the settings needed.

You'll need to have the following permissions:

- The identity used to migrate must have 'Contributor' role on the Azure Media Services account being migrated.
- The identity that runs this migration tool should be added to the ['Storage Blob Data Contributor'](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#storage-blob-data-contributor) role for the source and destination storage accounts.

# Quick Start

## Build project

To build the project, you can run the following command

    dotnet build
    dotnet publish

the output binary will be in your publish folder (e.g. bin\Debug\net6.0\publish\AMSMigrate.exe for windows, 
                                                       bin\Debug\net6.0\publish\AMSMigrate for Linux)

There are two main commands: analyze and assets.  You can view the help by running the following three commands

    AMSMigrate.exe -h
    AMSMigrate.exe analyze -h
    AMSMigrate.exe assets -h

(Run ./AMSMigrate or full path in Linux, don't add .exe extension.)

## Migrate

Migration will do the following:
- Copy non-streamable assets from AMS account to an output storage account.
- Package streamable assets (currently only a subset of streamable assets, see [Open Issues](#Open-Issues)) as CMAF + HLS, DASH manifests and copy to output storage account.

After migration, the container metadata for the source containers will be marked with migration status, output url, etc.

To migrate, you will use the assets command, an example is

    AMSMigrate.exe assets -s <subscription> -g <resource group of media service> -n <media service account name> -o <output storage account uri>

You can look at the help for assets command to further customization.

## Analyze

Analyze will do the following:
- Go through all the assets and try to classify it by type (e.g. streamable, etc)
- Go through all the assets and read the migration status to generate an html report

Typically, you can run analyze before and after you run the migrate command.
- Running before to get an idea of what type of assets are in your media service account.
- Running after to get a report of migration status.

To analyze, you will use the analyze command, an example is

    AMSMigrate.exe analyze -s <subscription> -g <resource group of media service> -n <media service account name>

The html file and the log file location will be reported at the end of execution.

You can look at the help for analyze command to further customization.
