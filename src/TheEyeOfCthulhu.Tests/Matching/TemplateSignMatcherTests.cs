using Xunit;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Core.Matching;
using TheEyeOfCthulhu.Sources.Matching;

namespace TheEyeOfCthulhu.Tests.Matching;

public class TemplateSignMatcherTests
{
    [Fact]
    public void Name_ReturnsTemplateMatch()
    {
        using var matcher = new TemplateSignMatcher();
        Assert.Equal("TemplateMatch", matcher.Name);
    }

    [Fact]
    public void Search_TemplateInImage_FindsMatch()
    {
        // Arrange
        using var matcher = new TemplateSignMatcher();
        
        // Créer une image avec un carré blanc au milieu
        var imageData = new byte[200 * 200 * 3];
        DrawWhiteSquare(imageData, 200, 200, 50, 50, 50, 50); // Carré à (50,50)
        using var image = new Frame(imageData, 200, 200, PixelFormat.Bgr24, 1);
        
        // Template = le même carré blanc
        var templateData = new byte[50 * 50 * 3];
        for (int i = 0; i < templateData.Length; i++) templateData[i] = 255; // Tout blanc
        using var templateFrame = new Frame(templateData, 50, 50, PixelFormat.Bgr24, 1);
        using var sign = new ElderSign("WhiteSquare", templateFrame) { MinScore = 0.9 };

        // Act
        var result = matcher.Search(image, sign);

        // Assert
        Assert.True(result.Found);
        Assert.NotNull(result.BestMatch);
        Assert.True(result.BestMatch.Score >= 0.9);
    }

    [Fact]
    public void Search_TemplateNotInImage_ReturnsLowScore()
    {
        // Arrange
        using var matcher = new TemplateSignMatcher();
        
        // Image avec pattern aléatoire
        var imageData = new byte[200 * 200 * 3];
        var rnd = new Random(42);
        rnd.NextBytes(imageData);
        using var image = new Frame(imageData, 200, 200, PixelFormat.Bgr24, 1);
        
        // Template avec pattern différent (damier)
        var templateData = new byte[50 * 50 * 3];
        for (int y = 0; y < 50; y++)
        {
            for (int x = 0; x < 50; x++)
            {
                var idx = (y * 50 + x) * 3;
                var isWhite = (x / 10 + y / 10) % 2 == 0;
                templateData[idx] = templateData[idx + 1] = templateData[idx + 2] = (byte)(isWhite ? 255 : 0);
            }
        }
        using var templateFrame = new Frame(templateData, 50, 50, PixelFormat.Bgr24, 1);
        using var sign = new ElderSign("Checkerboard", templateFrame) { MinScore = 0.95 };

        // Act
        var result = matcher.Search(image, sign);

        // Assert - Un pattern damier ne devrait pas matcher du bruit aléatoire avec un score > 0.95
        Assert.False(result.Found);
    }

    [Fact]
    public void Search_TemplateLargerThanImage_ReturnsEmpty()
    {
        // Arrange
        using var matcher = new TemplateSignMatcher();
        
        var imageData = new byte[50 * 50 * 3];
        using var image = new Frame(imageData, 50, 50, PixelFormat.Bgr24, 1);
        
        var templateData = new byte[100 * 100 * 3];
        using var templateFrame = new Frame(templateData, 100, 100, PixelFormat.Bgr24, 1);
        using var sign = new ElderSign("TooLarge", templateFrame);

        // Act
        var result = matcher.Search(image, sign);

        // Assert
        Assert.False(result.Found);
        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void SearchMultiple_FindsMultipleMatches()
    {
        // Arrange
        using var matcher = new TemplateSignMatcher();
        
        // Image avec deux carrés blancs
        var imageData = new byte[300 * 100 * 3];
        DrawWhiteSquare(imageData, 300, 100, 10, 10, 50, 50);   // Premier carré
        DrawWhiteSquare(imageData, 300, 100, 200, 10, 50, 50);  // Deuxième carré
        using var image = new Frame(imageData, 300, 100, PixelFormat.Bgr24, 1);
        
        // Template = carré blanc
        var templateData = new byte[50 * 50 * 3];
        for (int i = 0; i < templateData.Length; i++) templateData[i] = 255;
        using var templateFrame = new Frame(templateData, 50, 50, PixelFormat.Bgr24, 1);
        using var sign = new ElderSign("WhiteSquare", templateFrame) { MinScore = 0.8 };

        // Act
        var result = matcher.SearchMultiple(image, sign, 5);

        // Assert
        Assert.True(result.Count >= 2);
    }

    [Fact]
    public void SearchAll_SearchesMultipleElderSigns()
    {
        // Arrange
        using var matcher = new TemplateSignMatcher();
        
        var imageData = new byte[200 * 200 * 3];
        DrawWhiteSquare(imageData, 200, 200, 50, 50, 50, 50);
        using var image = new Frame(imageData, 200, 200, PixelFormat.Bgr24, 1);
        
        var template1Data = new byte[50 * 50 * 3];
        for (int i = 0; i < template1Data.Length; i++) template1Data[i] = 255;
        using var templateFrame1 = new Frame(template1Data, 50, 50, PixelFormat.Bgr24, 1);
        using var sign1 = new ElderSign("Sign1", templateFrame1) { MinScore = 0.8 };

        var template2Data = new byte[30 * 30 * 3];
        using var templateFrame2 = new Frame(template2Data, 30, 30, PixelFormat.Bgr24, 1);
        using var sign2 = new ElderSign("Sign2", templateFrame2) { MinScore = 0.8 };

        // Act
        var results = matcher.SearchAll(image, new[] { sign1, sign2 });

        // Assert
        Assert.Equal(2, results.Count);
        Assert.True(results.ContainsKey("Sign1"));
        Assert.True(results.ContainsKey("Sign2"));
    }

    [Fact]
    public void Search_RecordsSearchTime()
    {
        // Arrange
        using var matcher = new TemplateSignMatcher();
        var imageData = new byte[100 * 100 * 3];
        using var image = new Frame(imageData, 100, 100, PixelFormat.Bgr24, 1);
        var templateData = new byte[20 * 20 * 3];
        using var templateFrame = new Frame(templateData, 20, 20, PixelFormat.Bgr24, 1);
        using var sign = new ElderSign("Test", templateFrame);

        // Act
        var result = matcher.Search(image, sign);

        // Assert
        Assert.True(result.SearchTimeMs >= 0);
    }

    private static void DrawWhiteSquare(byte[] data, int imageWidth, int imageHeight, 
        int x, int y, int width, int height)
    {
        for (int row = y; row < y + height && row < imageHeight; row++)
        {
            for (int col = x; col < x + width && col < imageWidth; col++)
            {
                int idx = (row * imageWidth + col) * 3;
                data[idx] = 255;     // B
                data[idx + 1] = 255; // G
                data[idx + 2] = 255; // R
            }
        }
    }
}
