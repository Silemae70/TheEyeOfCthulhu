using OpenCvSharp;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Core.Processing;
using TheEyeOfCthulhu.Sources.DroidCam;
using TheEyeOfCthulhu.Sources.File;
using TheEyeOfCthulhu.Sources.Processors;
using TheEyeOfCthulhu.Sources.Recording;
using TheEyeOfCthulhu.Sources.Utilities;
using TheEyeOfCthulhu.Sources.Webcam;

namespace TheEyeOfCthulhu.Console;

/// <summary>
/// The Eye of Cthulhu - Console Test Application
/// "Ph'nglui mglw'nafh Cthulhu R'lyeh wgah'nagl fhtagn"
/// </summary>
class Program
{
    private static readonly FrameSourceFactory Factory = FrameSourceFactory.Default;
    private static FrameRecorder? _recorder;

    static async Task Main(string[] args)
    {
        PrintBanner();
        RegisterProviders();
        InitializeRecorder();

        while (true)
        {
            PrintMenu();
            var choice = System.Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    await RunLiveView(GetDroidCamConfig());
                    break;
                case "2":
                    await RunLiveView(GetWebcamConfig());
                    break;
                case "3":
                    await RunLiveViewWithProcessing(GetDroidCamConfig());
                    break;
                case "4":
                    await RunLiveViewWithProcessing(GetWebcamConfig());
                    break;
                case "5":
                    await RunFileSourceTest();
                    break;
                case "6":
                    await RunProcessingDemo();
                    break;
                case "7":
                    ConfigureRecorder();
                    break;
                case "0":
                case "q":
                    System.Console.WriteLine("\nIä! Iä! Cthulhu fhtagn! 🐙");
                    _recorder?.Dispose();
                    return;
                default:
                    System.Console.WriteLine("Invalid choice");
                    break;
            }
        }
    }

    static void PrintBanner()
    {
        System.Console.ForegroundColor = ConsoleColor.DarkGreen;
        System.Console.WriteLine(@"
  _____ _          _____              ___   __  ___ _   _       _ _           
 |_   _| |_ ___   | ____|_  _ ___    / _ \ / _|/ __| |_| |_ _  _| | |_ _  _   
   | | | ' | -_)  | _|| || / -_)   | (_) |  _| (__| _| ' | || | | | ' | || |  
   |_| |_||_\___|_|___|_  \___|    \___/|_|  \___|__|_||_\_,_|_|_|_||_\_,_|  
                  |____|                                                      
");
        System.Console.ResetColor();
        System.Console.WriteLine("  Vision Framework - The Eye sees all... 👁️🐙\n");
    }

    static void PrintMenu()
    {
        System.Console.WriteLine("\n╔════════════════════════════════════════════╗");
        System.Console.WriteLine("║              MAIN MENU                     ║");
        System.Console.WriteLine("╠════════════════════════════════════════════╣");
        System.Console.WriteLine("║  1. DroidCam Live View                     ║");
        System.Console.WriteLine("║  2. Webcam Live View                       ║");
        System.Console.WriteLine("║  3. DroidCam + Processing                  ║");
        System.Console.WriteLine("║  4. Webcam + Processing                    ║");
        System.Console.WriteLine("║  5. File Source (image/video)              ║");
        System.Console.WriteLine("║  6. Processing Demo                        ║");
        System.Console.WriteLine("╠════════════════════════════════════════════╣");
        System.Console.WriteLine("║  7. Configure Snapshot Settings            ║");
        System.Console.WriteLine("║  0. Exit                                   ║");
        System.Console.WriteLine("╚════════════════════════════════════════════╝");
        System.Console.WriteLine($"\n  [Snapshots: {_recorder?.Options.OutputDirectory} | Format: {_recorder?.Options.Format}]");
        System.Console.Write("\nChoice: ");
    }

    static void RegisterProviders()
    {
        Factory.RegisterProvider(new DroidCamSourceProvider());
        Factory.RegisterProvider(new FileSourceProvider());
        Factory.RegisterProvider(new WebcamSourceProvider());

        System.Console.WriteLine($"Registered providers: {string.Join(", ", Factory.GetAvailableSourceTypes())}");
    }

    static void InitializeRecorder()
    {
        _recorder = new FrameRecorder(new RecordingOptions
        {
            OutputDirectory = "captures",
            Format = ImageFormat.Png,
            IncludeTimestamp = true,
            IncludeFrameNumber = true
        });
        System.Console.WriteLine($"Snapshot directory: {Path.GetFullPath(_recorder.Options.OutputDirectory)}");
    }

    static void ConfigureRecorder()
    {
        System.Console.WriteLine("\n=== Snapshot Configuration ===\n");
        
        System.Console.Write($"Output directory [{_recorder?.Options.OutputDirectory}]: ");
        var dir = System.Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(dir))
        {
            _recorder!.Options.OutputDirectory = dir;
        }

        System.Console.WriteLine("Format: 1=PNG, 2=JPEG, 3=BMP, 4=TIFF");
        System.Console.Write($"Choice [{(int)_recorder!.Options.Format + 1}]: ");
        var formatStr = System.Console.ReadLine()?.Trim();
        if (int.TryParse(formatStr, out int formatChoice) && formatChoice >= 1 && formatChoice <= 4)
        {
            _recorder.Options.Format = (ImageFormat)(formatChoice - 1);
        }

        if (_recorder.Options.Format == ImageFormat.Jpeg)
        {
            System.Console.Write($"JPEG Quality (1-100) [{_recorder.Options.JpegQuality}]: ");
            var qualityStr = System.Console.ReadLine()?.Trim();
            if (int.TryParse(qualityStr, out int quality) && quality >= 1 && quality <= 100)
            {
                _recorder.Options.JpegQuality = quality;
            }
        }

        System.Console.Write($"File prefix [{_recorder.Options.FilePrefix}]: ");
        var prefix = System.Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(prefix))
        {
            _recorder.Options.FilePrefix = prefix;
        }

        System.Console.WriteLine($"\nConfiguration saved!");
        System.Console.WriteLine($"  Directory: {Path.GetFullPath(_recorder.Options.OutputDirectory)}");
        System.Console.WriteLine($"  Format: {_recorder.Options.Format}");
        System.Console.WriteLine($"  Prefix: {_recorder.Options.FilePrefix}");
    }

    #region Live View

    static async Task RunLiveView(SourceConfiguration? config)
    {
        if (config == null) return;

        System.Console.WriteLine($"\nConnecting...");

        using var source = Factory.Create(config);
        var running = true;
        Frame? latestFrame = null;
        Frame? frameToSnapshot = null;
        object frameLock = new();
        bool snapshotRequested = false;
        const string windowName = "The Eye of Cthulhu - Live";

        try
        {
            await source.InitializeAsync();
            System.Console.WriteLine($"Initialized: {source.Width}x{source.Height}");

            FrameViewer.CreateWindow(windowName, WindowFlags.Normal);

            source.FrameReceived += (s, e) =>
            {
                if (!running) return;
                
                lock (frameLock)
                {
                    latestFrame?.Dispose();
                    latestFrame = e.Frame.Clone();
                }
            };

            await source.StartAsync();

            PrintLiveViewHelp();

            while (running)
            {
                // Vérifier si la fenêtre a été fermée
                if (!IsWindowOpen(windowName))
                {
                    System.Console.WriteLine("Window closed by user");
                    running = false;
                    break;
                }

                // Récupérer la dernière frame
                Frame? frameToShow = null;
                lock (frameLock)
                {
                    frameToShow = latestFrame;
                    latestFrame = null;
                    
                    // Garder une copie pour snapshot si demandé
                    if (snapshotRequested && frameToShow != null)
                    {
                        frameToSnapshot?.Dispose();
                        frameToSnapshot = frameToShow.Clone();
                        snapshotRequested = false;
                    }
                }

                if (frameToShow != null)
                {
                    FrameViewer.ShowFrameWithInfo(frameToShow, source.ActualFps, windowName);
                    frameToShow.Dispose();
                }

                // Traiter le snapshot en dehors du lock
                if (frameToSnapshot != null)
                {
                    try
                    {
                        var path = _recorder!.SaveSnapshot(frameToSnapshot);
                        System.Console.WriteLine($"📸 Snapshot saved: {path}");
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"❌ Snapshot failed: {ex.Message}");
                    }
                    frameToSnapshot.Dispose();
                    frameToSnapshot = null;
                }

                // Gérer les touches
                int key = Cv2.WaitKey(10);
                running = HandleLiveViewKeys(key, ref snapshotRequested, running);

                // Vérifier la console
                if (System.Console.KeyAvailable)
                {
                    var consoleKey = System.Console.ReadKey(true);
                    running = HandleConsoleKey(consoleKey, ref snapshotRequested, running);
                }
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            System.Console.WriteLine("Stopping capture...");
            await source.StopAsync();
            
            lock (frameLock)
            {
                latestFrame?.Dispose();
            }
            frameToSnapshot?.Dispose();
            
            FrameViewer.DestroyAllWindows();
            System.Console.WriteLine($"Stopped. Total frames: {source.TotalFramesCaptured} | Snapshots: {_recorder?.FramesSaved}");
        }
    }

    static async Task RunLiveViewWithProcessing(SourceConfiguration? config)
    {
        if (config == null) return;

        System.Console.WriteLine($"\nConnecting...");

        using var source = Factory.Create(config);
        using var pipeline = CreateProcessingPipeline();
        var running = true;
        Frame? latestFrame = null;
        Frame? frameToSnapshot = null;
        object frameLock = new();
        bool snapshotRequested = false;
        bool snapshotProcessed = false; // true = save processed, false = save original
        const string windowOriginal = "Original";
        const string windowProcessed = "Processed";

        try
        {
            await source.InitializeAsync();
            System.Console.WriteLine($"Initialized: {source.Width}x{source.Height}");

            FrameViewer.CreateWindow(windowOriginal, WindowFlags.Normal);
            FrameViewer.CreateWindow(windowProcessed, WindowFlags.Normal);

            source.FrameReceived += (s, e) =>
            {
                if (!running) return;

                lock (frameLock)
                {
                    latestFrame?.Dispose();
                    latestFrame = e.Frame.Clone();
                }
            };

            await source.StartAsync();

            PrintProcessingHelp(pipeline);

            while (running)
            {
                if (!IsWindowOpen(windowOriginal))
                {
                    System.Console.WriteLine("Window closed by user");
                    running = false;
                    break;
                }

                Frame? frameToShow = null;
                lock (frameLock)
                {
                    frameToShow = latestFrame;
                    latestFrame = null;
                }

                if (frameToShow != null)
                {
                    // Afficher original
                    FrameViewer.ShowFrameWithInfo(frameToShow, source.ActualFps, windowOriginal);

                    // Appliquer pipeline
                    var result = pipeline.Process(frameToShow);

                    // Afficher résultat
                    using var processedMat = FrameViewer.FrameToMat(result.FinalFrame);
                    var info = $"Processed | {result.TotalProcessingTimeMs:F1}ms";
                    Cv2.PutText(processedMat, info, new Point(10, 30), HersheyFonts.HersheySimplex, 0.7, Scalar.LimeGreen, 2);

                    if (result.AllMetadata.TryGetValue("ContourDetector.ContourCount", out var countObj))
                    {
                        var count = (int)countObj;
                        Cv2.PutText(processedMat, $"Contours: {count}", new Point(10, 60), 
                            HersheyFonts.HersheySimplex, 0.7, Scalar.Yellow, 2);
                    }

                    Cv2.ImShow(windowProcessed, processedMat);

                    // Snapshot si demandé
                    if (snapshotRequested)
                    {
                        try
                        {
                            var frameToSave = snapshotProcessed ? result.FinalFrame : frameToShow;
                            var path = _recorder!.SaveSnapshot(frameToSave);
                            var type = snapshotProcessed ? "processed" : "original";
                            System.Console.WriteLine($"📸 Snapshot ({type}): {path}");
                        }
                        catch (Exception ex)
                        {
                            System.Console.WriteLine($"❌ Snapshot failed: {ex.Message}");
                        }
                        snapshotRequested = false;
                    }

                    frameToShow.Dispose();
                }

                // Gérer les touches
                int key = Cv2.WaitKey(10);
                running = HandleProcessingKeys(key, pipeline, ref snapshotRequested, ref snapshotProcessed, running);

                if (System.Console.KeyAvailable)
                {
                    var consoleKey = System.Console.ReadKey(true);
                    running = HandleConsoleKey(consoleKey, ref snapshotRequested, running);
                }
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            System.Console.WriteLine("Stopping capture...");
            await source.StopAsync();
            
            lock (frameLock)
            {
                latestFrame?.Dispose();
            }
            
            FrameViewer.DestroyAllWindows();
            System.Console.WriteLine($"Stopped. Total frames: {source.TotalFramesCaptured} | Snapshots: {_recorder?.FramesSaved}");
        }
    }

    #endregion

    #region Helpers

    static bool IsWindowOpen(string windowName)
    {
        try
        {
            var prop = Cv2.GetWindowProperty(windowName, WindowPropertyFlags.Visible);
            return prop >= 1;
        }
        catch
        {
            return false;
        }
    }

    static void PrintLiveViewHelp()
    {
        System.Console.WriteLine("\n╔════════════════════════════════════════════╗");
        System.Console.WriteLine("║  Controls:                                 ║");
        System.Console.WriteLine("║    S     = Save snapshot                   ║");
        System.Console.WriteLine("║    ESC/Q = Stop                            ║");
        System.Console.WriteLine("╚════════════════════════════════════════════╝\n");
    }

    static void PrintProcessingHelp(ProcessingPipeline pipeline)
    {
        System.Console.WriteLine("\n╔════════════════════════════════════════════╗");
        System.Console.WriteLine("║  Controls:                                 ║");
        System.Console.WriteLine("║    1-5   = Toggle processors               ║");
        System.Console.WriteLine("║    S     = Snapshot original               ║");
        System.Console.WriteLine("║    D     = Snapshot processed              ║");
        System.Console.WriteLine("║    ESC/Q = Stop                            ║");
        System.Console.WriteLine("╠════════════════════════════════════════════╣");
        System.Console.WriteLine("║  Pipeline:                                 ║");
        int i = 1;
        foreach (var p in pipeline.Processors)
        {
            var status = p.IsEnabled ? "ON " : "OFF";
            System.Console.WriteLine($"║    {i}. {p.Name,-20} [{status}]        ║");
            i++;
        }
        System.Console.WriteLine("╚════════════════════════════════════════════╝\n");
    }

    static bool HandleLiveViewKeys(int key, ref bool snapshotRequested, bool running)
    {
        switch (key)
        {
            case 27: // ESC
            case 'q':
            case 'Q':
                System.Console.WriteLine("Stop requested");
                return false;
            case 's':
            case 'S':
                snapshotRequested = true;
                break;
        }
        return running;
    }

    static bool HandleProcessingKeys(int key, ProcessingPipeline pipeline, ref bool snapshotRequested, ref bool snapshotProcessed, bool running)
    {
        switch (key)
        {
            case 27: // ESC
            case 'q':
            case 'Q':
                System.Console.WriteLine("Stop requested");
                return false;
            case 's':
            case 'S':
                snapshotRequested = true;
                snapshotProcessed = false;
                break;
            case 'd':
            case 'D':
                snapshotRequested = true;
                snapshotProcessed = true;
                break;
            case >= '1' and <= '9':
                int index = key - '1';
                if (index < pipeline.Processors.Count)
                {
                    var processor = pipeline.Processors[index];
                    processor.IsEnabled = !processor.IsEnabled;
                    System.Console.WriteLine($"{processor.Name}: {(processor.IsEnabled ? "ON" : "OFF")}");
                }
                break;
        }
        return running;
    }

    static bool HandleConsoleKey(ConsoleKeyInfo consoleKey, ref bool snapshotRequested, bool running)
    {
        switch (consoleKey.Key)
        {
            case ConsoleKey.Escape:
            case ConsoleKey.Q:
                System.Console.WriteLine("Console stop requested");
                return false;
            case ConsoleKey.S:
                snapshotRequested = true;
                break;
        }
        return running;
    }

    #endregion

    #region File Source & Demo

    static async Task RunFileSourceTest()
    {
        System.Console.Write("Enter file path (image or video): ");
        var path = System.Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
        {
            System.Console.WriteLine("File not found.");
            return;
        }

        var config = new FileSourceConfiguration
        {
            FilePath = path,
            Loop = true,
            TargetFps = 30
        };

        await RunLiveView(config);
    }

    static async Task RunProcessingDemo()
    {
        System.Console.WriteLine("\nCreating test image...");

        using var testImage = new Mat(480, 640, MatType.CV_8UC3, new Scalar(50, 50, 50));

        // Dessiner quelques formes
        Cv2.Rectangle(testImage, new Rect(100, 100, 150, 100), new Scalar(255, 255, 255), -1);
        Cv2.Circle(testImage, new Point(450, 200), 60, new Scalar(255, 255, 255), -1);
        Cv2.Ellipse(testImage, new Point(300, 350), new Size(80, 40), 30, 0, 360, new Scalar(255, 255, 255), -1);

        var frameData = new byte[testImage.Rows * testImage.Step()];
        System.Runtime.InteropServices.Marshal.Copy(testImage.Data, frameData, 0, frameData.Length);
        var frame = new Frame(frameData, testImage.Width, testImage.Height, 
            Core.PixelFormat.Bgr24, 0, (int)testImage.Step());

        using var pipeline = CreateProcessingPipeline();

        System.Console.WriteLine("\nPipeline steps:");
        foreach (var p in pipeline.Processors)
        {
            System.Console.WriteLine($"  - {p.Name}");
        }

        FrameViewer.CreateWindow("0. Original", WindowFlags.Normal);
        Cv2.ImShow("0. Original", testImage);

        var currentFrame = frame;
        int step = 1;
        foreach (var processor in pipeline.Processors)
        {
            var result = processor.Process(currentFrame);
            using var mat = FrameViewer.FrameToMat(result.Frame);
            
            var windowName = $"{step}. {processor.Name}";
            FrameViewer.CreateWindow(windowName, WindowFlags.Normal);
            Cv2.ImShow(windowName, mat);

            if (result.Metadata.Count > 0)
            {
                System.Console.WriteLine($"\n{processor.Name} metadata:");
                foreach (var (key, value) in result.Metadata)
                {
                    if (key == "Contours") continue;
                    System.Console.WriteLine($"  {key}: {value}");
                }
            }

            currentFrame = result.Frame;
            step++;
        }

        System.Console.WriteLine("\nPress any key to close windows...");
        Cv2.WaitKey(0);
        FrameViewer.DestroyAllWindows();

        await Task.CompletedTask;
    }

    #endregion

    #region Configuration

    static DroidCamConfiguration? GetDroidCamConfig()
    {
        const string DEFAULT_IP = "192.168.1.57";
        const int DEFAULT_PORT = 4747;

        System.Console.Write($"Enter DroidCam IP [{DEFAULT_IP}]: ");
        var ip = System.Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(ip)) ip = DEFAULT_IP;

        System.Console.Write($"Enter port [{DEFAULT_PORT}]: ");
        var portStr = System.Console.ReadLine()?.Trim();
        int port = string.IsNullOrWhiteSpace(portStr) ? DEFAULT_PORT : int.Parse(portStr);

        return DroidCamConfiguration.Create(ip, port);
    }

    static WebcamConfiguration? GetWebcamConfig()
    {
        System.Console.Write("Enter webcam index [0]: ");
        var indexStr = System.Console.ReadLine()?.Trim();
        int index = string.IsNullOrWhiteSpace(indexStr) ? 0 : int.Parse(indexStr);

        return WebcamConfiguration.Create(index);
    }

    static ProcessingPipeline CreateProcessingPipeline()
    {
        return new ProcessingPipeline("Demo Pipeline")
            .Add(new GrayscaleProcessor())
            .Add(new GaussianBlurProcessor { KernelSize = 5 })
            .Add(new ThresholdProcessor { UseOtsu = true })
            .Add(new CannyEdgeProcessor { Threshold1 = 50, Threshold2 = 150 })
            .Add(new ContourDetectorProcessor { MinArea = 500, DrawContours = true });
    }

    #endregion
}
