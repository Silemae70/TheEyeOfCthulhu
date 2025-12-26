using Xunit;
using TheEyeOfCthulhu.Sources.Processors;

namespace TheEyeOfCthulhu.Tests.Processors;

public class CannyEdgeProcessorTests
{
    [Fact]
    public void Name_ReturnsCannyEdge()
    {
        var processor = new CannyEdgeProcessor();
        Assert.Equal("CannyEdge", processor.Name);
    }

    [Fact]
    public void Threshold1_Default_Is50()
    {
        var processor = new CannyEdgeProcessor();
        Assert.Equal(50, processor.Threshold1);
    }

    [Fact]
    public void Threshold2_Default_Is150()
    {
        var processor = new CannyEdgeProcessor();
        Assert.Equal(150, processor.Threshold2);
    }

    [Fact]
    public void Threshold1_ValidRange_Succeeds()
    {
        var processor = new CannyEdgeProcessor();
        
        processor.Threshold1 = 0;
        Assert.Equal(0, processor.Threshold1);
        
        processor.Threshold1 = 255;
        Assert.Equal(255, processor.Threshold1);
    }

    [Fact]
    public void Threshold1_OutOfRange_ThrowsArgumentOutOfRangeException()
    {
        var processor = new CannyEdgeProcessor();
        Assert.Throws<ArgumentOutOfRangeException>(() => processor.Threshold1 = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => processor.Threshold1 = 256);
    }

    [Fact]
    public void Threshold2_OutOfRange_ThrowsArgumentOutOfRangeException()
    {
        var processor = new CannyEdgeProcessor();
        Assert.Throws<ArgumentOutOfRangeException>(() => processor.Threshold2 = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => processor.Threshold2 = 256);
    }

    [Fact]
    public void ApertureSize_Default_Is3()
    {
        var processor = new CannyEdgeProcessor();
        Assert.Equal(3, processor.ApertureSize);
    }

    [Fact]
    public void ApertureSize_ValidValues_Succeeds()
    {
        var processor = new CannyEdgeProcessor();
        
        processor.ApertureSize = 3;
        Assert.Equal(3, processor.ApertureSize);
        
        processor.ApertureSize = 5;
        Assert.Equal(5, processor.ApertureSize);
        
        processor.ApertureSize = 7;
        Assert.Equal(7, processor.ApertureSize);
    }

    [Fact]
    public void ApertureSize_InvalidValues_ThrowsArgumentOutOfRangeException()
    {
        var processor = new CannyEdgeProcessor();
        
        Assert.Throws<ArgumentOutOfRangeException>(() => processor.ApertureSize = 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => processor.ApertureSize = 2);
        Assert.Throws<ArgumentOutOfRangeException>(() => processor.ApertureSize = 4);
        Assert.Throws<ArgumentOutOfRangeException>(() => processor.ApertureSize = 6);
        Assert.Throws<ArgumentOutOfRangeException>(() => processor.ApertureSize = 9);
    }
}
