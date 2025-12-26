using Xunit;
using TheEyeOfCthulhu.Sources.Processors;

namespace TheEyeOfCthulhu.Tests.Processors;

public class GaussianBlurProcessorTests
{
    [Fact]
    public void Name_ReturnsGaussianBlur()
    {
        var processor = new GaussianBlurProcessor();
        Assert.Equal("GaussianBlur", processor.Name);
    }

    [Fact]
    public void KernelSize_Default_Is5()
    {
        var processor = new GaussianBlurProcessor();
        Assert.Equal(5, processor.KernelSize);
    }

    [Fact]
    public void KernelSize_ValidOddValue_Succeeds()
    {
        var processor = new GaussianBlurProcessor();
        
        processor.KernelSize = 1;
        Assert.Equal(1, processor.KernelSize);
        
        processor.KernelSize = 3;
        Assert.Equal(3, processor.KernelSize);
        
        processor.KernelSize = 7;
        Assert.Equal(7, processor.KernelSize);
        
        processor.KernelSize = 15;
        Assert.Equal(15, processor.KernelSize);
    }

    [Fact]
    public void KernelSize_EvenValue_ThrowsArgumentException()
    {
        var processor = new GaussianBlurProcessor();
        
        Assert.Throws<ArgumentException>(() => processor.KernelSize = 2);
        Assert.Throws<ArgumentException>(() => processor.KernelSize = 4);
        Assert.Throws<ArgumentException>(() => processor.KernelSize = 6);
    }

    [Fact]
    public void KernelSize_Zero_ThrowsArgumentOutOfRangeException()
    {
        var processor = new GaussianBlurProcessor();
        
        Assert.Throws<ArgumentOutOfRangeException>(() => processor.KernelSize = 0);
    }

    [Fact]
    public void KernelSize_Negative_ThrowsArgumentOutOfRangeException()
    {
        var processor = new GaussianBlurProcessor();
        
        Assert.Throws<ArgumentOutOfRangeException>(() => processor.KernelSize = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => processor.KernelSize = -5);
    }

    [Fact]
    public void SigmaX_Default_IsZero()
    {
        var processor = new GaussianBlurProcessor();
        Assert.Equal(0, processor.SigmaX);
    }

    [Fact]
    public void SigmaX_CanBeSet()
    {
        var processor = new GaussianBlurProcessor { SigmaX = 1.5 };
        Assert.Equal(1.5, processor.SigmaX);
    }
}
