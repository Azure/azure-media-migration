function play() {
    removePlayers();

    showError("error", "");
    showError("dash-error", "");
    showError("shaka-error", "");
    var input = document.getElementById("dash-input").value;
    if (!input) {
        showError("error", "Please input your .mpd uri on azure storage");
        return;
    }

    var blobUriPattern = "^https://([^./]+)\\.blob\\.core\\.windows\\.net/(.+)\\.((mpd)|(m3u8?))$";
    var match = input.match(blobUriPattern);
    if (!match) {
        showError("error", `<b>${input}</b> is an invalid azure blob uri for dash.`);
        return;
    }

    var container = match[1];
    var path = `${match[2]}.${match[3]}`;
    console.log(`Find dash input, container: ${container}, path: ${path}`);

    var playUri = `/${container}/${path}?token=abcd`;
    console.log(`Will play with uri: ${playUri}`);

    if (path.endsWith(".mpd"))
    {
        dashPlay(playUri);
        shakaPlay(playUri);
    }
    else
    {
        playUri = `${window.location.origin}${playUri}`;
        showError("error", `Player doesn't support HLS, please access <a href="${playUri}" target=_blank>${playUri}</a> from safari on iOS/Mac directly.`);
    }
}

function dashPlay(playUri) {
    var video = createVideoElement(document.getElementById("dashvideo-container"));
    var player = dashjs.MediaPlayer().create();
    player.initialize(video, playUri, true);
    player.on("error", (e) => {
        console.log("dash-error");
        console.log(e);
        if (e.error.data.request.action == "download") {
            switch (e.error.data.response.status) {
                case 403:
                    showError("dash-error", `Download failed. Maybe the playback service doesn't have permission to access your container <b>${container}</b>. Please grant <b>Storage Blob Data Reader</b> permission to <b>ams-ok</b> application.`);
                    return;

                case 404:
                    showError("dash-error", `The file can't be found on the container <b>${container}</b>, please double check it exists.`);
                    return;
            }
        }

        showError("dash-error", "Hmm, there are some erros, please check the console for detail.");
    });
}

async function shakaPlay(playUri) {
    var video = createVideoElement(document.getElementById("shakavideo-container"));
    var player = new shaka.Player(video);
    player.addEventListener("error", (e) => {
        console.log("shaka-error");
        console.log(e);
        if (e.detail && e.detail.message) {
            showError("shaka-error", e.detail.message)
            return;
        }

        showError("shaka-error", "Hmm, there are some errors, please check the console for detial.");
    });
    try {
        await player.load(playUri);
    } catch (e) {
        console.log("shaka-error");
        console.log(e);
        if (e.detail && e.detail.message) {
            showError("shaka-error", e.detail.message)
            return;
        }

        showError("shaka-error", "Hmm, there are some errors, please check the console for detial.");
    }
}

function showError(domId, msg) {
    var errorDom = document.getElementById(domId);
    errorDom.innerHTML = `<i>${msg}</i>`;
}

function removePlayers()
{
    var videos = document.getElementsByTagName("video");
    while (videos.length) {
        videos[videos.length - 1].remove();
    }
}

function clearVideoElement(parent)
{
    while (parent.firstChild) {
        parent.removeChild(parent.lastChild);
    }
}

function createVideoElement(parent)
{
    var video = document.createElement('video');
    video.setAttribute("controls", "controls");
    video.setAttribute("autoplay", "autoplay");
    parent.appendChild(video);
    return video;
}