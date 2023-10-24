using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using static PlaybackService.KeyDeliverController;

namespace PlaybackService.Test;

public class KeyDeliverControllerTest : TestBase
{
    [Fact]
    public async Task ClearKeyShouldBeDeliveredForHlsKeyRequest()
    {
        var keyId = Guid.NewGuid().ToString("n");
        var key = Guid.NewGuid().ToString("n");

        // Create secret client mock.
        var secretClientMock = CreateSecretClientMock(new KeyVaultSecret(keyId, key));

        // Create HttpContext Mock.
        var (httpContextMock, httpRequestMock, httpResponseMock) = CreateHttpContentMock();

        // Hook HttpResponseMock.
        using var output = new MemoryStream();
        string? contentType = null;
        long? contentLength = null;
        httpResponseMock.SetupSet(c => c.ContentType = It.IsAny<string?>()).Callback<string?>(c => contentType = c);
        httpResponseMock.SetupSet(c => c.ContentLength = It.IsAny<long?>()).Callback<long?>(c => contentLength = c);
        httpResponseMock.Setup(c => c.Body).Returns(output);

        // Create controller.
        var keyDeliverController = new KeyDeliverController(NullLogger<KeyDeliverController>.Instance);
        keyDeliverController.ControllerContext.HttpContext = httpContextMock.Object;

        // Get the clear key.
        await keyDeliverController.GetAsync(keyId, secretClientMock.Object, new KeyCache());

        // Make sure content type is right.
        Assert.Equal("application/octet-stream", contentType);

        // Make sure content length is right. (Guid is 128 bits (16 bytes) length)
        Assert.Equal(16, contentLength);

        // Make sure content is right.
        var receivedKey = new byte[contentLength!.Value];
        output.Position = 0;
        await output.ReadAsync(receivedKey);
        Assert.Equal(BitConverter.ToString(receivedKey).Replace("-", "").ToLower(), key);
    }

    [Fact]
    public async Task ClearKeyShouldBeDeliveredForDashKeyRequest()
    {
        var keyId = BitConverter.ToString(Convert.FromBase64String("d2wUTiirQ3S+pdTTq/FboA==")).Replace("-", "").ToLower();
        var key = BitConverter.ToString(Convert.FromBase64String("j5K7Op3uSIG+AZ18s/rugg==")).Replace("-", "").ToLower();

        var dashKeyId = "d2wUTiirQ3S-pdTTq_FboA";
        var dashKey = "j5K7Op3uSIG-AZ18s_rugg";

        // Create secret client mock.
        var secretClientMock = CreateSecretClientMock(new KeyVaultSecret(keyId, key));

        // Create HttpContext Mock.
        var (httpContextMock, httpRequestMock, httpResponseMock) = CreateHttpContentMock();

        // Create controller.
        var keyDeliverController = new KeyDeliverController(NullLogger<KeyDeliverController>.Instance);
        keyDeliverController.ControllerContext.HttpContext = httpContextMock.Object;

        // Get the clear key. For dash, the keyId/key is base64 encoded (trim the last '=')
        var keyRequest = new KeyRequest()
        {
            Kids = new[] { dashKeyId },
            Type = "abcdefg",
        };
        var result = await keyDeliverController.PostAsync(keyRequest, secretClientMock.Object, new KeyCache());

        // Make sure the content result is right.
        var jsonResult = result as JsonResult;
        Assert.NotNull(jsonResult);

        var keyResult = jsonResult.Value as KeyRequestResponse;
        Assert.NotNull(keyResult);
        Assert.Single(keyResult.Keys);
        Assert.Equal(dashKeyId, keyResult.Keys[0].KeyId);
        Assert.Equal(dashKey, keyResult.Keys[0].Key);
        Assert.Equal("abcdefg", keyRequest.Type);
    }
}
