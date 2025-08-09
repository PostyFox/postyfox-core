using Xunit;
using PostyFox_Posting;
using PostyFox_DataLayer;
using System.Text.Json;

public class PostTests
{
    [Fact]
    public void PostParameters_Serialization_Works()
    {
        // Arrange
        var postParameters = new Post.PostParameters
        {
            APIKey = new ProfileAPIKeyDTO { UserID = "testuser", ID = "testkey" },
            TargetPlatforms = new List<string> { "platform1" },
            Media = new List<string> { "media1.jpg" },
            Title = "Test Title",
            Description = "Test Description",
            HTMLDescription = "Test HTML Description",
            Tags = new List<string> { "tag1", "tag2" },
            PostAt = DateTime.UtcNow
        };

        // Act & Assert - Test that serialization/deserialization works
        var json = JsonSerializer.Serialize(postParameters);
        Assert.NotEmpty(json);
        
        var deserialized = JsonSerializer.Deserialize<Post.PostParameters>(json);
        Assert.NotNull(deserialized);
        Assert.Equal(postParameters.Title, deserialized.Title);
        Assert.Equal(postParameters.Description, deserialized.Description);
    }

    [Fact]
    public void QueueEntry_HasRequiredProperties()
    {
        // Arrange & Act
        var queueEntry = new QueueEntry()
        {
            PostAt = DateTime.UtcNow,
            RootPostId = "test-root-id",
            PostId = "test-post-id",
            User = "test-user",
            TargetPlatformServiceId = "test-platform",
            Status = (int)Post.PostStatus.Queued,
            Media = new List<string> { "media1.jpg" }
        };

        // Assert
        Assert.NotNull(queueEntry.RootPostId);
        Assert.NotNull(queueEntry.PostId);
        Assert.NotNull(queueEntry.User);
        Assert.NotNull(queueEntry.Media);
        Assert.Equal((int)Post.PostStatus.Queued, queueEntry.Status);
    }

    [Fact]
    public void PostResponse_HasCorrectStructure()
    {
        // Arrange & Act
        var response = new Post.PostResponse
        {
            PostId = "test-id",
            Status = Post.PostStatus.Queued,
            MediaSavedUri = "test-uri"
        };

        // Assert
        Assert.NotNull(response.PostId);
        Assert.Equal(Post.PostStatus.Queued, response.Status);
        Assert.NotNull(response.MediaSavedUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void PostParameters_HandlesNullOrEmptyStrings(string? testValue)
    {
        // Arrange & Act
        var postParameters = new Post.PostParameters
        {
            APIKey = new ProfileAPIKeyDTO { UserID = "testuser", ID = "testkey" },
            TargetPlatforms = new List<string> { "platform1" },
            Media = new List<string>(),
            Title = testValue,
            Description = testValue,
            HTMLDescription = testValue,
            Tags = new List<string>(),
            PostAt = null
        };

        // Assert - These should not throw exceptions
        Assert.NotNull(postParameters);
        Assert.Equal(testValue, postParameters.Title);
        Assert.Equal(testValue, postParameters.Description);
        Assert.Equal(testValue, postParameters.HTMLDescription);
    }
}