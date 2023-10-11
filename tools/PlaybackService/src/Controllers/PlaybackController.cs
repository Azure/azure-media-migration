using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;

namespace PlaybackService;

public class PlaybackController : ControllerBase
{
    private readonly Options _options;
    private readonly ILogger _logger;

    public PlaybackController(IOptionsSnapshot<Options> options, ILogger<PlaybackController> logger)
    {
        var optionValidationResult = options.Value.Validate(name: null, options.Value);
        if (optionValidationResult.Failed)
        {
            throw new ArgumentException(nameof(options), optionValidationResult.FailureMessage);
        }

        _options = options.Value;
        _logger = logger;
    }

    [Route("/{storage}/{container}/{*path}")]
    [HttpGet]
    public async Task GetAsync(string storage, string container, string path,
        [FromServices] IBlobClientFactory blobClientFactory)
    {
        var logger = _logger;
        var allowedFileExtensions = _options.AllowedFileExtensions;
        var skipCheckStorageHostExists = _options.SkipCheckStorageHostExists;

        using (logger.BeginScope(nameof(GetAsync)))
        {
            logger.LogDebug($"Process Get request for {Request.GetDisplayUrl()}");

            // Path is required.
            if (string.IsNullOrEmpty(path))
            {
                throw new BadHttpRequestException("path can't be empty");
            }

            // Check whether the file is allowed to access.
            if (!allowedFileExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            {
                logger.LogDebug($"{path} is not in allowed list.");
                throw new BadHttpRequestException("", (int)HttpStatusCode.Forbidden);
            }

            // Get blob service client.
            BlobServiceClient? blobService = null;
#if DEBUG
            if (storage == "devstoreaccount1")
            {
                blobService = blobClientFactory.CreateBlobServiceClient("UseDevelopmentStorage=true");
            }
#endif
            if (blobService == null)
            {
                // Get the storage account info. if storage is not a domain, add the azure storage post fix.
                if (!storage.Contains('.'))
                {
                    storage = $"{storage}.blob.core.windows.net";
                }
                logger.LogDebug($"Use storage account: {storage}");

                // Check the storage account is valid or not.
                if (!skipCheckStorageHostExists)
                {
                    try
                    {
                        if ((await Dns.GetHostAddressesAsync(storage)).Count() == 0)
                        {
                            throw new BadHttpRequestException($"storage account '{storage}' doesn't exist.");
                        }
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.HostNotFound)
                    {
                        throw new BadHttpRequestException($"storage account '{storage}' doesn't exist.");
                    }
                    logger.LogDebug($"Find the storage account host domain: {storage}");
                }

                // Get the storage uri.
                if (!Uri.TryCreate($"https://{storage}", UriKind.Absolute, out var storageUri))
                {
                    throw new BadHttpRequestException($"storage account '{storage}' format is invalid.");
                }
                logger.LogDebug($"Storage uri: {storageUri}");

                // Get the blob service.
                blobService = blobClientFactory.CreateBlobServiceClient(storageUri.ToString());
            }

            // Get the blob client.
            var containerClient = blobService.GetBlobContainerClient(container);
            logger.LogDebug($"Container uri: {containerClient.Uri}");

            var blobClient = containerClient.GetBlobClient(path);
            logger.LogDebug($"Blob uri: {blobClient.Uri}");

            // Save the content type from the blob's properties.
            var result = await blobClient.GetPropertiesAsync().ConfigureAwait(false);
            if (result?.Value?.ContentType == null || result?.Value?.ContentLength == null)
            {
                throw new Exception($"Can't get the content type and content length from blob: {blobClient.Uri}");
            }
            Response.ContentType = result.Value.ContentType;
            logger.LogDebug($"Blob content type: {Response.ContentType}");

            // Process the request by files
            if (path.EndsWith(".mpd") || path.EndsWith(".m3u8") || path.EndsWith(".m3u"))
            {
                // For dash manifest, require the user is authenticated.
                if (User.Identity?.IsAuthenticated != true)
                {
                    logger.LogDebug("User is unauthenticated to get hls manifest.");
                    throw new UnauthorizedAccessException();
                }

                // Get the token from the user's claims.
                var token = User.Claims.FirstOrDefault(c => c.Type == "token")?.Value;
                if (string.IsNullOrEmpty(token))
                {
                    logger.LogCritical("token is not in authenticated user's claims, authenticate middleware must be wrong.");
                    throw new UnauthorizedAccessException();
                }

                // Disable cache for manifest.
                Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                Response.Headers.Expires = "0";

                if (path.EndsWith(".mpd"))
                {
                    await GetDashManifestAsync(blobClient, token).ConfigureAwait(false);
                }
                else
                {
                    await GetHlsManifestAsync(blobClient, token).ConfigureAwait(false);
                }
            }
            else
            {
                await GetOtherFilesAsync(blobClient, result.Value.ContentLength).ConfigureAwait(false);
            }

            // All done.
            logger.LogDebug($"Process Get request exit.");
        }
    }

    private async Task GetDashManifestAsync(BlobClient blob, string token)
    {
        var logger = _logger;

        logger.LogDebug("Process dash manifest request.");

        // For dash, we need read all the content.
        var dash = new XmlDocument();
        var dashStream = await blob.OpenReadAsync().ConfigureAwait(false);
        using (var reader = new StreamReader(dashStream))
        {
            var dashContent = await reader.ReadToEndAsync().ConfigureAwait(false);
            dash.LoadXml(dashContent);
        }

        // Find all AdaptionSets node.
        var root = dash.DocumentElement;
        if (root == null)
        {
            throw new Exception("Bad dash manifest format.");
        }

        var nsmgr = new XmlNamespaceManager(dash.NameTable);
        nsmgr.AddNamespace("dash", root.NamespaceURI);
        var adaptationSets = root.SelectNodes("dash:Period/dash:AdaptationSet", nsmgr);
        logger.LogDebug($"Found AdaptationSet count: {adaptationSets?.Count}");

        // For each adaptationSet, inject clearkey uri.
        if (adaptationSets != null)
        {
            for (var i = 0; i < adaptationSets.Count; ++i)
            {
                var adaptationSet = adaptationSets[i];

                // if there is no ContentProtection node, which means the content is not encrypted,
                // so we can ignore it.
                var contentProtection = adaptationSet!.SelectSingleNode("dash:ContentProtection", nsmgr);
                if (contentProtection == null)
                {
                    logger.LogDebug($"No ContentProtection in AdaptationSet({i}), ignore it.");
                    continue;
                }

                // Let's ingest the format uri.
                var contentProtectionEL = dash.CreateElement("ContentProtection", adaptationSet.NamespaceURI);
                contentProtectionEL.SetAttribute("schemeIdUri", "urn:uuid:e2719d58-a985-b3c9-781a-b030af78d30e");
                contentProtectionEL.SetAttribute("value", "ClearKey1.0");
                adaptationSet.InsertAfter(contentProtectionEL, contentProtection);

                var licenseServerEL = dash.CreateElement("clearkey", "Laurl", "http://dashif.org/guidelines/clearKey");
                licenseServerEL.AppendChild(dash.CreateTextNode($"/.clearkeys?token={HttpUtility.UrlEncode(token)}"));
                licenseServerEL.SetAttribute("Lic_type", "EME-1.0");
                contentProtectionEL.AppendChild(licenseServerEL);
                logger.LogDebug($"Inject license url for AdaptationSet({i})");
            }
        }

        // Write the result.
        using var xmlStream = new MemoryStream();
        dash.Save(xmlStream);
        xmlStream.Position = 0;
        await xmlStream.CopyToAsync(Response.Body);

        // All done.
        logger.LogDebug("Process dash manifest request exit.");
    }

    private async Task GetHlsManifestAsync(BlobClient blob, string token)
    {
        var logger = _logger;

        logger.LogDebug("Process hls manifest request.");

        // For hls, we need read all the content.
        var hlsStream = await blob.OpenReadAsync();
        if (hlsStream == null)
        {
            throw new Exception($"Can't read the blob: {blob.Uri}");
        }

        logger.LogDebug($"Open the hls manifest blob {blob.Uri} to read success.");

        // Read all lines, inject token to key uri and all playlist files.
        using var reader = new StreamReader(hlsStream);
        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line == null)
            {
                break;
            }

            // Follow the RFC, we should process URI attributes of EXT-X-KEY, EXT-X-MEDIA and EXT-X-I-FRAME-STREAM-INF line.
            if (line.StartsWith("#EXT-X-KEY:")
                || line.StartsWith("#EXT-X-MEDIA:")
                || line.StartsWith("#EXT-X-I-FRAME-STREAM-INF:")
               )
            {
                logger.LogDebug($"Find line {line}");

                var hlsAttributes = ParseHLSAttributes(line, line.IndexOf(':') + 1);

                // Get the original "URI" value.
                if (hlsAttributes.TryGetValue("URI", out var uri))
                {
                    uri = uri.Trim('"');
                    logger.LogDebug($"Extract the URI: {uri}");

                    hlsAttributes["URI"] = $"\"{AddTokenToHlsPlaylistUri(uri, token)}\"";
                }

                // Rebuild the EXT-X-KEY line.
                var attributes = hlsAttributes.ToList().Select(e => $"{e.Key}={e.Value}");
                line = line.Substring(0, line.IndexOf(':') + 1) + string.Join(',', attributes);
            }
            else if (line.StartsWith("#EXT-X-STREAM-INF:"))
            {
                logger.LogDebug($"Find line {line}");

                // for EXT-X-STREAM-INF, the next line is the uri.
                // So, output this line.
                var nextLine = await reader.ReadLineAsync();
                if (nextLine != null)
                {
                    logger.LogDebug($"Find URI line {nextLine}");

                    line = line + "\n" + AddTokenToHlsPlaylistUri(nextLine, token);
                }
            }

            // Output to response.
            var bytes = UTF8Encoding.UTF8.GetBytes(line + "\n");
            await Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }

        // All done.
        logger.LogDebug("Process hls manifest request exit.");
    }

    private IDictionary<string, string> ParseHLSAttributes(string attributes, int startPos)
    {
        var logger = _logger;

        var res = new Dictionary<string, string>();
        while (startPos < attributes.Length)
        {
            var attributeName = ParseHLSAttributeName(attributes, ref startPos);

            // skip '='
            ++startPos;

            var attributeValue = ParseHLSAttributeValue(attributes, ref startPos);

            logger.LogDebug($"Find hls attribute {attributeName}:{attributeValue}");
            res.Add(attributeName, attributeValue);
            
            if (startPos < attributes.Length)
            {
                if (attributes[startPos++] != ',')
                {
                    throw new Exception("Invalid HLS attribute list.");
                }
            }
        }
        return res;
    }

    private string ParseHLSAttributeName(string attributes, ref int startPos)
    {
        if (startPos >= attributes.Length)
        {
            throw new Exception("Invalid HLS attribute name.");
        }

        int assignmentMarkPos = attributes.IndexOf('=', startPos);
        if (assignmentMarkPos == -1)
        {
            throw new Exception("Invalid HLS attribute name.");
        }

        var attributeName = attributes.Substring(startPos, assignmentMarkPos - startPos);
        startPos = assignmentMarkPos;
        return attributeName;
    }

    private string ParseHLSAttributeValue(string attributes, ref int startPos)
    {
        if (startPos >= attributes.Length)
        {
            throw new Exception("Invalid HLS attribute value.");
        }

        if (attributes[startPos] == '"')
        {
            int nextQuotes = attributes.IndexOf('"', startPos + 1);
            if (nextQuotes++ == -1)
            {
                throw new Exception("Invalid HLS attribute value.");
            }

            var attributeValue = attributes.Substring(startPos, nextQuotes - startPos);
            startPos = nextQuotes;
            return attributeValue;
        }

        // Find the comma character.
        int commaPos = attributes.IndexOf(',', startPos);
        if (commaPos == -1)
        {
            var attributeValue = attributes.Substring(startPos);
            startPos = attributes.Length;
            return attributeValue;
        }
        else
        {
            var attributeValue = attributes.Substring(startPos, commaPos - startPos);
            startPos = commaPos;
            return attributeValue;
        }
    }

    private async Task GetOtherFilesAsync(BlobClient blob, long contentLength)
    {
        var logger = _logger;

        logger.LogDebug($"Process request for other files, uri: {blob.Uri}.");

        // Get range form the headers.
        HttpRange? range = null;
        if (RangeHeaderValue.TryParse(Request.Headers["Range"].ToString(), out var value))
        {
            if (value.Unit != "bytes")
            {
                throw new BadHttpRequestException("Only support bytes unit in range header.");
            }

            if (value.Ranges.Count > 0)
            {
                var from = value.Ranges.First().From == null ? 0 : value.Ranges.First().From!.Value;
                if (value.Ranges.First().To != null)
                {
                    range = new HttpRange(from, value.Ranges.First().To!.Value - from + 1);
                }
                else
                {
                    range = new HttpRange(from);
                }

                logger.LogDebug($"Request range: {range.Value.Offset}-{range.Value.Length + range.Value.Offset - 1}");
            }
        }

        // Get blob stream.
        var blobStreamResponse = await blob.DownloadStreamingAsync(range == null ? null : new BlobDownloadOptions() { Range = range.Value }).ConfigureAwait(false);
        if (blobStreamResponse == null)
        {
            throw new Exception($"Can't download the blob {blob.Uri}");
        }

        // Copy the http status code and response headers.
        var blobServerResponse = blobStreamResponse.GetRawResponse();
        Response.StatusCode = blobServerResponse.Status;

        // For security, we only expose the following headers:
        foreach (var allowHeader in new[] { "Content-Length", "Content-Type", "Date", "Accept-Ranges", "Content-MD5", "ETag", "Last-Modified" })
        {
            if (blobServerResponse.Headers.TryGetValue(allowHeader, out var header))
            {
                Response.Headers.TryAdd(allowHeader, header);
            }
        }

        // Copy the body.
        await blobStreamResponse.Value.Content.CopyToAsync(Response.Body).ConfigureAwait(false);

        // All done.
        logger.LogDebug($"Process request for other files exit, uri: {blob.Uri}.");
    }

    private string AddTokenToHlsPlaylistUri(string uri, string token)
    {
        // Does uri has fragment part?
        var fragmentStartPos = uri.IndexOf('#');
        var fragment = fragmentStartPos == -1 ? "" : uri.Substring(fragmentStartPos);
        if (fragmentStartPos != -1)
        {
            uri = uri.Substring(0, fragmentStartPos);
        }

        // Does uri has query part?
        var queryStartPos = uri.IndexOf("?");
        var query = queryStartPos == -1 ? "" : uri.Substring(queryStartPos);
        if (queryStartPos != -1)
        {
            uri = uri.Substring(0, queryStartPos);
        }

        // We only add token for .m3u8, .m3u, .clearkeys, clearkeys.
        if (uri.EndsWith(".m3u8") || uri.EndsWith(".m3u") || uri.EndsWith(".clearkeys"))
        {
            uri += query + (queryStartPos == -1 ? '?' : '&')
                +  $"token={HttpUtility.UrlEncode(token)}"
                +  fragment;
        }
        else
        {
            uri += query + fragment;
        }
        return uri;
    }

    public class Options : IValidateOptions<Options>
    {
        public bool SkipCheckStorageHostExists { get; set; }

        public required string[] AllowedFileExtensions { get; set; }

        public ValidateOptionsResult Validate(string? name, Options options)
        {
            var failure = new StringBuilder();

            if (!(AllowedFileExtensions?.Length > 0))
            {
                failure.AppendLine("AllowedFileExtensions can't be empty.");
            }

            if (AllowedFileExtensions!.Any(ext => !ext.StartsWith('.')))
            {
                failure.AppendLine("FileExtensions must be started with '.'");
            }

            return failure.Length > 0 ? ValidateOptionsResult.Fail(failure.ToString()) : ValidateOptionsResult.Success;
        }
    }

    public class DefaultStorageCredential : DefaultAzureCredential
    {
    }
}
