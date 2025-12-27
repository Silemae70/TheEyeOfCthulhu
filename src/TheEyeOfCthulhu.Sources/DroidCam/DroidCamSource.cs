using System.Diagnostics;
using System.Net.NetworkInformation;
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
        // Vérifier si le téléphone est accessible
        if (!IsHostReachable(_config.IpAddress, _config.ConnectionTimeoutSeconds * 1000))
        {
            throw new InvalidOperationException(
                $"❌ Cannot reach DroidCam at {_config.IpAddress}:{_config.Port}\n\n" +
                $"Please check:\n" +
                $"• DroidCam app is running on your phone\n" +
                $"• Phone and PC are on the same WiFi network\n" +
                $"• IP address is correct (shown in DroidCam app)\n" +
                $"• Firewall is not blocking the connection");
        }

        var capture = new VideoCapture();

        // Buffer minimal pour réduire la latence
        capture.Set(VideoCaptureProperties.BufferSize, 1);

        Log($"Connecting to {_config.VideoUrl}...");

        // Ouvrir le flux MJPEG
        if (!capture.Open(_config.VideoUrl, VideoCaptureAPIs.FFMPEG))
        {
            // Fallback sur ANY si FFMPEG échoue
            if (!capture.Open(_config.VideoUrl, VideoCaptureAPIs.ANY))
            {
                capture.Dispose();
                throw new InvalidOperationException(
                    $"❌ Failed to open video stream at {_config.VideoUrl}\n\n" +
                    $"DroidCam is reachable but the video stream failed.\n" +
                    $"Try restarting the DroidCam app on your phone.");
            }
        }

        Log($"Connected to stream: {_config.VideoUrl}");
        return capture;
    }

    /// <summary>
    /// Vérifie si l'hôte est accessible via ping.
    /// </summary>
    private static bool IsHostReachable(string host, int timeoutMs)
    {
        try
        {
            using var ping = new Ping();
            var reply = ping.Send(host, timeoutMs);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            // Si ping échoue (ex: désactivé sur le réseau), on tente quand même
            return TryTcpConnect(host, 4747, timeoutMs);
        }
    }

    /// <summary>
    /// Essaie une connexion TCP directe.
    /// </summary>
    private static bool TryTcpConnect(string host, int port, int timeoutMs)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var result = client.BeginConnect(host, port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(timeoutMs));
            
            if (success)
            {
                client.EndConnect(result);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}
