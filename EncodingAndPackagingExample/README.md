Encoding and Packaging Samples
==

This folder contains sample code about how to encode an MP4 video file into multiple resolution streams and pacakge these streams
to DASH format which is suitable for playback in browser.

There are three projects: 

* `EncodingAndPackagingTool.Core` contains the code of core functionality.
* `EncodingAndPackagingTool.Cli` contains a standalone command line executable application which will invoke `EncodingAndPackagingTool.Core`.
* `EncodingAndPackagingTool.AzureFunction` contains an Azure Function which can be triggered by a message from azure message queue.

In order to run these samples, user need install [FFmpeg](https://ffmpeg.org/download.html) on the machine. Please follow info in this
[link](https://ffmpeg.org/download.html) to install FFmpeg on yours system and add it to the path of your system.

Some installation examples:

- For Ubuntu, you can use the package manager 'apt' which will install and add ffmpeg to your path.

    ```
    sudo apt install -y ffmpeg
    ```

- For Windows 11, you can use 'winget' to install from [gyan.dev](https://www.gyan.dev/ffmpeg/builds/) which will install and add ffmpeg to your path (Note that for windows, after you install, you need to recycle the command prompt windows to get the updated PATH.)

    ```
    winget install ffmpeg
    ```

These samples also Azure Identity library for authentication.
See [here](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme?view=azure-dotnet) for various ways to authenticate and the settings needed.

You'll need to have the following permissions:

- The identity must have `Reader` role on the MP4 file input container blob.
- The identity must have `Contributor` role on the output container.

If you want to deploy `EncodingAndPackagingTool.AzureFunction` sample in an Azure Function, please following info in this [link](https://learn.microsoft.com/en-us/azure/azure-functions/functions-how-to-custom-container?tabs=core-tools%2Cacr%2Cazure-cli&pivots=azure-functions).