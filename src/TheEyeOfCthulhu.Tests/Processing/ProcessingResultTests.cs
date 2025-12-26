using Xunit;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Core.Processing;

namespace TheEyeOfCthulhu.Tests.Processing;

public class ProcessingResultTests
{
    [Fact]
    public void Ok_CreatesSuccessfulResult()
    {
        // Arrange
        var frame = CreateTestFrame();
        
        // Act
        var result = ProcessingResult.Ok(frame);
        
        // Assert
        Assert.True(result.Success);
        Assert.Same(frame, result.Frame);
        Assert.Null(result.ErrorMessage);
        Assert.Empty(result.Metadata);
    }

    [Fact]
    public void Ok_WithMetadata_IncludesMetadata()
    {
        // Arrange
        var frame = CreateTestFrame();
        var metadata = new Dictionary<string, object>
        {
            ["Count"] = 5,
            ["Detected"] = true
        };
        
        // Act
        var result = ProcessingResult.Ok(frame, metadata);
        
        // Assert
        Assert.Equal(5, result.Metadata["Count"]);
        Assert.Equal(true, result.Metadata["Detected"]);
    }

    [Fact]
    public void Ok_WithProcessingTime_IncludesTime()
    {
        // Arrange
        var frame = CreateTestFrame();
        
        // Act
        var result = ProcessingResult.Ok(frame, processingTimeMs: 15.5);
        
        // Assert
        Assert.Equal(15.5, result.ProcessingTimeMs);
    }

    [Fact]
    public void Fail_CreatesFailedResult()
    {
        // Arrange
        var frame = CreateTestFrame();
        
        // Act
        var result = ProcessingResult.Fail(frame, "Something went wrong");
        
        // Assert
        Assert.False(result.Success);
        Assert.Same(frame, result.Frame);
        Assert.Equal("Something went wrong", result.ErrorMessage);
    }

    [Fact]
    public void Fail_WithProcessingTime_IncludesTime()
    {
        // Arrange
        var frame = CreateTestFrame();
        
        // Act
        var result = ProcessingResult.Fail(frame, "Error", 5.0);
        
        // Assert
        Assert.Equal(5.0, result.ProcessingTimeMs);
    }

    [Fact]
    public void GetMetadata_ExistingKey_ReturnsTypedValue()
    {
        // Arrange
        var frame = CreateTestFrame();
        var metadata = new Dictionary<string, object>
        {
            ["IntValue"] = 42,
            ["StringValue"] = "hello",
            ["DoubleValue"] = 3.14
        };
        var result = ProcessingResult.Ok(frame, metadata);
        
        // Act & Assert
        Assert.Equal(42, result.GetMetadata<int>("IntValue"));
        Assert.Equal("hello", result.GetMetadata<string>("StringValue"));
        Assert.Equal(3.14, result.GetMetadata<double>("DoubleValue"));
    }

    [Fact]
    public void GetMetadata_NonExistentKey_ReturnsDefault()
    {
        // Arrange
        var frame = CreateTestFrame();
        var result = ProcessingResult.Ok(frame);
        
        // Act
        var value = result.GetMetadata<int>("NonExistent");
        
        // Assert
        Assert.Equal(0, value); // Default for int
    }

    [Fact]
    public void GetMetadata_WrongType_ReturnsDefault()
    {
        // Arrange
        var frame = CreateTestFrame();
        var metadata = new Dictionary<string, object> { ["Value"] = "not an int" };
        var result = ProcessingResult.Ok(frame, metadata);
        
        // Act
        var value = result.GetMetadata<int>("Value");
        
        // Assert
        Assert.Equal(0, value);
    }

    [Fact]
    public void Metadata_NullInput_CreatesEmptyDictionary()
    {
        // Arrange
        var frame = CreateTestFrame();
        
        // Act
        var result = ProcessingResult.Ok(frame, null);
        
        // Assert
        Assert.NotNull(result.Metadata);
        Assert.Empty(result.Metadata);
    }

    private static Frame CreateTestFrame()
    {
        var data = new byte[100 * 100 * 3];
        return new Frame(data, 100, 100, PixelFormat.Bgr24, 1);
    }
}
