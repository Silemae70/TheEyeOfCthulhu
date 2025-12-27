using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Core.Matching;
using TheEyeOfCthulhu.Core.Processing;
using TheEyeOfCthulhu.Sources.DroidCam;
using TheEyeOfCthulhu.Sources.Matching;
using TheEyeOfCthulhu.Sources.Processors;
using TheEyeOfCthulhu.Sources.Recording;
using TheEyeOfCthulhu.Sources.Webcam;

using CoreFrame = TheEyeOfCthulhu.Core.Frame;
using CorePixelFormat = TheEyeOfCthulhu.Core.PixelFormat;

namespace TheEyeOfCthulhu.Lab;

/// <summary>
/// Main window for The Eye of Cthulhu Lab application.
/// Provides a visual interface for camera connection, pattern matching, and image processing.
/// </summary>
public partial class MainWindow : Window
{
    #region Fields

    private IFrameSource? _source;
    private ProcessingPipeline? _pipeline;
    private readonly FrameRecorder _recorder;
    private readonly ElderSignStorage _elderSignStorage;
    
    // ElderSign pattern matching
    private ElderSign? _elderSign;
    private ElderSignProcessor? _elderSignProcessor;
    private bool _elderSignDetectionEnabled;
    private int _selectedMatcherType; // 0=Template, 1=ORB, 2=AKAZE

    // Settings path
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TheEyeOfCthulhu");

    #endregion

    #region Constructor

    public MainWindow()
    {
        InitializeComponent();
        
        _recorder = new FrameRecorder(new RecordingOptions
        {
            OutputDirectory = "captures",
            Format = ImageFormat.Png
        });
        
        _elderSignStorage = new ElderSignStorage("eldersigns");

        CreatePipeline();
        
        VisionView.FrameProcessed += OnFrameProcessed;
        Closing += OnWindowClosing;
        Loaded += OnWindowLoaded;
    }

    #endregion

