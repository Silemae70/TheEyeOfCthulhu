using Xunit;
using TheEyeOfCthulhu.Core;

namespace TheEyeOfCthulhu.Tests.Core;

public class FrameTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesFrame()
    {
        // Arrange
        var data = new byte[640 * 480 * 3]; // BGR24
        
        // Act
        var frame = new Frame(data, 640, 480, PixelFormat.Bgr24, 1);
        
        // Assert
        Assert.Equal(640, frame.Width);
        Assert.Equal(480, frame.Height);
        Assert.Equal(PixelFormat.Bgr24, frame.Format);
        Assert.Equal(1, frame.FrameNumber);
        Assert.Equal(640 * 3, frame.Stride);
    }

    [Fact]
    public void Constructor_WithCustomStride_UsesProvidedStride()
    {
        // Arrange
        var stride = 2048; // Padded stride
        var data = new byte[stride * 480];
        
        // Act
        var frame = new Frame(data, 640, 480, PixelFormat.Bgr24, 1, stride);
        
        // Assert
        Assert.Equal(stride, frame.Stride);
    }

    [Fact]
    public void Constructor_Gray8Format_CalculatesCorrectStride()
    {
        // Arrange
        var data = new byte[640 * 480];
        
        // Act
        var frame = new Frame(data, 640, 480, PixelFormat.Gray8, 1);
        
        // Assert
        Assert.Equal(640, frame.Stride); // 1 byte per pixel
    }

    [Fact]
    public void Constructor_Bgra32Format_CalculatesCorrectStride()
    {
        // Arrange
        var data = new byte[640 * 480 * 4];
        
        // Act
        var frame = new Frame(data, 640, 480, PixelFormat.Bgra32, 1);
        
        // Assert
        Assert.Equal(640 * 4, frame.Stride); // 4 bytes per pixel
    }

    [Fact]
    public void Constructor_DataTooSmall_ThrowsArgumentException()
    {
        // Arrange
        var data = new byte[100]; // Too small for 640x480
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            new Frame(data, 640, 480, PixelFormat.Bgr24, 1));
    }

    [Fact]
    public void Constructor_NullData_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new Frame(null!, 640, 480, PixelFormat.Bgr24, 1));
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        // Arrange
        var data = new byte[640 * 480 * 3];
        data[0] = 255;
        var original = new Frame(data, 640, 480, PixelFormat.Bgr24, 42);
        
        // Act
        var clone = original.Clone();
        
        // Assert
        Assert.NotSame(original, clone);
        Assert.Equal(original.Width, clone.Width);
        Assert.Equal(original.Height, clone.Height);
        Assert.Equal(original.Format, clone.Format);
        Assert.Equal(original.FrameNumber, clone.FrameNumber);
        Assert.Equal(original.RawBuffer[0], clone.RawBuffer[0]);
        
        // Modify original, clone should not change
        original.RawBuffer[0] = 0;
        Assert.Equal(255, clone.RawBuffer[0]);
    }

    [Fact]
    public void RawBuffer_ReturnsInternalData()
    {
        // Arrange
        var data = new byte[640 * 480 * 3];
        for (int i = 0; i < 10; i++) data[i] = (byte)i;
        var frame = new Frame(data, 640, 480, PixelFormat.Bgr24, 1);
        
        // Act
        var buffer = frame.RawBuffer;
        
        // Assert
        Assert.Equal(data.Length, buffer.Length);
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(i, buffer[i]);
        }
    }

    [Fact]
    public void Timestamp_IsSetOnCreation()
    {
        // Arrange
        var before = DateTime.UtcNow;
        var data = new byte[640 * 480 * 3];
        
        // Act
        var frame = new Frame(data, 640, 480, PixelFormat.Bgr24, 1);
        var after = DateTime.UtcNow;
        
        // Assert
        Assert.True(frame.Timestamp >= before);
        Assert.True(frame.Timestamp <= after);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var data = new byte[640 * 480 * 3];
        var frame = new Frame(data, 640, 480, PixelFormat.Bgr24, 1);
        
        // Act & Assert (should not throw)
        frame.Dispose();
        frame.Dispose();
        frame.Dispose();
    }
}
