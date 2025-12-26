using Xunit;
using OpenCvSharp;
using TheEyeOfCthulhu.Sources.Processors;

namespace TheEyeOfCthulhu.Tests.Processors;

public class ContourDetectorProcessorTests
{
    [Fact]
    public void Name_ReturnsContourDetector()
    {
        var processor = new ContourDetectorProcessor();
        Assert.Equal("ContourDetector", processor.Name);
    }

    [Fact]
    public void MinArea_Default_Is100()
    {
        var processor = new ContourDetectorProcessor();
        Assert.Equal(100, processor.MinArea);
    }

    [Fact]
    public void MinArea_ValidValues_Succeeds()
    {
        var processor = new ContourDetectorProcessor();
        
        processor.MinArea = 0;
        Assert.Equal(0, processor.MinArea);
        
        processor.MinArea = 500;
        Assert.Equal(500, processor.MinArea);
        
        processor.MinArea = 10000;
        Assert.Equal(10000, processor.MinArea);
    }

    [Fact]
    public void MinArea_Negative_ThrowsArgumentOutOfRangeException()
    {
        var processor = new ContourDetectorProcessor();
        Assert.Throws<ArgumentOutOfRangeException>(() => processor.MinArea = -1);
    }

    [Fact]
    public void DrawContours_Default_IsTrue()
    {
        var processor = new ContourDetectorProcessor();
        Assert.True(processor.DrawContours);
    }

    [Fact]
    public void ContourColor_Default_IsGreen()
    {
        var processor = new ContourDetectorProcessor();
        Assert.Equal(new Scalar(0, 255, 0), processor.ContourColor);
    }

    [Fact]
    public void CentroidColor_Default_IsRed()
    {
        var processor = new ContourDetectorProcessor();
        Assert.Equal(new Scalar(0, 0, 255), processor.CentroidColor);
    }

    [Fact]
    public void ContourThickness_Default_Is2()
    {
        var processor = new ContourDetectorProcessor();
        Assert.Equal(2, processor.ContourThickness);
    }

    [Fact]
    public void RetrievalMode_Default_IsExternal()
    {
        var processor = new ContourDetectorProcessor();
        Assert.Equal(RetrievalModes.External, processor.RetrievalMode);
    }

    [Fact]
    public void ApproximationMethod_Default_IsApproxSimple()
    {
        var processor = new ContourDetectorProcessor();
        Assert.Equal(ContourApproximationModes.ApproxSimple, processor.ApproximationMethod);
    }

    [Fact]
    public void ContourColor_CanBeChanged()
    {
        var processor = new ContourDetectorProcessor();
        var newColor = new Scalar(255, 0, 0); // Blue
        
        processor.ContourColor = newColor;
        
        Assert.Equal(newColor, processor.ContourColor);
    }
}
