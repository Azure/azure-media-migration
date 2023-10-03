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
* Support for packaging live archive assets.
* Support for copying non-streamable assets.
* Marks migrated assets and provides HTML summary on analyze
* Support for statically encrypting the content while packaging.

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
- The identity used to migrate must have the role ['Key Vault Secrets Officer'](https://learn.microsoft.com/en-us/azure/key-vault/general/rbac-guide?tabs=azure-cli#azure-built-in-roles-for-key-vault-data-plane-operations) on the key vault used to store the keys. If the key vault is not using Azure RBAC then you will have to create an Access policy giving secrets management permission to the identity used.

# Prerequisite dependencies

The following are the tools that we require the user to install on the machine that is running the migration tool

- [FFmpeg](https://ffmpeg.org/download.html)

    Some types of AMS asset requires FFmpeg in order to complete (e.g. Smooth content).  
    To handle these type of contents, we expect that user install FFmpeg and add it to the path prior to running the migration tool so that it has access to FFmpeg.

    Please follow info in this [link](https://ffmpeg.org/download.html) to install FFmpeg on yours system and add it to the path of your system.

    Some installation examples:

    - For Ubuntu, you can use the package manager 'apt' which will install and add ffmpeg to your path.

        ```
        sudo apt install -y ffmpeg
        ```

    - For Windows 11, you can use 'winget' to install from [gyan.dev](https://www.gyan.dev/ffmpeg/builds/) which will install and add ffmpeg to your path (Note that for windows, after you install, you need to recycle the command prompt windows to get the updated PATH.)

        ```
        winget install ffmpeg
        ```

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

# Deployment
## Running in Azure
The quickest way to run the tool in Azure is use the deployment scripts. See [deployment.md](deployment/deployment.md) for more instructions how to quickly run it.

# Test streaming of migrated content
One way to test streaming of migrated content is to stream directly from the migrated Azure blob storage.  See [blobStreaming.md](doc/blobStreaming.md) for more instructions.

# AES Encryption

The migration tool can be optionally configured to perform static packaging with clear key encryption.  In this mode, the tool needs access to an Azure Key Vault (with the 'Key Vault Secrets Officer' permission given to the tool).

To enable clear key encryption while migrating, append '--key-vault-uri' parameter to the command line.  An example,

    AMSMigrate.exe assets -s <subscription> -g <resource group of media service> -n <media service account name> -o <output storage account uri> -e --key-vault-uri https://<your azure key vault name>.vault.azure.net

The tool will generate a new key id and key and store it as secrets in the azure key vault (secret name = key id, secret = key) and encrypt the media contents with the key.  The encrypted outputs can not be viewed directly via its azure blob storage url.  It requires a proxy server to perform manifest rewrite and key delivery.  An example of a proxy server that is capable of supporting viewing of the encrypted content can be found here [PlaybackService.md](tools/PlaybackService/README.md).

Currently the tool does not support decryption of the encrypted content for you.  If you need to recover the encrypted content, you can manually decrypt the content using shaka packager directly ([using raw key](https://shaka-project.github.io/shaka-packager/html/tutorials/raw_key.html)).  An example workflow is to download your asset from storage locally, look at the manifest to identify the key id and retrieve the key from azure key vault, and identify list of media files from manifest and then run shaka packager directly to decrypt.

To locate the key id, you can look at your dash manifest file (.mpd) and locate the following string

    cenc:default_KID="<key id>"

then you can use the 'key id' to look up the key in your azure key vault (you can do it via azure portal or programmatically).

Then you need to identify the list of media streams and its type which you can do by looking the 

    <BaseURL>fileName</BaseURL>

and its associated mimeType attribute in the 'Representation' tag.

For example, if we have one audio stream called audio.mp4, and two video streams called video1.mp4, video2.mp4, we can use the following command line (shown for Windows) to decrypt the content,

    packager-win-x64.exe --enable_raw_key_decryption --keys label=cenc:key=<key>:key_id=<key id> --mpd_output clear.mpd --hls_master_playlist_output clear.m3u8 in=audio.mp4,stream=audio,output=clear_audio.mp4,playlist_name=clear0.m3u8 in=video1.mp4,stream=video,output=clear_video1.m4,playlist_name=clear1.m3u8 in=video2.mp4,stream=video,output=clear_video2.mp4,playlist_name=clear2.m3u8

# Troubleshooting

This section contains some tips that hopefully will be helpful to you in troubleshoot issues during migration.  At the end of running the 'assets' command to do migration, a summary of the status will be printed on the screen that looks like this:

Asset Summary:
| Asset Type        | Count |
| ----------        | ----- |
| Total             |   *   |
| Already Migrated  |   *   |
| Skipped           |   *   |
| Successful        |   *   |
| Failed            |   *   |

if you don't see any counts in the 'Skipped' or 'Failed' row, then all the jobs were completed successfully.

If you have nonzero counts in 'Failed', then you can do a couple of things to identify the asset(s) that failed:

1. Look at the log file (the path of which will be printed on the console) to figure out which asset(s) failed.  One good way to identify the status of an asset is to grep this log line in the log file

        Migrated asset: <asset name>, container: <container name>, type: vod-fmp4, status: Completed

    there should be one of this line for each asset which tells you the status of the migration.

2. Run 'analyze' command, which will output an html file and a json file (the path of which will be printed on the console). You can open the html page in a browser to get a table of all the assets and its migration status, or open the .json file with an appropriate editor (such as Visual Studio Code) to get a better view for the list of assets and their migration status.

3. The tips on how to get new streaming URL of the generated content for an existing streaming URL of an input asset:
   
     A typical streaming URL of an input asset has below format:

        https://{StreamingEndpoint_HostName_Or_CDN_Endpoint}:443/{LocatorGuid}/{manifestName}.ism/format(.....)  with optional extension .mpd or .m3u8
  
     Figure out the {LocatorGuid} and {manifestName} part from the input streaming URL.

     Run 'analyze' command, get the HTML file or JSON file for the detail report.
     
     Search the json file or html file for {LocatorGuid}, if it is found, take the matching record.
     
     If the record shows "Completed" in MigrationStatus field, take values from below fields (columns in html page):
  
          "OutputHlsUrl"  : {new StreamingURL with .m3u8 extension for HLS},
          "OutputDashUrl" : {new StreamingURL with .mpd extension for DASH}

     The URL in above two fields should share the host name, path and basic file name, only the file extension is different.
  
     Double check the basic file name in new URL, it should match with the manifestName from the input streaming URL.
  
     Please be aware, an input asset might have multiple locators, after the data migration, all these streaming URLs map to the same output streaming URL.
  
     If the output container doesn't enable public view, an appropriate SAS token might be required at the end of the streaming URL for a playback or download.

   
Once you find the asset that failed, you can look at the migration log file, and try to find all the log lines that corresponds to the asset name / asset container.  As multiple assets are being migrated in batch, the log file might be a bit hard to grok.  So another way to simplify the log is to just run migration on a single 'failed' asset, e.g.

    AMSMigrate.exe assets -s <subscription> -g <resource group of media service> -n <media service account name> -o <output storage account uri> -f "name eq '<asset name>'"

that way you get a cleaner / easier log to look at.

Below are some tip(s) about error(s) that may occurs, (more will be added later on):

- If your migration failed for certain asset, and you see the following log line:

        [09:59:28 WRN] Another tool is working on the container test and output path: aaaa/bbbbb/cccc

  This could mean that either another instance of AMSMigrate is working on the same output container or the output container is in a bad state (possibly due to a previous AMSMigrate process was terminated abruptly or crashed).  The reason is that AMSMigrate is designed to prevent multiple instances from writing to the same output path and it does this by creating a blob called '__migrate' and lock it via acquiring the lease.
  
  So in this case, if you look the the output container path, you will find a __migrate blob that is locked via lease which you'll need to break the lease of prior to another run that writes to this location.  So if you know that another tool is not working on this output path, then you will need to break the lease the '__migrate' blob.  This can be done manually using the azure portal or azure storage explorer.  Another approach is to run the same AMSMigrate 'asset' command that writes to this output path but append '--break-output-lease' flag which will automatically break the lease of the __migrate blob for you.

# Post AMS shutdown migration
After AMS shutdown, you can not migrate from your AMS account any longer as it doesn't exist anymore.  If you have any unmigrated contents, then the tool provides an alternative (but somewhat limited in functionality, e.g. no decryption support) way for you to migrate directly from the storage container where your asset is located.  However, we recommed that you finish your migration prior to AMS shutdown.

To migrate from directly storage account, you need to identify the storage container yourself and then you can follow the instruction in the [direct storage migration](doc/storageCommand.md) document.


