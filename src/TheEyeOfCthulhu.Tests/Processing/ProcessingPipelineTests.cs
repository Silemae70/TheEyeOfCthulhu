using Xunit;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Core.Processing;

namespace TheEyeOfCthulhu.Tests.Processing;

public class ProcessingPipelineTests
{
    [Fact]
    public void Constructor_WithName_SetsName()
    {
        // Act
        var pipeline = new ProcessingPipeline("Test Pipeline");
        
        // Assert
        Assert.Equal("Test Pipeline", pipeline.Name);
    }

    [Fact]
    public void Add_Processor_AddsToList()
    {
        // Arrange
        var pipeline = new ProcessingPipeline();
        var processor = new FakeProcessor("Test");
        
        // Act
        pipeline.Add(processor);
        
        // Assert
        Assert.Single(pipeline.Processors);
        Assert.Same(processor, pipeline.Processors[0]);
    }

    [Fact]
    public void Add_FluentApi_ReturnsPipeline()
    {
        // Arrange
        var pipeline = new ProcessingPipeline();
        
        // Act
        var result = pipeline
            .Add(new FakeProcessor("A"))
            .Add(new FakeProcessor("B"))
            .Add(new FakeProcessor("C"));
        
        // Assert
        Assert.Same(pipeline, result);
        Assert.Equal(3, pipeline.Processors.Count);
    }

    [Fact]
    public void Insert_AtIndex_InsertsCorrectly()
    {
        // Arrange
        var pipeline = new ProcessingPipeline()
            .Add(new FakeProcessor("A"))
            .Add(new FakeProcessor("C"));
        
        // Act
        pipeline.Insert(1, new FakeProcessor("B"));
        
        // Assert
        Assert.Equal(3, pipeline.Processors.Count);
        Assert.Equal("A", pipeline.Processors[0].Name);
        Assert.Equal("B", pipeline.Processors[1].Name);
        Assert.Equal("C", pipeline.Processors[2].Name);
    }

    [Fact]
    public void Remove_ByInstance_RemovesProcessor()
    {
        // Arrange
        var processor = new FakeProcessor("ToRemove");
        var pipeline = new ProcessingPipeline()
            .Add(new FakeProcessor("Keep"))
            .Add(processor);
        
        // Act
        var result = pipeline.Remove(processor);
        
        // Assert
        Assert.True(result);
        Assert.Single(pipeline.Processors);
        Assert.Equal("Keep", pipeline.Processors[0].Name);
    }

    [Fact]
    public void Remove_ByName_RemovesProcessor()
    {
        // Arrange
        var pipeline = new ProcessingPipeline()
            .Add(new FakeProcessor("Keep"))
            .Add(new FakeProcessor("ToRemove"));
        
        // Act
        var result = pipeline.Remove("ToRemove");
        
        // Assert
        Assert.True(result);
        Assert.Single(pipeline.Processors);
    }

