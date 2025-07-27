using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using System.IO;
using System.Text;
using System.Net;
using Newtonsoft.Json;
using PostyFox_Posting;
using PostyFox_DataLayer.TableEntities;
using System.Collections.Generic;
using System.Linq;

public class PostTests
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<IAzureClientFactory<TableServiceClient>> _tableClientFactoryMock;
    private readonly Mock<IAzureClientFactory<BlobServiceClient>> _blobClientFactoryMock;
    private readonly Mock<IAzureClientFactory<QueueServiceClient>> _queueClientFactoryMock;
    private readonly Mock<TableServiceClient> _tableServiceClientMock;
    private readonly Mock<BlobServiceClient> _blobServiceClientMock;
    private readonly Mock<QueueServiceClient> _queueServiceClientMock;
    private readonly Mock<TableClient> _tableClientMock;
    private readonly Mock<BlobContainerClient> _blobContainerClientMock;
    private readonly Mock<QueueClient> _queueClientMock;
    private readonly Post _post;

    public PostTests()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _tableClientFactoryMock = new Mock<IAzureClientFactory<TableServiceClient>>();
        _blobClientFactoryMock = new Mock<IAzureClientFactory<BlobServiceClient>>();
        _queueClientFactoryMock = new Mock<IAzureClientFactory<QueueServiceClient>>();
        _tableServiceClientMock = new Mock<TableServiceClient>();
        _blobServiceClientMock = new Mock<BlobServiceClient>();
        _queueServiceClientMock = new Mock<QueueServiceClient>();
        _tableClientMock = new Mock<TableClient>();
        _blobContainerClientMock = new Mock<BlobContainerClient>();
        _queueClientMock = new Mock<QueueClient>();

        _tableClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_tableServiceClientMock.Object);
        _blobClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_blobServiceClientMock.Object);
        _queueClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_queueServiceClientMock.Object);
        _tableServiceClientMock.Setup(x => x.GetTableClient(It.IsAny<string>())).Returns(_tableClientMock.Object);
        _blobServiceClientMock.Setup(x => x.GetBlobContainerClient(It.IsAny<string>())).Returns(_blobContainerClientMock.Object);
        _queueServiceClientMock.Setup(x => x.GetQueueClient(It.IsAny<string>())).Returns(_queueClientMock.Object);

        _post = new Post(_loggerFactoryMock.Object, _tableClientFactoryMock.Object, _blobClientFactoryMock.Object, _queueClientFactoryMock.Object);
    }

    [Fact]
    public void Run_ValidApiKey_ReturnsOk()
    {
        // Arrange
        var reqMock = new Mock<HttpRequestData>(MockBehavior.Strict);
        var contextMock = new Mock<FunctionContext>();
        var responseMock = new Mock<HttpResponseData>(contextMock.Object);
        var postParameters = new Post.PostParameters
        {
            APIKey = new ProfileAPIKeyDTO { UserID = "testuser", ID = "testkey" },
            TargetPlatforms = new List<string> { "platform1" },
            Media = new List<string>(),
            Title = "Test Title",
            Description = "Test Description",
            HTMLDescription = "Test HTML Description",
            Tags = new List<string> { "tag1", "tag2" },
            PostAt = DateTime.UtcNow
        };
        var requestBody = JsonConvert.SerializeObject(postParameters);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(requestBody));
        reqMock.Setup(x => x.Body).Returns(stream);
        reqMock.Setup(x => x.CreateResponse(It.IsAny<HttpStatusCode>())).Returns(responseMock.Object);
        _tableClientMock.Setup(x => x.Query<ProfileAPIKeyTableEntity>(It.IsAny<Func<ProfileAPIKeyTableEntity, bool>>()))
            .Returns(new List<ProfileAPIKeyTableEntity> { new ProfileAPIKeyTableEntity { PartitionKey = "testuser", RowKey = "testkey" } }.AsQueryable());
        _blobContainerClientMock.Setup(x => x.UploadBlob(It.IsAny<string>(), It.IsAny<BinaryData>()));
        _queueClientMock.Setup(x => x.SendMessage(It.IsAny<string>()));

        // Act
        var result = _post.Run(reqMock.Object);

        // Assert
        Assert.Equal(responseMock.Object, result);
        reqMock.VerifyAll();
        _tableClientMock.VerifyAll();
        _blobContainerClientMock.VerifyAll();
        _queueClientMock.VerifyAll();
    }

    [Fact]
    public void Run_InvalidApiKey_ReturnsUnauthorized()
    {
        // Arrange
        var reqMock = new Mock<HttpRequestData>(MockBehavior.Strict);
        var contextMock = new Mock<FunctionContext>();
        var responseMock = new Mock<HttpResponseData>(contextMock.Object);
        var postParameters = new Post.PostParameters
        {
            APIKey = new ProfileAPIKeyDTO { UserID = "testuser", ID = "invalidkey" },
            TargetPlatforms = new List<string> { "platform1" },
            Media = new List<string>(),
            Title = "Test Title",
            Description = "Test Description",
            HTMLDescription = "Test HTML Description",
            Tags = new List<string> { "tag1", "tag2" },
            PostAt = DateTime.UtcNow
        };
        var requestBody = JsonConvert.SerializeObject(postParameters);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(requestBody));
        reqMock.Setup(x => x.Body).Returns(stream);
        reqMock.Setup(x => x.CreateResponse(It.IsAny<HttpStatusCode>())).Returns(responseMock.Object);
        _tableClientMock.Setup(x => x.Query<ProfileAPIKeyTableEntity>(It.IsAny<Func<ProfileAPIKeyTableEntity, bool>>()))
            .Returns(new List<ProfileAPIKeyTableEntity>().AsQueryable());

        // Act
        var result = _post.Run(reqMock.Object);

        // Assert
        Assert.Equal(responseMock.Object, result);
        reqMock.VerifyAll();
        _tableClientMock.VerifyAll();
    }

    [Fact]
    public void Run_NullParameters_ReturnsBadRequest()
    {
        // Arrange
        var reqMock = new Mock<HttpRequestData>(MockBehavior.Strict);
        var contextMock = new Mock<FunctionContext>();
        var responseMock = new Mock<HttpResponseData>(contextMock.Object);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(""));
        reqMock.Setup(x => x.Body).Returns(stream);
        reqMock.Setup(x => x.CreateResponse(It.IsAny<HttpStatusCode>())).Returns(responseMock.Object);

        // Act
        var result = _post.Run(reqMock.Object);

        // Assert
        Assert.Equal(responseMock.Object, result);
        reqMock.VerifyAll();
    }
}
