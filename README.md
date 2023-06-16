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

## Overview
A command line tool to migrate your data from Azure Media Services.
This tool helps you to migrate your media data from Azure Media Services (AMS). 
It can be packaged to be streamed directly from Azure storage without any service.

The tool supports both [ffmpeg](https://ffmpeg.org/) and [shaka-packager](https://github.com/shaka-project/shaka-packager) to conver the videos to directly streamable format.
The content is converted to CMAF format with both a DASH and HLS manifest to support a wide range of devices.
The default is shaka packager because it can use pipes to reduce the temporary storage required but can changed via the command line.

## Features
* Cross-Platform. Works on all platforms where .NET core is available.
* Simple command line interface. Intuitive and easy to use.
* Support for packaging both VOD and live archive assets.
* Marks migrated assets and provides summary.

## Open Issues
* Live assets with discontinuiities are not supported.
* Captions in TTML are not migrated.
* Direct migration from an Azure Storage account without using the AMS API.

# Credentials used
The tools uses Azure Identity library for authentication.
See [here](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme?view=azure-dotnet) for various ways to authenticate and the settings needed.
The identity used to migrate must have 'Contributor' role on the Azure Media Services account being migrated.

# Types of Migration
The tool supports various types of migration depending on the asset format and the command line options.
* It can simply upload the files to the new storage account.
* For assets created by live events, it can convert to MP4 files and then upload.
* For direct streaming, it can convers the assets to CMAF files with a DASH and HLS manifest.

# Temporary storage needed.
The tool uses temporary storage space for format conversion and uses pipes where possible to minimize storage usage.
Smooth Streaming assets or assets from live events dont need to be downloaded locally.

## Linux
* The only storage needed is for manifests when using shaka packager.
* When using ffmpeg, if the asset files are MP4, it downloads the files locally before converting so storage is proportional to asset size.

## Windows
* Shaka packager writes the packaged files to local disk first before uploading due to a windows specific bug.
* Using ffmpeg needs doulbe the local disk space when packaging MP4 files.
* Smooth Streaming assets or assets from live events dont need to be downloaded locally.

## Migrate to an Azure Storage Account.

Ensure that the Identity you are using to migrate has the following permissions
* 'Storage Blob Delegator' role on the storage account to which you are migrating.


# FFmpeg dependency
The tool optionally uses ffmpeg for media format conversion. It primarily uses shaka-packager but can be changed to use ffmpeg.
It doesn't ship a copy of FFmpeg itself but uses the one in the PATH.
* On Windows you can use winget or chocolatey to install ffmpeg.
```
winget install ffmpeg
```
* On Ubuntu/Debian Linux use apt to install ffmpeg
```
sudo apt install -y ffmpeg
```
* On RedHat Linux use dnf to install ffmpeg.
```
sudo dnf install ffmpeg
```
* On MacOs use brew to install ffmpeg
```
brew install ffmpeg
```
