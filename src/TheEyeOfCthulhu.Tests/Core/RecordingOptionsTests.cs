using Xunit;
using TheEyeOfCthulhu.Core;

namespace TheEyeOfCthulhu.Tests.Core;

public class RecordingOptionsTests
{
    [Fact]
    public void GenerateFilePath_DefaultOptions_IncludesAllParts()
    {
        // Arrange
        var options = new RecordingOptions
        {
            OutputDirectory = "captures",
            FilePrefix = "frame",
            Format = ImageFormat.Png,
            IncludeTimestamp = true,
            IncludeFrameNumber = true
        };
        var frame = CreateTestFrame(42);
        
        // Act
        var path = options.GenerateFilePath(frame);
        
        // Assert
        Assert.StartsWith("captures", path);
        Assert.Contains("frame_", path);
        Assert.Contains("_f000042", path);
        Assert.EndsWith(".png", path);
    }

    [Fact]
    public void GenerateFilePath_JpegFormat_HasJpgExtension()
    {
        // Arrange
        var options = new RecordingOptions { Format = ImageFormat.Jpeg };
        var frame = CreateTestFrame(1);
        
        // Act
        var path = options.GenerateFilePath(frame);
        
        // Assert
        Assert.EndsWith(".jpg", path);
    }

    [Fact]
    public void GenerateFilePath_BmpFormat_HasBmpExtension()
    {
        // Arrange
        var options = new RecordingOptions { Format = ImageFormat.Bmp };
        var frame = CreateTestFrame(1);
        
        // Act
        var path = options.GenerateFilePath(frame);
        
        // Assert
        Assert.EndsWith(".bmp", path);
    }

    [Fact]
    public void GenerateFilePath_TiffFormat_HasTiffExtension()
    {
        // Arrange
        var options = new RecordingOptions { Format = ImageFormat.Tiff };
        var frame = CreateTestFrame(1);
        
        // Act
        var path = options.GenerateFilePath(frame);
        
        // Assert
        Assert.EndsWith(".tiff", path);
    }

    [Fact]
    public void GenerateFilePath_NoTimestamp_ExcludesTimestamp()
    {
        // Arrange
        var options = new RecordingOptions
        {
            IncludeTimestamp = false,
            IncludeFrameNumber = true,
            FilePrefix = "test"
        };
        var frame = CreateTestFrame(1);
        
        // Act
        var path = options.GenerateFilePath(frame);
        var filename = Path.GetFileName(path);
        
        // Assert
        // Should be like "test_f000001.png" without timestamp
        Assert.Equal("test_f000001.png", filename);
    }

    [Fact]
    public void GenerateFilePath_NoFrameNumber_ExcludesFrameNumber()
    {
        // Arrange
        var options = new RecordingOptions
        {
            IncludeTimestamp = false,
            IncludeFrameNumber = false,
            FilePrefix = "snapshot"
        };
        var frame = CreateTestFrame(1);
        
        // Act
        var path = options.GenerateFilePath(frame);
        var filename = Path.GetFileName(path);
        
        // Assert
        Assert.Equal("snapshot.png", filename);
    }

    [Fact]
    public void GenerateFilePath_CustomPrefix_UsesPrefix()
    {
        // Arrange
        var options = new RecordingOptions
        {
            FilePrefix = "custom_prefix",
            IncludeTimestamp = false,
            IncludeFrameNumber = false
        };
        var frame = CreateTestFrame(1);
        
        // Act
        var path = options.GenerateFilePath(frame);
        
        // Assert
        Assert.Contains("custom_prefix", path);
    }

    [Fact]
    public void GenerateFilePath_CustomOutputDirectory_UsesDirectory()
    {
        // Arrange
        var options = new RecordingOptions
        {
            OutputDirectory = @"C:\temp\vision",
            IncludeTimestamp = false,
            IncludeFrameNumber = false
        };
        var frame = CreateTestFrame(1);
        
        // Act
        var path = options.GenerateFilePath(frame);
        
        // Assert
        Assert.StartsWith(@"C:\temp\vision", path);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Act
        var options = new RecordingOptions();
        
        // Assert
        Assert.Equal("captures", options.OutputDirectory);
        Assert.Equal("frame", options.FilePrefix);
        Assert.Equal(ImageFormat.Png, options.Format);
        Assert.Equal(95, options.JpegQuality);
        Assert.True(options.IncludeTimestamp);
        Assert.True(options.IncludeFrameNumber);
    }

    private static Frame CreateTestFrame(long frameNumber)
    {
        var data = new byte[100 * 100 * 3];
        return new Frame(data, 100, 100, PixelFormat.Bgr24, frameNumber);
    }
}