    #region Initialization

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (ShouldShowWizard())
        {
            ShowHelpWizard();
        }
    }

    private void CreatePipeline()
    {
        _pipeline = new ProcessingPipeline("Lab Pipeline")
            .Add(new GrayscaleProcessor { IsEnabled = GrayscaleCheckBox.IsChecked == true })
            .Add(new GaussianBlurProcessor { KernelSize = 5, IsEnabled = BlurCheckBox.IsChecked == true })
            .Add(new ThresholdProcessor { UseOtsu = true, IsEnabled = ThresholdCheckBox.IsChecked == true })
            .Add(new CannyEdgeProcessor { Threshold1 = 50, Threshold2 = 150, IsEnabled = CannyCheckBox.IsChecked == true })
            .Add(new ContourDetectorProcessor { MinArea = 500, DrawContours = true, IsEnabled = ContoursCheckBox.IsChecked == true })
            .Add(new HoughCirclesProcessor { IsEnabled = false });

        if (EnablePipelineCheckBox.IsChecked == true)
        {
            VisionView.SetPipeline(_pipeline);
        }
    }

    #endregion

    #region Help Wizard

    private bool ShouldShowWizard()
    {
        var settingsPath = Path.Combine(SettingsDirectory, "settings.txt");
        
        if (File.Exists(settingsPath))
        {
            return !File.ReadAllText(settingsPath).Contains("wizard_shown=true");
        }
        
        return true;
    }

    private void SaveWizardSetting(bool dontShowAgain)
    {
        if (!dontShowAgain) return;
        
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(Path.Combine(SettingsDirectory, "settings.txt"), "wizard_shown=true");
    }

    private void ShowHelpWizard()
    {
        var wizard = new HelpWizardWindow { Owner = this };
        wizard.ShowDialog();
        SaveWizardSetting(wizard.DontShowAgain);
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e) => ShowHelpWizard();

    #endregion

    #region Camera Connection

    private void SourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DroidCamPanel == null || WebcamPanel == null) return;

        var isDroidCam = SourceComboBox.SelectedIndex == 0;
        DroidCamPanel.Visibility = isDroidCam ? Visibility.Visible : Visibility.Collapsed;
        WebcamPanel.Visibility = isDroidCam ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StartButton.IsEnabled = false;
            
            _source = SourceComboBox.SelectedIndex == 0
                ? new DroidCamSource(DroidCamConfiguration.Create(IpTextBox.Text.Trim(), int.Parse(PortTextBox.Text.Trim())))
                : new WebcamSource(WebcamConfiguration.Create(int.Parse(WebcamIndexTextBox.Text.Trim())));

            VisionView.SetSource(_source);
            await VisionView.StartAsync();

            StopButton.IsEnabled = true;
            SnapshotButton.IsEnabled = true;
            CaptureElderSignButton.IsEnabled = true;
            RoiModeCheckBox.IsEnabled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            StartButton.IsEnabled = true;
        }
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await VisionView.StopAsync();
            
            _source?.Dispose();
            _source = null;
            VisionView.SetSource(null);
            
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            SnapshotButton.IsEnabled = false;
            CaptureElderSignButton.IsEnabled = false;
            RoiModeCheckBox.IsEnabled = false;
            RoiModeCheckBox.IsChecked = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to stop: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SnapshotButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var frame = VisionView.CaptureFrame();
            if (frame == null) return;
            
            var path = _recorder.SaveSnapshot(frame);
            frame.Dispose();
            ProcessingInfoText.Text = $"📸 Saved: {path}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Snapshot failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region ElderSign - ROI Selection

    private void RoiModeCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        VisionView.RoiSelectionEnabled = RoiModeCheckBox.IsChecked == true;
        
        if (RoiModeCheckBox.IsChecked == true)
        {
            RoiInfoText.Text = "🎯 Draw a rectangle on the image";
            RoiInfoText.Foreground = new SolidColorBrush(Color.FromRgb(0, 206, 209));
        }
        else
        {
            VisionView.ClearRoiSelection();
            RoiInfoText.Text = "";
            RoiInfoText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
        }
    }

    private void VisionView_RoiSelected(object? sender, Int32Rect roi)
    {
        RoiInfoText.Text = $"✅ ROI: {roi.Width}x{roi.Height} at ({roi.X}, {roi.Y})";
        RoiInfoText.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 100));
    }

    private void VisionView_ImageClicked(object? sender, System.Windows.Point e)
    {
        ClickInfoText.Text = $"Clicked: X={e.X:F1}, Y={e.Y:F1}";
    }

    #endregion

    #region ElderSign - Capture & Management

    private void CaptureElderSignButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var hasRoi = VisionView.SelectedRoi.HasValue;
            var roiInfo = hasRoi ? VisionView.SelectedRoi.Value : default;
            
            var frame = VisionView.CaptureRoi();
            if (frame == null)
            {
                MessageBox.Show("No frame available", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ClearElderSign();
            CreateElderSign("CapturedSign", frame);
            
            ClearElderSignButton.IsEnabled = true;
            SaveElderSignButton.IsEnabled = true;
            EnableElderSignCheckBox.IsEnabled = true;
            AutoContourButton.IsEnabled = true;
            BackgroundRemoverButton.IsEnabled = true;
            
            var roiText = hasRoi ? $" (ROI {roiInfo.Width}x{roiInfo.Height})" : " (full frame)";
            ElderSignResultText.Text = $"🔯 Captured{roiText}! Enable detection to start.";

            RoiModeCheckBox.IsChecked = false;
            frame.Dispose();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Capture failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearElderSignButton_Click(object sender, RoutedEventArgs e) => ClearElderSign();

    private void ClearElderSign()
    {
        if (_elderSignProcessor != null && _pipeline != null)
        {
            _pipeline.Remove(_elderSignProcessor);
        }

        _elderSign?.Dispose();
        _elderSign = null;
        
        _elderSignProcessor?.Dispose();
        _elderSignProcessor = null;

        _elderSignDetectionEnabled = false;
        EnableElderSignCheckBox.IsChecked = false;
        EnableElderSignCheckBox.IsEnabled = false;
        ClearElderSignButton.IsEnabled = false;
        SaveElderSignButton.IsEnabled = false;
        AutoContourButton.IsEnabled = false;
        BackgroundRemoverButton.IsEnabled = false;
        ShowTemplateOverlayCheckBox.IsEnabled = false;
        
        ElderSignPreview.Visibility = Visibility.Collapsed;
        ElderSignImage.Source = null;
        ElderSignResultText.Text = "";
    }

    private void CreateElderSign(string name, CoreFrame frame)
    {
        _elderSign = new ElderSign(name, frame) { MinScore = MinScoreSlider.Value };
        
        var matcher = CreateMatcher();
        _elderSignProcessor = new ElderSignProcessor(matcher);
        _elderSignProcessor.AddElderSign(_elderSign);
        _elderSignProcessor.DrawMatches = true;
        _elderSignProcessor.ShowLabel = true;
        _elderSignProcessor.IsEnabled = false;

        ShowElderSignPreview(frame);
    }

    private IElderSignMatcher CreateMatcher()
    {
        return _selectedMatcherType switch
        {
            1 => new FeatureSignMatcher(FeatureDetectorType.ORB),
            2 => new FeatureSignMatcher(FeatureDetectorType.AKAZE),
            _ => new TemplateSignMatcher()
        };
    }

    #endregion

    #region ElderSign - Save/Load

    private void SaveElderSignButton_Click(object sender, RoutedEventArgs e)
    {
        if (_elderSign == null) return;

        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save ElderSign",
                InitialDirectory = Path.GetFullPath("eldersigns"),
                FileName = _elderSign.Name,
                DefaultExt = ".json",
                Filter = "ElderSign|*.json"
            };

            if (dialog.ShowDialog() != true) return;
            
            var name = Path.GetFileNameWithoutExtension(dialog.FileName);
            
            if (name != _elderSign.Name)
            {
                var newSign = new ElderSign(name, _elderSign.Template, _elderSign.Anchor) { MinScore = _elderSign.MinScore };
                _elderSign.Dispose();
                _elderSign = newSign;
                
                _elderSignProcessor?.ClearElderSigns();
                _elderSignProcessor?.AddElderSign(_elderSign);
            }
            
            _elderSignStorage.Save(_elderSign);
            ElderSignResultText.Text = $"💾 Saved as '{name}'";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Save failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadElderSignButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Load ElderSign",
                InitialDirectory = Path.GetFullPath("eldersigns"),
                DefaultExt = ".json",
                Filter = "ElderSign|*.json"
            };

            if (dialog.ShowDialog() != true) return;
            
            var name = Path.GetFileNameWithoutExtension(dialog.FileName);
            var loaded = _elderSignStorage.Load(name);
            
            if (loaded == null)
            {
                MessageBox.Show($"Failed to load '{name}'", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ClearElderSign();
            
            _elderSign = loaded;
            _elderSign.MinScore = MinScoreSlider.Value;

            var matcher = CreateMatcher();
            _elderSignProcessor = new ElderSignProcessor(matcher);
            _elderSignProcessor.AddElderSign(_elderSign);
            _elderSignProcessor.DrawMatches = true;
            _elderSignProcessor.ShowLabel = true;
            _elderSignProcessor.IsEnabled = false;

            ShowElderSignPreview(_elderSign.Template);
            
            ClearElderSignButton.IsEnabled = true;
            SaveElderSignButton.IsEnabled = true;
            EnableElderSignCheckBox.IsEnabled = true;
            
            ElderSignResultText.Text = $"📂 Loaded '{name}' ({_elderSign.Width}x{_elderSign.Height})";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Load failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportPngButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import PNG with transparency",
                DefaultExt = ".png",
                Filter = "PNG Images|*.png"
            };

            if (dialog.ShowDialog() != true) return;
            
            ClearElderSign();

            var name = Path.GetFileNameWithoutExtension(dialog.FileName);
            _elderSign = ElderSignFactory.FromPngWithAlpha(name, dialog.FileName);
            _elderSign.MinScore = MinScoreSlider.Value;

            var matcher = CreateMatcher();
            _elderSignProcessor = new ElderSignProcessor(matcher);
            _elderSignProcessor.AddElderSign(_elderSign);
            _elderSignProcessor.DrawMatches = true;
            _elderSignProcessor.ShowLabel = true;
            _elderSignProcessor.IsEnabled = false;

            ShowElderSignPreview(_elderSign.Template);
            
            ClearElderSignButton.IsEnabled = true;
            SaveElderSignButton.IsEnabled = true;
            EnableElderSignCheckBox.IsEnabled = true;
            
            var contourInfo = _elderSign.ContourPoints != null ? $" with {_elderSign.ContourPoints.Length} contour points" : "";
            ElderSignResultText.Text = $"🖼️ Imported '{name}' ({_elderSign.Width}x{_elderSign.Height}){contourInfo}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Import failed: {ex.Message}\n\nMake sure the PNG has an alpha channel.", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region ElderSign - Contour & Background Removal

    private void AutoContourButton_Click(object sender, RoutedEventArgs e)
    {
        if (_elderSign == null) return;

        try
        {
            var oldSign = _elderSign;
            _elderSign = ElderSignFactory.FromFrameWithAutoContour(oldSign.Name, oldSign.Template);
            _elderSign.MinScore = MinScoreSlider.Value;
            
            _elderSignProcessor?.ClearElderSigns();
            _elderSignProcessor?.AddElderSign(_elderSign);
            
            oldSign.Dispose();

            ElderSignResultText.Text = _elderSign.ContourPoints != null 
                ? $"✨ Contour extracted: {_elderSign.ContourPoints.Length} points" 
                : "⚠️ No contour found";
            
            AutoContourButton.IsEnabled = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Auto contour failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BackgroundRemoverButton_Click(object sender, RoutedEventArgs e)
    {
        if (_elderSign == null) return;

        try
        {
            var window = new BackgroundRemoverWindow(_elderSign.Template) { Owner = this };

            if (window.ShowDialog() == true && window.Applied)
            {
                ApplyBackgroundRemovalResult(window);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Background remover failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadImageForRemovalButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Load Image for Background Removal",
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.tiff|All files|*.*"
            };

            if (dialog.ShowDialog() != true) return;
            
            using var mat = Cv2.ImRead(dialog.FileName, ImreadModes.Color);
            
            if (mat.Empty())
            {
                MessageBox.Show("Failed to load image", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var frame = FrameMatConverter.ToFrame(mat, 0);
            var window = new BackgroundRemoverWindow(frame) { Owner = this };

            if (window.ShowDialog() == true && window.Applied)
            {
                ClearElderSign();

                var name = Path.GetFileNameWithoutExtension(dialog.FileName);
                _elderSign = new ElderSign(name, frame) { MinScore = MinScoreSlider.Value };

                if (window.ResultMask != null) _elderSign.SetMask(window.ResultMask);
                if (window.ResultContour is { Length: >= 3 }) _elderSign.SetContour(window.ResultContour);

                var matcher = CreateMatcher();
                _elderSignProcessor = new ElderSignProcessor(matcher);
                _elderSignProcessor.AddElderSign(_elderSign);
                _elderSignProcessor.DrawMatches = true;
                _elderSignProcessor.ShowLabel = true;
                _elderSignProcessor.IsEnabled = false;

                ShowElderSignPreview(_elderSign.Template);
                
                ClearElderSignButton.IsEnabled = true;
                SaveElderSignButton.IsEnabled = true;
                EnableElderSignCheckBox.IsEnabled = true;
                
                var contourInfo = _elderSign.ContourPoints != null ? $" with {_elderSign.ContourPoints.Length} contour points" : "";
                ElderSignResultText.Text = $"🖼️ Loaded '{name}'{contourInfo}";
                ElderSignResultText.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 100));
            }

            frame.Dispose();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyBackgroundRemovalResult(BackgroundRemoverWindow window)
    {
        if (_elderSign == null) return;

        if (window.ResultMask != null) _elderSign.SetMask(window.ResultMask);
        if (window.ResultContour is { Length: >= 3 }) _elderSign.SetContour(window.ResultContour);
        
        _elderSignProcessor?.ClearElderSigns();
        _elderSignProcessor?.AddElderSign(_elderSign);
        
        ElderSignResultText.Text = _elderSign.ContourPoints != null 
            ? $"🪄 Background removed! Contour: {_elderSign.ContourPoints.Length} points" 
            : "🪄 Background removed!";
        ElderSignResultText.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 100));
        
        AutoContourButton.IsEnabled = false;
        BackgroundRemoverButton.IsEnabled = false;
    }

    #endregion

    #region ElderSign - Detection Settings

    private void EnableElderSignCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_elderSignProcessor == null || _pipeline == null) return;

        _elderSignDetectionEnabled = EnableElderSignCheckBox.IsChecked == true;

        if (_elderSignDetectionEnabled)
        {
            if (!_pipeline.Processors.Contains(_elderSignProcessor))
            {
                _pipeline.Insert(0, _elderSignProcessor);
            }
            _elderSignProcessor.IsEnabled = true;
            
            if (EnablePipelineCheckBox.IsChecked != true)
            {
                EnablePipelineCheckBox.IsChecked = true;
            }
            
            ShowTemplateOverlayCheckBox.IsEnabled = true;
            ElderSignResultText.Text = "🔍 Searching...";
        }
        else
        {
            _elderSignProcessor.IsEnabled = false;
            ShowTemplateOverlayCheckBox.IsEnabled = false;
            ElderSignResultText.Text = "Detection disabled";
        }
    }

    private void ShowTemplateOverlayCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_elderSignProcessor != null)
        {
            _elderSignProcessor.DrawTemplateOverlay = ShowTemplateOverlayCheckBox.IsChecked == true;
        }
    }

    private void MinScoreSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MinScoreText == null) return;
        
        MinScoreText.Text = $"{MinScoreSlider.Value:P0}";
        
        if (_elderSign != null)
        {
            _elderSign.MinScore = MinScoreSlider.Value;
        }
    }

    private void MatcherTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedMatcherType = MatcherTypeComboBox.SelectedIndex;
        
        if (_elderSign == null || _elderSignProcessor == null) return;
        
        var wasEnabled = _elderSignDetectionEnabled;
        
        if (wasEnabled) EnableElderSignCheckBox.IsChecked = false;
        
        _pipeline?.Remove(_elderSignProcessor);
        _elderSignProcessor.Dispose();
        
        var matcher = CreateMatcher();
        _elderSignProcessor = new ElderSignProcessor(matcher);
        _elderSignProcessor.AddElderSign(_elderSign);
        _elderSignProcessor.DrawMatches = true;
        _elderSignProcessor.ShowLabel = true;
        _elderSignProcessor.IsEnabled = false;
        
        if (wasEnabled) EnableElderSignCheckBox.IsChecked = true;
        
        var matcherName = _selectedMatcherType switch { 1 => "ORB", 2 => "AKAZE", _ => "Template" };
        ElderSignResultText.Text = $"🔄 Matcher changed to {matcherName}";
    }

    private void ShowElderSignPreview(CoreFrame frame)
    {
        try
        {
            var bitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, GetWpfPixelFormat(frame.Format), null);

            bitmap.Lock();
            System.Runtime.InteropServices.Marshal.Copy(frame.RawBuffer, 0, bitmap.BackBuffer, frame.RawBuffer.Length);
            bitmap.AddDirtyRect(new Int32Rect(0, 0, frame.Width, frame.Height));
            bitmap.Unlock();

            ElderSignImage.Source = bitmap;
            ElderSignSizeText.Text = $"{frame.Width}x{frame.Height}";
            ElderSignPreview.Visibility = Visibility.Visible;
        }
        catch { /* Silently fail preview */ }
    }

    private static System.Windows.Media.PixelFormat GetWpfPixelFormat(CorePixelFormat format) => format switch
    {
        CorePixelFormat.Gray8 => PixelFormats.Gray8,
        CorePixelFormat.Bgr24 => PixelFormats.Bgr24,
        CorePixelFormat.Rgb24 => PixelFormats.Rgb24,
        CorePixelFormat.Bgra32 => PixelFormats.Bgra32,
        _ => PixelFormats.Bgr24
    };

    #endregion

    #region Processing Pipeline

    private void EnablePipelineCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_pipeline == null) return;

        if (EnablePipelineCheckBox.IsChecked == true)
        {
            VisionView.SetPipeline(_pipeline);
            ProcessorsPanel.IsEnabled = true;
        }
        else
        {
            VisionView.SetPipeline(null);
            ProcessorsPanel.IsEnabled = false;
            
            if (_elderSignDetectionEnabled)
            {
                EnableElderSignCheckBox.IsChecked = false;
            }
        }
    }

    private void ProcessorCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_pipeline == null) return;

        var grayscale = _pipeline.GetProcessor<GrayscaleProcessor>("Grayscale");
        var blur = _pipeline.GetProcessor<GaussianBlurProcessor>("GaussianBlur");
        var threshold = _pipeline.GetProcessor<ThresholdProcessor>("Threshold");
        var canny = _pipeline.GetProcessor<CannyEdgeProcessor>("CannyEdge");
        var contours = _pipeline.GetProcessor<ContourDetectorProcessor>("ContourDetector");
        var houghCircles = _pipeline.GetProcessor<HoughCirclesProcessor>("HoughCircles");

        if (grayscale != null) grayscale.IsEnabled = GrayscaleCheckBox.IsChecked == true;
        if (blur != null) blur.IsEnabled = BlurCheckBox.IsChecked == true;
        if (threshold != null) threshold.IsEnabled = ThresholdCheckBox.IsChecked == true;
        if (canny != null) canny.IsEnabled = CannyCheckBox.IsChecked == true;
        if (contours != null) contours.IsEnabled = ContoursCheckBox.IsChecked == true;
        
        if (houghCircles != null)
        {
            houghCircles.IsEnabled = HoughCirclesCheckBox.IsChecked == true;
            HoughCirclesSettings.Visibility = houghCircles.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
            
            if (houghCircles.IsEnabled) UpdateHoughCirclesSettings();
        }
    }

    private void HoughSlider_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e) => UpdateHoughCirclesSettings();
    private void HoughSlider_LostCapture(object sender, System.Windows.Input.MouseEventArgs e) => UpdateHoughCirclesSettings();

    private void UpdateHoughCirclesSettings()
    {
        var houghCircles = _pipeline?.GetProcessor<HoughCirclesProcessor>("HoughCircles");
        if (houghCircles == null) return;

        houghCircles.MinRadius = (int)HoughMinRadiusSlider.Value;
        houghCircles.MaxRadius = (int)HoughMaxRadiusSlider.Value;
        houghCircles.AccumulatorThreshold = HoughSensitivitySlider.Value;
        
        HoughMinRadiusText.Text = $"{houghCircles.MinRadius}";
        HoughMaxRadiusText.Text = $"{houghCircles.MaxRadius}";
        HoughSensitivityText.Text = $"{houghCircles.AccumulatorThreshold:F0}";
    }

    #endregion

    #region Frame Processing Events

    private void OnFrameProcessed(object? sender, PipelineResult e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var contourCount = e.GetMetadata<int>("ContourDetector", "ContourCount");
            var circleCount = e.GetMetadata<int>("HoughCircles", "CircleCount");
            
            var info = $"Processing: {e.TotalProcessingTimeMs:F1}ms";
            if (contourCount > 0) info += $" | Contours: {contourCount}";
            if (circleCount > 0) info += $" | Circles: {circleCount}";
            ProcessingInfoText.Text = info;

            // Hough Circles results
            if (circleCount > 0 && HoughCirclesCheckBox.IsChecked == true)
            {
                var radius = e.GetMetadata<float>("HoughCircles", "LargestCircle.Radius");
                var diameter = e.GetMetadata<float>("HoughCircles", "LargestCircle.Diameter");
                var x = e.GetMetadata<float>("HoughCircles", "LargestCircle.X");
                var y = e.GetMetadata<float>("HoughCircles", "LargestCircle.Y");
                
                HoughResultText.Text = $"✅ {circleCount} cercle(s)\nPlus grand: R={radius:F0}px (Ø{diameter:F0}px)\nCentre: ({x:F0}, {y:F0})";
            }
            else if (HoughCirclesCheckBox.IsChecked == true)
            {
                HoughResultText.Text = "❌ Aucun cercle détecté";
            }

            // ElderSign results
            if (_elderSignDetectionEnabled && _elderSign != null)
            {
                UpdateElderSignResults(e);
            }
        });
    }

    private void UpdateElderSignResults(PipelineResult e)
    {
        var found = e.GetMetadata<bool>("ElderSignDetector", $"{_elderSign!.Name}.Found");
        
        if (found)
        {
            var x = e.GetMetadata<double>("ElderSignDetector", $"{_elderSign.Name}.X");
            var y = e.GetMetadata<double>("ElderSignDetector", $"{_elderSign.Name}.Y");
            var score = e.GetMetadata<double>("ElderSignDetector", $"{_elderSign.Name}.Score");
            
            var resultText = $"✅ FOUND!\nPos: ({x:F0}, {y:F0})\nScore: {score:P0}";
            
            if (_selectedMatcherType > 0)
            {
                var angle = e.GetMetadata<double>("ElderSignDetector", $"{_elderSign.Name}.Angle");
                var scale = e.GetMetadata<double>("ElderSignDetector", $"{_elderSign.Name}.Scale");
                if (Math.Abs(angle) > 0.1 || Math.Abs(scale - 1.0) > 0.01)
                {
                    resultText += $"\n🔄 {angle:F1}° | ×{scale:F2}";
                }
            }
            
            ElderSignResultText.Text = resultText;
            ElderSignResultText.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 100));
        }
        else
        {
            ElderSignResultText.Text = "❌ Not found";
            ElderSignResultText.Foreground = new SolidColorBrush(Color.FromRgb(197, 134, 192));
        }
    }

    #endregion

    #region Cleanup

    private async void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        await VisionView.StopAsync();
        _source?.Dispose();
        _pipeline?.Dispose();
        _recorder.Dispose();
        _elderSign?.Dispose();
        _elderSignProcessor?.Dispose();
    }

    #endregion
}
