using Xunit;
using TheEyeOfCthulhu.Core;

namespace TheEyeOfCthulhu.Tests.Core;

public class FrameSourceFactoryTests
{
    [Fact]
    public void RegisterProvider_ValidProvider_Succeeds()
    {
        // Arrange
        var factory = new FrameSourceFactory();
        var provider = new FakeSourceProvider();
        
        // Act
        factory.RegisterProvider(provider);
        
        // Assert
        Assert.True(factory.IsSourceTypeSupported("Fake"));
    }

    [Fact]
    public void RegisterProvider_DuplicateType_ThrowsInvalidOperationException()
    {
        // Arrange
        var factory = new FrameSourceFactory();
        var provider1 = new FakeSourceProvider();
        var provider2 = new FakeSourceProvider();
        factory.RegisterProvider(provider1);
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => factory.RegisterProvider(provider2));
    }

    [Fact]
    public void RegisterProvider_NullProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new FrameSourceFactory();
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => factory.RegisterProvider(null!));
    }

    [Fact]
    public void UnregisterProvider_ExistingType_ReturnsTrue()
    {
        // Arrange
        var factory = new FrameSourceFactory();
        factory.RegisterProvider(new FakeSourceProvider());
        
        // Act
        var result = factory.UnregisterProvider("Fake");
        
        // Assert
        Assert.True(result);
        Assert.False(factory.IsSourceTypeSupported("Fake"));
    }

    [Fact]
    public void UnregisterProvider_NonExistingType_ReturnsFalse()
    {
        // Arrange
        var factory = new FrameSourceFactory();
        
        // Act
        var result = factory.UnregisterProvider("NonExistent");
        
        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetAvailableSourceTypes_ReturnsRegisteredTypes()
    {
        // Arrange
        var factory = new FrameSourceFactory();
        factory.RegisterProvider(new FakeSourceProvider());
        
        // Act
        var types = factory.GetAvailableSourceTypes();
        
        // Assert
        Assert.Single(types);
        Assert.Contains("Fake", types);
    }

    [Fact]
    public void Create_ValidConfiguration_ReturnsSource()
    {
        // Arrange
        var factory = new FrameSourceFactory();
        factory.RegisterProvider(new FakeSourceProvider());
        var config = new FakeConfiguration();
        
        // Act
        var source = factory.Create(config);
        
        // Assert
        Assert.NotNull(source);
        Assert.Equal("Fake", source.SourceType);
    }

    [Fact]
    public void Create_UnknownSourceType_ThrowsNotSupportedException()
    {
        // Arrange
        var factory = new FrameSourceFactory();
        var config = new FakeConfiguration();
        
        // Act & Assert
        Assert.Throws<NotSupportedException>(() => factory.Create(config));
    }

    [Fact]
    public void Create_NullConfiguration_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new FrameSourceFactory();
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => factory.Create(null!));
    }

    [Fact]
    public void TryCreate_ValidConfiguration_ReturnsSource()
    {
        // Arrange
        var factory = new FrameSourceFactory();
        factory.RegisterProvider(new FakeSourceProvider());
        var config = new FakeConfiguration();
        
        // Act
        var source = factory.TryCreate(config);
        
        // Assert
        Assert.NotNull(source);
    }

    [Fact]
    public void TryCreate_InvalidConfiguration_ReturnsNull()
    {
        // Arrange
        var factory = new FrameSourceFactory();
        var config = new FakeConfiguration();
        
        // Act
        var source = factory.TryCreate(config);
        
        // Assert
        Assert.Null(source);
    }

    [Fact]
    public void IsSourceTypeSupported_CaseInsensitive()
    {
        // Arrange
        var factory = new FrameSourceFactory();
        factory.RegisterProvider(new FakeSourceProvider());
        
        // Act & Assert
        Assert.True(factory.IsSourceTypeSupported("Fake"));
        Assert.True(factory.IsSourceTypeSupported("fake"));
        Assert.True(factory.IsSourceTypeSupported("FAKE"));
    }

    #region Test Helpers

    private class FakeConfiguration : SourceConfiguration
    {
        public override string SourceType => "Fake";
    }

    private class FakeSourceProvider : IFrameSourceProvider
    {
        public string SourceType => "Fake";

        public IFrameSource Create(SourceConfiguration configuration)
        {
            return new FakeSource();
        }

        public bool CanHandle(SourceConfiguration configuration)
        {
            return configuration is FakeConfiguration;
        }
    }

    private class FakeSource : IFrameSource
    {
        public string Name => "FakeSource";
        public string SourceType => "Fake";
        public SourceState State => SourceState.Created;
        public int Width => 640;
        public int Height => 480;
        public double TargetFps => 30;
        public double ActualFps => 0;
        public long TotalFramesCaptured => 0;

        public event EventHandler<FrameEventArgs>? FrameReceived;
        public event EventHandler<SourceErrorEventArgs>? ErrorOccurred;
        public event EventHandler<SourceState>? StateChanged;

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Frame?> CaptureFrameAsync(CancellationToken cancellationToken = default) => Task.FromResult<Frame?>(null);
        public void Dispose() { }
    }

    #endregion
}
