using Azure.Storage.Blobs;
using Azure;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using Moq;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Xunit;
using Microsoft.AspNetCore.Http.Headers;
using Azure.Security.KeyVault.Secrets;

namespace PlaybackService.Test;

public abstract class TestBase
{
    public static Mock<IBlobClientFactory> CreateBlobClientFactoryMock(
        BlobProperties blobProperties,
        string storageAccountUri,
        string containerName,
        string blobName,
        Stream blobContent)
    {
        var blobPropertiesResponse = new Mock<Response<BlobProperties>>();
        blobPropertiesResponse.SetupGet(c => c.Value).Returns(blobProperties);

        var mockBlobClient = new Mock<BlobClient>();
        mockBlobClient.Setup(c => c.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(blobPropertiesResponse.Object));
        mockBlobClient.Setup(c => c.OpenReadAsync(0, default, default, default)).Returns(Task.FromResult<Stream>(blobContent));

        var mockBlobContainerClient = new Mock<BlobContainerClient>();
        mockBlobContainerClient.Setup(c => c.GetBlobClient(blobName)).Returns(mockBlobClient.Object);

        var mockBlobServiceClient = new Mock<BlobServiceClient>();
        mockBlobServiceClient.Setup(c => c.GetBlobContainerClient(containerName)).Returns(mockBlobContainerClient.Object);

        var blobClientFactory = new Mock<IBlobClientFactory>();
        blobClientFactory.Setup(c => c.CreateBlobServiceClient(storageAccountUri)).Returns(mockBlobServiceClient.Object);
        return blobClientFactory;
    }

    public static (Mock<HttpContext>, Mock<HttpRequest>, Mock<HttpResponse>) CreateHttpContentMock(string? userToken = "abcd")
    {
        var mockHttpRequest = new Mock<HttpRequest>();

        var mockHttpResponse = new Mock<HttpResponse>();
        mockHttpResponse.Setup(c => c.Headers).Returns(new HeaderDictionary());

        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(c => c.Request).Returns(mockHttpRequest.Object);
        mockHttpContext.Setup(c => c.Response).Returns(mockHttpResponse.Object);

        if (userToken != null)
        {
            var claimsIdentity = new ClaimsIdentity("Custom");
            claimsIdentity.AddClaim(new Claim("token", userToken));
            var user = new ClaimsPrincipal(claimsIdentity);
            mockHttpContext.Setup(c => c.User).Returns(user);
        }

        return (mockHttpContext, mockHttpRequest, mockHttpResponse);
    }

    public static IOptionsSnapshot<T> CreateIOptionSnapshotMock<T>(T value) 
        where T : class
    {
        var mock = new Mock<IOptionsSnapshot<T>>();
        mock.Setup(m => m.Value).Returns(value);
        return mock.Object;
    }

    public static Mock<SecretClient> CreateSecretClientMock(KeyVaultSecret secret)
    {
        var keyVaultSecretResponse = new Mock<Response<KeyVaultSecret>>();
        keyVaultSecretResponse.SetupGet(c => c.Value).Returns(secret);

        var secretClientMock = new Mock<SecretClient>();
        secretClientMock.Setup(x => x.GetSecretAsync(secret.Name, null, default)).ReturnsAsync(keyVaultSecretResponse.Object);
        return secretClientMock;
    }

    protected static void AssertOutputIsExpected(MemoryStream actualOutput, string expectedOutputFileName)
    {
        actualOutput.Position = 0;
        using var reader = new StreamReader(actualOutput);
        var actualOutputContent = reader.ReadToEnd();

        var expectedOutputContent = File.ReadAllText(expectedOutputFileName);
        Assert.Equal(expectedOutputContent.Replace("\r\n", "\n"), actualOutputContent.Replace("\r\n", "\n"));
    }
}
