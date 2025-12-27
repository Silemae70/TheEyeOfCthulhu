using Xunit;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Core.Matching;

namespace TheEyeOfCthulhu.Tests.Matching;

public class ElderSignTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesElderSign()
    {
        // Arrange
        var template = CreateTestFrame(100, 100);

        // Act
        var sign = new ElderSign("TestSign", template);

        // Assert
        Assert.Equal("TestSign", sign.Name);
        Assert.Equal(100, sign.Width);
        Assert.Equal(100, sign.Height);
        Assert.Equal(50, sign.Anchor.X); // Center
        Assert.Equal(50, sign.Anchor.Y);
        Assert.Equal(0.7, sign.MinScore);
    }

    [Fact]
    public void Constructor_WithCustomAnchor_SetsAnchor()
    {
        // Arrange
        var template = CreateTestFrame(100, 100);
        var anchor = new Point(10, 20);

        // Act
        var sign = new ElderSign("TestSign", template, anchor);

        // Assert
        Assert.Equal(10, sign.Anchor.X);
        Assert.Equal(20, sign.Anchor.Y);
    }

    [Fact]
    public void Constructor_ClonesTemplate()
    {
        // Arrange
        var template = CreateTestFrame(50, 50);
        var sign = new ElderSign("TestSign", template);

        // Act
        template.RawBuffer[0] = 123;

        // Assert - Template should be independent
        Assert.NotEqual(123, sign.Template.RawBuffer[0]);
    }

    [Fact]
    public void Constructor_NullName_ThrowsArgumentNullException()
    {
        var template = CreateTestFrame(50, 50);
        Assert.Throws<ArgumentNullException>(() => new ElderSign(null!, template));
    }

    [Fact]
    public void Constructor_NullTemplate_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ElderSign("Test", null!));
    }

    [Fact]
    public void MinScore_CanBeModified()
    {
        // Arrange
        var template = CreateTestFrame(50, 50);
        var sign = new ElderSign("TestSign", template);

        // Act
        sign.MinScore = 0.9;

        // Assert
        Assert.Equal(0.9, sign.MinScore);
    }

    [Fact]
    public void Metadata_CanStoreValues()
    {
        // Arrange
        var template = CreateTestFrame(50, 50);
        var sign = new ElderSign("TestSign", template);

        // Act
        sign.Metadata["PartNumber"] = "ABC123";
        sign.Metadata["Tolerance"] = 0.5;

        // Assert
        Assert.Equal("ABC123", sign.Metadata["PartNumber"]);
        Assert.Equal(0.5, sign.Metadata["Tolerance"]);
    }

    [Fact]
    public void CreatedAt_IsSetOnCreation()
    {
        // Arrange
        var before = DateTime.UtcNow;
        var template = CreateTestFrame(50, 50);

        // Act
        var sign = new ElderSign("TestSign", template);
        var after = DateTime.UtcNow;

        // Assert
        Assert.True(sign.CreatedAt >= before);
        Assert.True(sign.CreatedAt <= after);
    }

    [Fact]
    public void Dispose_DisposesTemplate()
    {
        // Arrange
        var template = CreateTestFrame(50, 50);
        var sign = new ElderSign("TestSign", template);

        // Act & Assert (should not throw)
        sign.Dispose();
        sign.Dispose(); // Double dispose should be safe
    }

    private static Frame CreateTestFrame(int width, int height)
    {
        var data = new byte[width * height * 3];
        return new Frame(data, width, height, PixelFormat.Bgr24, 1);
    }
}

