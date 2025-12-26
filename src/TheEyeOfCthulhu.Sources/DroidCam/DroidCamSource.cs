using OpenCvSharp;
using TheEyeOfCthulhu.Sources.Common;

namespace TheEyeOfCthulhu.Sources.DroidCam;

/// <summary>
/// Source de frames depuis DroidCam (Android).
/// Supporte le flux MJPEG via HTTP.
/// </summary>
public sealed class DroidCamSource : VideoCaptureSourceBase
{
    private readonly DroidCamConfiguration _config;

    public override string Name { get; }
    public override string SourceType => "DroidCam";
    public override double TargetFps => _config.TargetFps;
    protected override string CaptureThreadName => "DroidCam Capture";
    protected override string LogTag => "DroidCam";

    public DroidCamSource(DroidCamConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        Name = config.DisplayName ?? $"DroidCam_{config.IpAddress}";
    }

    protected override VideoCapture CreateCapture()
    {
        var capture = new VideoCapture();

        // Buffer minimal pour réduire la latence
        capture.Set(VideoCaptureProperties.BufferSize, 1);

        // Ouvrir le flux MJPEG
        if (!capture.Open(_config.VideoUrl, VideoCaptureAPIs.FFMPEG))
        {
            // Fallback sur ANY si FFMPEG échoue
            if (!capture.Open(_config.VideoUrl, VideoCaptureAPIs.ANY))
            {
                throw new InvalidOperationException($"Failed to open video stream at {_config.VideoUrl}");
            }
        }

        Log($"Opened stream: {_config.VideoUrl}");
        return capture;
    }
}
