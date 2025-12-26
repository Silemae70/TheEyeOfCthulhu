using Xunit;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Sources.Processors;

namespace TheEyeOfCthulhu.Tests.Processors;

public class GrayscaleProcessorTests
{
    [Fact]
    public void Name_ReturnsGrayscale()
    {
        var processor = new GrayscaleProcessor();
        Assert.Equal("Grayscale", processor.Name);
    }

    [Fact]
    public void IsEnabled_DefaultTrue()
    {
        var processor = new GrayscaleProcessor();
        Assert.True(processor.IsEnabled);
    }

    [Fact]
    public void Process_Disabled_ReturnsInputFrame()
    {
        // Arrange
        var processor = new GrayscaleProcessor { IsEnabled = false };
        var frame = CreateBgrFrame();
        
        // Act
        var result = processor.Process(frame);
        
        // Assert
        Assert.True(result.Success);
        Assert.Same(frame, result.Frame);
    }

    [Fact]
    public void Process_Gray8Input_ReturnsSameFrame()
    {
        // Arrange
        var processor = new GrayscaleProcessor();
        var frame = CreateGrayFrame();
        
        // Act
        var result = processor.Process(frame);
        
        // Assert
        Assert.True(result.Success);
        Assert.Same(frame, result.Frame);
    }

    [Fact]
    public void Process_BgrInput_ReturnsGrayFrame()
    {
        // Arrange
        var processor = new GrayscaleProcessor();
        var frame = CreateBgrFrame();
        
        // Act
        var result = processor.Process(frame);
        
        // Assert
        Assert.True(result.Success);
        Assert.Equal(PixelFormat.Gray8, result.Frame.Format);
        Assert.Equal(frame.Width, result.Frame.Width);
        Assert.Equal(frame.Height, result.Frame.Height);
    }

    private static Frame CreateBgrFrame()
    {
        var data = new byte[100 * 100 * 3];
        return new Frame(data, 100, 100, PixelFormat.Bgr24, 1);
    }

    private static Frame CreateGrayFrame()
    {
        var data = new byte[100 * 100];
        return new Frame(data, 100, 100, PixelFormat.Gray8, 1);
    }
}
