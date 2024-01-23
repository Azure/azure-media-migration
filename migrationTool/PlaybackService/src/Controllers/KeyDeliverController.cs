using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PlaybackService;

[Authorize]
public class KeyDeliverController : ControllerBase
{
    private readonly ILogger _logger;

    public KeyDeliverController(ILogger<KeyDeliverController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    [Route(".clearkeys")]
    public async Task GetAsync(string kid,
        [FromServices] SecretClient secretClient,
        [FromServices] KeyCache keyCache)
    {
        var logger = _logger;

        using (logger.BeginScope(nameof(GetAsync)))
        {
            logger.LogDebug($"Process Get request for {Request.GetDisplayUrl()}");

            if (string.IsNullOrEmpty(kid))
            {
                throw new BadHttpRequestException("kid is empty.");
            }

            logger.LogDebug($"kid: {kid}");

            // Get keys from the keyId
            var keypair = await GetKey(kid, secretClient, keyCache).ConfigureAwait(false);
            var key = keypair.Item2;
            logger.LogDebug($"Get key for kid {kid} " + (key != null ? "success" : "failed"));

            if (key == null)
            {
                Response.StatusCode = 404;
                return;
            }

            // The key returned from key vault is hex string, convert it to binary format.
            byte[] keyBytes;
            if (key.Length % 2 != 0)
            {
                throw new Exception("Bad key format.");
            }

            try
            {
                keyBytes = Enumerable.Range(0, key.Length).Where(x => x % 2 == 0).Select(x => Convert.ToByte(key.Substring(x, 2), 16)).ToArray();
            }
            catch (FormatException ex)
            {
                throw new Exception("Bad key format.", ex);
            }

            logger.LogDebug($"Convert key to binary format success, key length: {keyBytes.Length}");

            // Return the result
            Response.ContentLength = keyBytes.Length;
            Response.ContentType = "application/octet-stream";
            await Response.Body.WriteAsync(keyBytes);

            logger.LogDebug("Process Get request to /.clearkeys exit.");
        }
    }

    [HttpPost]
    [Route(".clearkeys")]
    public async Task<ActionResult> PostAsync([FromBody] KeyRequest keyRequest,
        [FromServices] SecretClient secretClient,
        [FromServices] KeyCache keyCache)
    {
        var logger = _logger;

        using (logger.BeginScope(nameof(PostAsync)))
        {
            logger.LogDebug($"Process Post request for {Request.GetDisplayUrl()}");

            if (!(keyRequest.Kids?.Count() > 0)) 
            {
                throw new BadHttpRequestException("kids is empty.");
            }

            logger.LogDebug($"kids count: {keyRequest.Kids.Count()}");

            if (keyRequest.Kids.Count() > 10)
            {
                throw new BadHttpRequestException($"Too many kids.");
            }

            // Get keys from the keyId
            var keypairs = await Task.WhenAll(keyRequest.Kids.Select(async dashKid => await GetKey(DashKeyIdToKeyId(dashKid), secretClient, keyCache).ConfigureAwait(false))).ConfigureAwait(false);
            var keys = keypairs.Where(t =>
            {
                var kid = t.Item1;
                var key = t.Item2;
                logger.LogDebug($"Get key for kid {kid} " + (key != null ? "success" : "failed"));
                return key != null;
            }).Select(t => new ClearKey() { KeyId = ToDashKeyOrKeyId(t.Item1), Key = ToDashKeyOrKeyId(t.Item2!) }).ToArray();

            // All done.
            var result = new JsonResult(new KeyRequestResponse()
            {
                Keys = keys,
                Type = keyRequest.Type,
            });

            logger.LogDebug("Process Post request to /.clearkeys exit.");
            return result;
        }
    }

    // Return (keyid, key) pair.
    private async ValueTask<(string, string?)> GetKey(string kid, SecretClient secretClient, KeyCache keyCache)
    {
        var logger = _logger;

        var key = keyCache.GetKey(kid);
        if (key != null)
        {
            return (kid, key);
        }
        logger.LogDebug($"Didn't find key from key cache for kid: {kid}, try to get from secret client.");

        var response = await secretClient.GetSecretAsync(kid).ConfigureAwait(false);
        key = response?.Value.Value;
        if (key != null)
        {
            logger.LogDebug($"Received key from KeyVault for kid: {kid}");
            keyCache.AddKey(kid, key);
        }
        else
        {
            logger.LogDebug($"Didn't receive key from KeyVault for kid: {kid}");
        }
        return (kid, key);
    }

    private static string DashKeyIdToKeyId(string dashKid)
    {
        // Dash kid is base64 url encoded, the different between base64 encoded and base64url encoded is that
        // in base64 encoded, '+' will be replaced with '-' and '/' will be replaced with '_', also the ended
        // '=' (padding) will be removed. (padding '=' is optional in base64url encoded, but in dash protocol,
        // which is not used. So, let's convert dashKid from base64url encoded to base64 encoded string.
        dashKid = dashKid.Replace('-', '+').Replace('_', '/');
        while (dashKid.Length % 4 != 0)
        {
            dashKid += '=';
        }

        try
        {
            var bytes = Convert.FromBase64String(dashKid);
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }
        catch (FormatException)
        {
            throw new BadHttpRequestException("kid from request is invalid format.");
        }
    }

    private static string ToDashKeyOrKeyId(string k)
    {
        // See comments in DashKeyIdToKeyId(), use base64url encoding.
        try
        {
            var bytes = Enumerable.Range(0, k.Length).Where(x => x % 2 == 0).Select(x => Convert.ToByte(k.Substring(x, 2), 16)).ToArray();
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
        catch (FormatException)
        {
            throw new Exception("kid from key vault is invalid format.");
        }
    }

    public class KeyRequest
    {
        public string[]? Kids { get; set; }

        public string? Type { get; set; }
    }

    public class KeyRequestResponse
    {
        public required ClearKey[] Keys { get; set; }

        public string? Type { get; set; }
    }

    public class ClearKey
    {
        [JsonPropertyName("kty")]
        public string KeyType { get; set; } = "oct";

        [JsonPropertyName("k")]
        public required string Key { get; set; }

        [JsonPropertyName("kid")]
        public required string KeyId { get; set; }
    }
}