public class ElderSignMatchTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        // Arrange
        var template = CreateTestFrame(100, 100);
        var sign = new ElderSign("Test", template);
        var position = new PointF(50, 75);

        // Act
        var match = new ElderSignMatch(sign, position, 0.85);

        // Assert
        Assert.Same(sign, match.ElderSign);
        Assert.Equal(50, match.Position.X);
        Assert.Equal(75, match.Position.Y);
        Assert.Equal(0.85, match.Score);
    }

    [Fact]
    public void AnchorPosition_CalculatesCorrectly()
    {
        // Arrange
        var template = CreateTestFrame(100, 100);
        var sign = new ElderSign("Test", template); // Anchor at center (50, 50)
        var position = new PointF(100, 200);

        // Act
        var match = new ElderSignMatch(sign, position, 0.9);

        // Assert - Anchor should be at position + anchor offset
        Assert.Equal(150, match.AnchorPosition.X); // 100 + 50
        Assert.Equal(250, match.AnchorPosition.Y); // 200 + 50
    }

    [Fact]
    public void IsValid_ScoreAboveMin_ReturnsTrue()
    {
        // Arrange
        var template = CreateTestFrame(50, 50);
        var sign = new ElderSign("Test", template) { MinScore = 0.7 };

        // Act
        var match = new ElderSignMatch(sign, PointF.Zero, 0.8);

        // Assert
        Assert.True(match.IsValid);
    }

    [Fact]
    public void IsValid_ScoreBelowMin_ReturnsFalse()
    {
        // Arrange
        var template = CreateTestFrame(50, 50);
        var sign = new ElderSign("Test", template) { MinScore = 0.7 };

        // Act
        var match = new ElderSignMatch(sign, PointF.Zero, 0.5);

        // Assert
        Assert.False(match.IsValid);
    }

    [Fact]
    public void Score_ClampedTo0And1()
    {
        // Arrange
        var template = CreateTestFrame(50, 50);
        var sign = new ElderSign("Test", template);

        // Act
        var matchHigh = new ElderSignMatch(sign, PointF.Zero, 1.5);
        var matchLow = new ElderSignMatch(sign, PointF.Zero, -0.5);

        // Assert
        Assert.Equal(1.0, matchHigh.Score);
        Assert.Equal(0.0, matchLow.Score);
    }

    [Fact]
    public void BoundingBox_CalculatesCorrectly()
    {
        // Arrange
        var template = CreateTestFrame(80, 60);
        var sign = new ElderSign("Test", template);
        var position = new PointF(100, 200);

        // Act
        var match = new ElderSignMatch(sign, position, 0.9);
        var bbox = match.BoundingBox;

        // Assert
        Assert.Equal(100, bbox.X);
        Assert.Equal(200, bbox.Y);
        Assert.Equal(80, bbox.Width);
        Assert.Equal(60, bbox.Height);
    }

    private static Frame CreateTestFrame(int width, int height)
    {
        var data = new byte[width * height * 3];
        return new Frame(data, width, height, PixelFormat.Bgr24, 1);
    }
}

public class ElderSignSearchResultTests
{
    [Fact]
    public void Constructor_SortsMatchesByScoreDescending()
    {
        // Arrange
        var template = CreateTestFrame(50, 50);
        var sign = new ElderSign("Test", template);
        var matches = new[]
        {
            new ElderSignMatch(sign, PointF.Zero, 0.5),
            new ElderSignMatch(sign, PointF.Zero, 0.9),
            new ElderSignMatch(sign, PointF.Zero, 0.7)
        };

        // Act
        var result = new ElderSignSearchResult(matches, 10);

        // Assert
        Assert.Equal(0.9, result.Matches[0].Score);
        Assert.Equal(0.7, result.Matches[1].Score);
        Assert.Equal(0.5, result.Matches[2].Score);
    }

    [Fact]
    public void BestMatch_ReturnsHighestScore()
    {
        // Arrange
        var template = CreateTestFrame(50, 50);
        var sign = new ElderSign("Test", template);
        var matches = new[]
        {
            new ElderSignMatch(sign, PointF.Zero, 0.5),
            new ElderSignMatch(sign, PointF.Zero, 0.9)
        };

        // Act
        var result = new ElderSignSearchResult(matches, 10);

        // Assert
        Assert.Equal(0.9, result.BestMatch!.Score);
    }

    [Fact]
    public void BestMatch_EmptyMatches_ReturnsNull()
    {
        // Act
        var result = ElderSignSearchResult.Empty();

        // Assert
        Assert.Null(result.BestMatch);
    }