    [Fact]
    public void Remove_NonExistent_ReturnsFalse()
    {
        // Arrange
        var pipeline = new ProcessingPipeline();
        
        // Act
        var result = pipeline.Remove("NonExistent");
        
        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Clear_RemovesAllProcessors()
    {
        // Arrange
        var pipeline = new ProcessingPipeline()
            .Add(new FakeProcessor("A"))
            .Add(new FakeProcessor("B"));
        
        // Act
        pipeline.Clear();
        
        // Assert
        Assert.Empty(pipeline.Processors);
    }

    [Fact]
    public void Process_EmptyPipeline_ReturnsInputFrame()
    {
        // Arrange
        var pipeline = new ProcessingPipeline();
        var frame = CreateTestFrame();
        
        // Act
        var result = pipeline.Process(frame);
        
        // Assert
        Assert.Same(frame, result.FinalFrame);
        Assert.Empty(result.StepResults);
    }

    [Fact]
    public void Process_SingleProcessor_AppliesProcessor()
    {
        // Arrange
        var processor = new FakeProcessor("Test", modifyFrame: true);
        var pipeline = new ProcessingPipeline().Add(processor);
        var frame = CreateTestFrame();
        
        // Act
        var result = pipeline.Process(frame);
        
        // Assert
        Assert.Single(result.StepResults);
        Assert.Equal("Test", result.StepResults[0].ProcessorName);
        Assert.True(result.Success);
    }

    [Fact]
    public void Process_DisabledProcessor_SkipsProcessor()
    {
        // Arrange
        var processor = new FakeProcessor("Disabled") { IsEnabled = false };
        var pipeline = new ProcessingPipeline().Add(processor);
        var frame = CreateTestFrame();
        
        // Act
        var result = pipeline.Process(frame);
        
        // Assert
        Assert.Single(result.StepResults);
        Assert.Same(frame, result.FinalFrame); // Frame unchanged
    }

    [Fact]
    public void Process_MultipleProcessors_ExecutesInOrder()
    {
        // Arrange
        var executionOrder = new List<string>();
        var pipeline = new ProcessingPipeline()
            .Add(new FakeProcessor("First", onProcess: () => executionOrder.Add("First")))
            .Add(new FakeProcessor("Second", onProcess: () => executionOrder.Add("Second")))
            .Add(new FakeProcessor("Third", onProcess: () => executionOrder.Add("Third")));
        var frame = CreateTestFrame();
        
        // Act
        pipeline.Process(frame);
        
        // Assert
        Assert.Equal(new[] { "First", "Second", "Third" }, executionOrder);
    }

    [Fact]
    public void Process_WithMetadata_AggregatesMetadata()
    {
        // Arrange
        var pipeline = new ProcessingPipeline()
            .Add(new FakeProcessor("A", metadata: new() { ["KeyA"] = 1 }))
            .Add(new FakeProcessor("B", metadata: new() { ["KeyB"] = 2 }));
        var frame = CreateTestFrame();
        
        // Act
        var result = pipeline.Process(frame);
        
        // Assert
        Assert.Equal(1, result.AllMetadata["A.KeyA"]);
        Assert.Equal(2, result.AllMetadata["B.KeyB"]);
    }

    [Fact]
    public void GetProcessor_ByName_ReturnsProcessor()
    {
        // Arrange
        var target = new FakeProcessor("Target");
        var pipeline = new ProcessingPipeline()
            .Add(new FakeProcessor("Other"))
            .Add(target);
        
        // Act
        var found = pipeline.GetProcessor("Target");
        
        // Assert
        Assert.Same(target, found);
    }

    [Fact]
    public void GetProcessor_NonExistent_ReturnsNull()
    {
        // Arrange
        var pipeline = new ProcessingPipeline();
        
        // Act
        var found = pipeline.GetProcessor("NonExistent");
        
        // Assert
        Assert.Null(found);
    }

    [Fact]
    public void GetProcessor_Generic_ReturnsCastedProcessor()
    {
        // Arrange
        var target = new FakeProcessor("Target");
        var pipeline = new ProcessingPipeline().Add(target);
        
        // Act
        var found = pipeline.GetProcessor<FakeProcessor>("Target");
        
        // Assert
        Assert.Same(target, found);
    }

    [Fact]
    public void TotalProcessingTime_SumsAllSteps()
    {
        // Arrange
        var pipeline = new ProcessingPipeline()
            .Add(new FakeProcessor("A"))
            .Add(new FakeProcessor("B"));
        var frame = CreateTestFrame();
        
        // Act
        var result = pipeline.Process(frame);
        
        // Assert
        Assert.True(result.TotalProcessingTimeMs >= 0);
        Assert.Equal(
            result.StepResults.Sum(r => r.Result.ProcessingTimeMs),
            result.TotalProcessingTimeMs);
    }

    [Fact]
    public void Dispose_DisposesAllProcessors()
    {
        // Arrange
        var disposed = new List<string>();
        var pipeline = new ProcessingPipeline()
            .Add(new FakeProcessor("A", onDispose: () => disposed.Add("A")))
            .Add(new FakeProcessor("B", onDispose: () => disposed.Add("B")));
        
        // Act
        pipeline.Dispose();
        
        // Assert
        Assert.Equal(new[] { "A", "B" }, disposed);
        Assert.Empty(pipeline.Processors);
    }

    #region Test Helpers

    private static Frame CreateTestFrame()
    {
        var data = new byte[100 * 100 * 3];
        return new Frame(data, 100, 100, PixelFormat.Bgr24, 1);
    }

    private class FakeProcessor : FrameProcessorBase
    {
        private readonly bool _modifyFrame;
        private readonly Dictionary<string, object>? _metadata;
        private readonly Action? _onProcess;
        private readonly Action? _onDispose;

        public override string Name { get; }

        public FakeProcessor(
            string name,
            bool modifyFrame = false,
            Dictionary<string, object>? metadata = null,
            Action? onProcess = null,
            Action? onDispose = null)
        {
            Name = name;
            _modifyFrame = modifyFrame;
            _metadata = metadata;
            _onProcess = onProcess;
            _onDispose = onDispose;
        }

        protected override (Frame Frame, Dictionary<string, object>? Metadata) ProcessCore(Frame input)
        {
            _onProcess?.Invoke();

            if (_modifyFrame)
            {
                // Create a new frame to simulate modification
                var data = new byte[input.RawBuffer.Length];
                Array.Copy(input.RawBuffer, data, data.Length);
                var newFrame = new Frame(data, input.Width, input.Height, input.Format, input.FrameNumber, input.Stride);
                return (newFrame, _metadata);
            }

            return (input, _metadata);
        }

        public override void Dispose()
        {
            _onDispose?.Invoke();
            base.Dispose();
        }
    }

    #endregion
}
