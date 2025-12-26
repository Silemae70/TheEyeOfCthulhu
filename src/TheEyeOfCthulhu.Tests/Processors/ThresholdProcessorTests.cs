using Xunit;
using TheEyeOfCthulhu.Sources.Processors;

namespace TheEyeOfCthulhu.Tests.Processors;

public class ThresholdProcessorTests
{
    [Fact]
    public void Name_ReturnsThreshold()
    {
        var processor = new ThresholdProcessor();
        Assert.Equal("Threshold", processor.Name);
    }

    [Fact]
    public void ThresholdValue_Default_Is127()
    {
        var processor = new ThresholdProcessor();
        Assert.Equal(127, processor.ThresholdValue);
    }

    [Fact]
    public void ThresholdValue_ValidRange_Succeeds()
    {
        var processor = new ThresholdProcessor();
        
        processor.ThresholdValue = 0;
        Assert.Equal(0, processor.ThresholdValue);
        
        processor.ThresholdValue = 127;
        Assert.Equal(127, processor.ThresholdValue);
        
        processor.ThresholdValue = 255;
        Assert.Equal(255, processor.ThresholdValue);
    }

    [Fact]
    public void ThresholdValue_Negative_ThrowsArgumentOutOfRangeException()
    {
        var processor = new ThresholdProcessor();
        Assert.Throws<ArgumentOutOfRangeException>(() => processor.ThresholdValue = -1);
    }

    [Fact]
    public void ThresholdValue_Above255_ThrowsArgumentOutOfRangeException()
    {
        var processor = new ThresholdProcessor();
        Assert.Throws<ArgumentOutOfRangeException>(() => processor.ThresholdValue = 256);
    }

    [Fact]
    public void MaxValue_Default_Is255()
    {
        var processor = new ThresholdProcessor();
        Assert.Equal(255, processor.MaxValue);
    }

    [Fact]
    public void MaxValue_ValidRange_Succeeds()
    {
        var processor = new ThresholdProcessor();
        
        processor.MaxValue = 0;
        Assert.Equal(0, processor.MaxValue);
        
        processor.MaxValue = 200;
        Assert.Equal(200, processor.MaxValue);
    }

    [Fact]
    public void MaxValue_OutOfRange_ThrowsArgumentOutOfRangeException()
    {
        var processor = new ThresholdProcessor();
        Assert.Throws<ArgumentOutOfRangeException>(() => processor.MaxValue = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => processor.MaxValue = 256);
    }

    [Fact]
    public void UseOtsu_Default_IsFalse()
    {
        var processor = new ThresholdProcessor();
        Assert.False(processor.UseOtsu);
    }
}
