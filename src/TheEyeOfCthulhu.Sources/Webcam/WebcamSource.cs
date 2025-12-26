using OpenCvSharp;
using TheEyeOfCthulhu.Sources.Common;

namespace TheEyeOfCthulhu.Sources.Webcam;

/// <summary>
/// Source de frames depuis une webcam USB ou virtuelle.
/// </summary>
public sealed class WebcamSource : VideoCaptureSourceBase
{
    private readonly WebcamConfiguration _config;

    public override string Name { get; }
    public override string SourceType => "Webcam";
    public override double TargetFps => _config.TargetFps;
    protected override string CaptureThreadName => "Webcam Capture";
    protected override string LogTag => "Webcam";

    public WebcamSource(WebcamConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        Name = config.DisplayName ?? $"Webcam_{config.DeviceIndex}";
    }

    protected override VideoCapture CreateCapture()
    {
        var backend = _config.Backend switch
        {
            WebcamBackend.DirectShow => VideoCaptureAPIs.DSHOW,
            WebcamBackend.MediaFoundation => VideoCaptureAPIs.MSMF,
            WebcamBackend.V4L2 => VideoCaptureAPIs.V4L2,
            _ => VideoCaptureAPIs.ANY
        };

        var capture = new VideoCapture(_config.DeviceIndex, backend);

        if (!capture.IsOpened())
        {
            throw new InvalidOperationException($"Failed to open webcam at index {_config.DeviceIndex}");
        }

        // Configurer la résolution si demandée
        if (_config.RequestedWidth > 0)
        {
            capture.Set(VideoCaptureProperties.FrameWidth, _config.RequestedWidth);
        }
        if (_config.RequestedHeight > 0)
        {
            capture.Set(VideoCaptureProperties.FrameHeight, _config.RequestedHeight);
        }
        if (_config.TargetFps > 0)
        {
            capture.Set(VideoCaptureProperties.Fps, _config.TargetFps);
        }

        // Buffer minimal pour réduire la latence
        capture.Set(VideoCaptureProperties.BufferSize, 1);

        Log($"Opened device {_config.DeviceIndex} with backend {backend}");
        return capture;
    }
}
