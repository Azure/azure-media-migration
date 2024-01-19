Encoding and Packaging Sample
==

## Overview

This folder contains sample code that demonstrates how to use FFMpeg to encode and package a MP4 video file into multi-stream MPEG-DASH / HLS format that's suitable for playback in the browser.  The functionality is built into a .NET library, and two sample applications are shown that demonstrates the use the library in a command line tool and in an Azure Function.

Three projects:

* `EncodingAndPackagingTool.Core`: library project containing the core functionality of encoding and packaging using FFmpeg.  It demonstrates how to use FFmpeg to encode and package an input mp4 file from an input Azure blob storage container with a simple setting of encoding to 3 adaptive bitrate streams with h.264 and AAC, and generating HLS/DASH manifests for streaming and uploading the result to an output Azure blob storage.  Please see [EncodingAndPackagingTool.cs (line 98)](https://github.com/Azure/azure-media-migration/blob/main/EncodingAndPackagingExample/EncodingAndPackagingTool.Core/EncodingAndPackagingTool.cs#L98) for further details on the FFmpeg setting used, and feel free to make customizations here to suit your scenarios. 
* `EncodingAndPackagingTool.Cli`: command line executable project that demonstrates how to use the library.
* `EncodingAndPackagingTool.AzureFunction`: Azure Function project that demonstrates how to use the library where the Azure function is triggered by a message from an Azure message queue.


## Prerequisites

- FFmpeg:

    In order to run these samples, user need install [FFmpeg](https://ffmpeg.org/download.html) on the machine. Please follow info in this [link](https://ffmpeg.org/download.html) to install FFmpeg on yours system and add it to the path of your system.

    Some installation examples:

    - For Ubuntu, you can use the package manager 'apt' which will install and add ffmpeg to your path.

        ```
        sudo apt install -y ffmpeg
        ```

    - For Windows 11, you can use 'winget' to install from [gyan.dev](https://www.gyan.dev/ffmpeg/builds/) which will install and add ffmpeg to your path (Note that for windows, after you install, you need to recycle the command prompt windows to get the updated PATH.)

        ```
        winget install ffmpeg
        ```

- Azure Identity / Permission (for Azure Function)
    
    The sample code uses Azure Identity library for authentication.  See [here](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme?view=azure-dotnet) for various ways to authenticate and the settings needed.

    You'll need to have the following permissions:

    - The identity must have `Reader` role on the MP4 file input container blob.
    - The identity must have `Contributor` role on the output container.

    Please follow the information in this [link](https://learn.microsoft.com/en-us/azure/azure-functions/functions-how-to-custom-container?tabs=core-tools%2Cacr%2Cazure-cli&pivots=azure-functions) for instructions on how to deploy `EncodingAndPackagingTool.AzureFunction` sample to an Azure Function