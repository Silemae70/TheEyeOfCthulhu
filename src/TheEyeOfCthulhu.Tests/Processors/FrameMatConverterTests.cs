using Xunit;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Sources.Processors;

namespace TheEyeOfCthulhu.Tests.Processors;

public class FrameMatConverterTests
{
    [Fact]
    public void ToMat_Gray8Frame_CreatesCorrectMat()
    {
        // Arrange
        var data = new byte[100 * 100];
        for (int i = 0; i < data.Length; i++) data[i] = (byte)(i % 256);
        var frame = new Frame(data, 100, 100, PixelFormat.Gray8, 1);
        
        // Act
        using var mat = FrameMatConverter.ToMat(frame);
        
        // Assert
        Assert.Equal(100, mat.Width);
        Assert.Equal(100, mat.Height);
        Assert.Equal(1, mat.Channels());
    }

    [Fact]
    public void ToMat_Bgr24Frame_CreatesCorrectMat()
    {
        // Arrange
        var data = new byte[100 * 100 * 3];
        var frame = new Frame(data, 100, 100, PixelFormat.Bgr24, 1);
        
        // Act
        using var mat = FrameMatConverter.ToMat(frame);
        
        // Assert
        Assert.Equal(100, mat.Width);
        Assert.Equal(100, mat.Height);
        Assert.Equal(3, mat.Channels());
    }

    [Fact]
    public void ToMat_Bgra32Frame_CreatesCorrectMat()
    {
        // Arrange
        var data = new byte[100 * 100 * 4];
        var frame = new Frame(data, 100, 100, PixelFormat.Bgra32, 1);
        
        // Act
        using var mat = FrameMatConverter.ToMat(frame);
        
        // Assert
        Assert.Equal(100, mat.Width);
        Assert.Equal(100, mat.Height);
        Assert.Equal(4, mat.Channels());
    }

    [Fact]
    public void ToFrame_FromMat_CreatesCorrectFrame()
    {
        // Arrange
        var originalData = new byte[100 * 100 * 3];
        for (int i = 0; i < originalData.Length; i++) originalData[i] = (byte)(i % 256);
        var originalFrame = new Frame(originalData, 100, 100, PixelFormat.Bgr24, 42);
        using var mat = FrameMatConverter.ToMat(originalFrame);
        
        // Act
        var newFrame = FrameMatConverter.ToFrame(mat, 99);
        
        // Assert
        Assert.Equal(100, newFrame.Width);
        Assert.Equal(100, newFrame.Height);
        Assert.Equal(PixelFormat.Bgr24, newFrame.Format);
        Assert.Equal(99, newFrame.FrameNumber);
    }

    [Fact]
    public void RoundTrip_PreservesData()
    {
        // Arrange
        var originalData = new byte[50 * 50];
        for (int i = 0; i < originalData.Length; i++) originalData[i] = (byte)(i % 256);
        var originalFrame = new Frame(originalData, 50, 50, PixelFormat.Gray8, 1);
        
        // Act
        using var mat = FrameMatConverter.ToMat(originalFrame);
        var roundTripFrame = FrameMatConverter.ToFrame(mat, 1);
        
        // Assert
        Assert.Equal(originalFrame.Width, roundTripFrame.Width);
        Assert.Equal(originalFrame.Height, roundTripFrame.Height);
        Assert.Equal(originalFrame.Format, roundTripFrame.Format);
        
        for (int i = 0; i < originalData.Length; i++)
        {
            Assert.Equal(originalData[i], roundTripFrame.RawBuffer[i]);
        }
    }
}