    [Fact]
    public void Found_ValidMatch_ReturnsTrue()
    {
        // Arrange
        var template = CreateTestFrame(50, 50);
        var sign = new ElderSign("Test", template) { MinScore = 0.7 };
        var matches = new[] { new ElderSignMatch(sign, PointF.Zero, 0.8) };

        // Act
        var result = new ElderSignSearchResult(matches, 10);

        // Assert
        Assert.True(result.Found);
    }

    [Fact]
    public void Found_NoValidMatch_ReturnsFalse()
    {
        // Arrange
        var template = CreateTestFrame(50, 50);
        var sign = new ElderSign("Test", template) { MinScore = 0.7 };
        var matches = new[] { new ElderSignMatch(sign, PointF.Zero, 0.5) };

        // Act
        var result = new ElderSignSearchResult(matches, 10);

        // Assert
        Assert.False(result.Found);
    }

    [Fact]
    public void Count_ReturnsMatchCount()
    {
        // Arrange
        var template = CreateTestFrame(50, 50);
        var sign = new ElderSign("Test", template);
        var matches = new[]
        {
            new ElderSignMatch(sign, PointF.Zero, 0.8),
            new ElderSignMatch(sign, PointF.Zero, 0.7)
        };

        // Act
        var result = new ElderSignSearchResult(matches, 10);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void SearchTimeMs_IsSet()
    {
        // Act
        var result = ElderSignSearchResult.Empty(15.5);

        // Assert
        Assert.Equal(15.5, result.SearchTimeMs);
    }

    private static Frame CreateTestFrame(int width, int height)
    {
        var data = new byte[width * height * 3];
        return new Frame(data, width, height, PixelFormat.Bgr24, 1);
    }
}

public class PointTests
{
    [Fact]
    public void Constructor_SetsValues()
    {
        var p = new Point(10, 20);
        Assert.Equal(10, p.X);
        Assert.Equal(20, p.Y);
    }

    [Fact]
    public void Zero_ReturnsOrigin()
    {
        var p = Point.Zero;
        Assert.Equal(0, p.X);
        Assert.Equal(0, p.Y);
    }

    [Fact]
    public void Addition_Works()
    {
        var a = new Point(10, 20);
        var b = new Point(5, 15);
        var c = a + b;
        Assert.Equal(15, c.X);
        Assert.Equal(35, c.Y);
    }

    [Fact]
    public void Subtraction_Works()
    {
        var a = new Point(10, 20);
        var b = new Point(5, 15);
        var c = a - b;
        Assert.Equal(5, c.X);
        Assert.Equal(5, c.Y);
    }
}

public class RectangleTests
{
    [Fact]
    public void Constructor_SetsValues()
    {
        var r = new Rectangle(10, 20, 100, 50);
        Assert.Equal(10, r.X);
        Assert.Equal(20, r.Y);
        Assert.Equal(100, r.Width);
        Assert.Equal(50, r.Height);
    }

    [Fact]
    public void Edges_CalculateCorrectly()
    {
        var r = new Rectangle(10, 20, 100, 50);
        Assert.Equal(10, r.Left);
        Assert.Equal(20, r.Top);
        Assert.Equal(110, r.Right);
        Assert.Equal(70, r.Bottom);
    }

    [Fact]
    public void Center_CalculatesCorrectly()
    {
        var r = new Rectangle(10, 20, 100, 50);
        Assert.Equal(60, r.Center.X);
        Assert.Equal(45, r.Center.Y);
    }

    [Fact]
    public void Contains_PointInside_ReturnsTrue()
    {
        var r = new Rectangle(10, 10, 100, 100);
        Assert.True(r.Contains(new Point(50, 50)));
    }

    [Fact]
    public void Contains_PointOutside_ReturnsFalse()
    {
        var r = new Rectangle(10, 10, 100, 100);
        Assert.False(r.Contains(new Point(5, 5)));
        Assert.False(r.Contains(new Point(150, 150)));
    }
}
