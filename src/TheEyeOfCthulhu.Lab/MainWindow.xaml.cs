using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Core.Matching;
using TheEyeOfCthulhu.Core.Processing;
using TheEyeOfCthulhu.Sources.DroidCam;
using TheEyeOfCthulhu.Sources.Matching;
using TheEyeOfCthulhu.Sources.Processors;
using TheEyeOfCthulhu.Sources.Recording;
using TheEyeOfCthulhu.Sources.Webcam;

// Alias pour éviter les conflits WPF
using CoreFrame = TheEyeOfCthulhu.Core.Frame;
using CorePixelFormat = TheEyeOfCthulhu.Core.PixelFormat;

namespace TheEyeOfCthulhu.Lab;

public partial class MainWindow : Window
{
    private IFrameSource? _source;
    private ProcessingPipeline? _pipeline;
    private FrameRecorder? _recorder;
    
    // 🔯 ElderSign
    private ElderSign? _elderSign;
    private ElderSignProcessor? _elderSignProcessor;
    private bool _elderSignDetectionEnabled;
    private int _selectedMatcherType = 0; // 0=Template, 1=ORB, 2=AKAZE

    public MainWindow()
    {
        InitializeComponent();
        
        _recorder = new FrameRecorder(new RecordingOptions
        {
            OutputDirectory = "captures",
            Format = ImageFormat.Png
        });

        CreatePipeline();
        
        // Événements du VisionView
        VisionView.FrameProcessed += OnFrameProcessed;
        
        Closing += OnWindowClosing;
    }

    private void CreatePipeline()
    {
        _pipeline = new ProcessingPipeline("Lab Pipeline")
            .Add(new GrayscaleProcessor { IsEnabled = GrayscaleCheckBox.IsChecked == true })
            .Add(new GaussianBlurProcessor { KernelSize = 5, IsEnabled = BlurCheckBox.IsChecked == true })
            .Add(new ThresholdProcessor { UseOtsu = true, IsEnabled = ThresholdCheckBox.IsChecked == true })
            .Add(new CannyEdgeProcessor { Threshold1 = 50, Threshold2 = 150, IsEnabled = CannyCheckBox.IsChecked == true })
            .Add(new ContourDetectorProcessor { MinArea = 500, DrawContours = true, IsEnabled = ContoursCheckBox.IsChecked == true })
            .Add(new HoughCirclesProcessor { IsEnabled = false }); // Désactivé par défaut

        if (EnablePipelineCheckBox.IsChecked == true)
        {
            VisionView.SetPipeline(_pipeline);
        }
    }

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
            
            // Créer la source
            if (SourceComboBox.SelectedIndex == 0) // DroidCam
            {
                var ip = IpTextBox.Text.Trim();
                var port = int.Parse(PortTextBox.Text.Trim());
                _source = new DroidCamSource(DroidCamConfiguration.Create(ip, port));
            }
            else // Webcam
            {
                var index = int.Parse(WebcamIndexTextBox.Text.Trim());
                _source = new WebcamSource(WebcamConfiguration.Create(index));
            }

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
            if (frame != null)
            {
                var path = _recorder!.SaveSnapshot(frame);
                frame.Dispose();
                
                ProcessingInfoText.Text = $"📸 Saved: {path}";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Snapshot failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #region 🔯 ElderSign

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
            RoiInfoText.Text = "💡 Enable ROI mode to select a region";
            RoiInfoText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
        }
    }

    private void VisionView_RoiSelected(object? sender, Int32Rect roi)
    {
        RoiInfoText.Text = $"✅ ROI: {roi.Width}x{roi.Height} at ({roi.X}, {roi.Y})";
        RoiInfoText.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 100));
    }

    private void CaptureElderSignButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Sauvegarder la ROI AVANT de désactiver le mode (sinon elle est effacée)
            var hasRoi = VisionView.SelectedRoi.HasValue;
            var roiInfo = hasRoi ? VisionView.SelectedRoi.Value : default;
            
            // Utiliser CaptureRoi() qui capture soit la ROI soit toute la frame
            var frame = VisionView.CaptureRoi();
            if (frame == null)
            {
                MessageBox.Show("No frame available", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Nettoyer l'ancien
            ClearElderSign();

            // Créer le nouvel ElderSign
            _elderSign = new ElderSign("CapturedSign", frame)
            {
                MinScore = MinScoreSlider.Value
            };

            // Créer le processeur avec le bon matcher
            IElderSignMatcher matcher = _selectedMatcherType switch
            {
                1 => new FeatureSignMatcher(FeatureDetectorType.ORB),
                2 => new FeatureSignMatcher(FeatureDetectorType.AKAZE),
                _ => new TemplateSignMatcher()
            };
            
            _elderSignProcessor = new ElderSignProcessor(matcher);
            _elderSignProcessor.AddElderSign(_elderSign);
            _elderSignProcessor.DrawMatches = true;
            _elderSignProcessor.ShowLabel = true;
            _elderSignProcessor.IsEnabled = false;

            // Afficher la preview
            ShowElderSignPreview(frame);
            
            // Activer les contrôles
            ClearElderSignButton.IsEnabled = true;
            EnableElderSignCheckBox.IsEnabled = true;
            
            var roiText = hasRoi ? $" (from ROI {roiInfo.Width}x{roiInfo.Height})" : " (full frame)";
            ElderSignResultText.Text = $"🔯 ElderSign captured{roiText}! Enable detection to start.";

            // Désactiver le mode ROI APRES la capture
            RoiModeCheckBox.IsChecked = false;
            
            frame.Dispose();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Capture failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearElderSignButton_Click(object sender, RoutedEventArgs e)
    {
        ClearElderSign();
    }

    private void ClearElderSign()
    {
        // Retirer du pipeline si actif
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
        
        ElderSignPreview.Visibility = Visibility.Collapsed;
        ElderSignImage.Source = null;
        ElderSignResultText.Text = "";
    }

    private void EnableElderSignCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_elderSignProcessor == null || _pipeline == null) return;

        _elderSignDetectionEnabled = EnableElderSignCheckBox.IsChecked == true;

        if (_elderSignDetectionEnabled)
        {
            // Ajouter au pipeline (au début pour avoir l'image originale)
            if (!_pipeline.Processors.Contains(_elderSignProcessor))
            {
                _pipeline.Insert(0, _elderSignProcessor);
            }
            _elderSignProcessor.IsEnabled = true;
            
            // S'assurer que le pipeline est actif
            if (EnablePipelineCheckBox.IsChecked != true)
            {
                EnablePipelineCheckBox.IsChecked = true;
            }
            
            ElderSignResultText.Text = "🔍 Searching...";
        }
        else
        {
            _elderSignProcessor.IsEnabled = false;
            ElderSignResultText.Text = "Detection disabled";
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
        
        // Si on a déjà un ElderSign, recréer le processeur avec le nouveau matcher
        if (_elderSign != null && _elderSignProcessor != null)
        {
            var wasEnabled = _elderSignDetectionEnabled;
            
            // Désactiver temporairement
            if (wasEnabled)
            {
                EnableElderSignCheckBox.IsChecked = false;
            }
            
            // Recréer le processeur
            if (_pipeline != null)
            {
                _pipeline.Remove(_elderSignProcessor);
            }
            _elderSignProcessor.Dispose();
            
            IElderSignMatcher matcher = _selectedMatcherType switch
            {
                1 => new FeatureSignMatcher(FeatureDetectorType.ORB),
                2 => new FeatureSignMatcher(FeatureDetectorType.AKAZE),
                _ => new TemplateSignMatcher()
            };
            
            _elderSignProcessor = new ElderSignProcessor(matcher);
            _elderSignProcessor.AddElderSign(_elderSign);
            _elderSignProcessor.DrawMatches = true;
            _elderSignProcessor.ShowLabel = true;
            _elderSignProcessor.IsEnabled = false;
            
            // Réactiver si nécessaire
            if (wasEnabled)
            {
                EnableElderSignCheckBox.IsChecked = true;
            }
            
            var matcherName = _selectedMatcherType switch
            {
                1 => "ORB",
                2 => "AKAZE",
                _ => "Template"
            };
            ElderSignResultText.Text = $"🔄 Matcher changed to {matcherName}";
        }
    }

    private void ShowElderSignPreview(CoreFrame frame)
    {
        try
        {
            var bitmap = new WriteableBitmap(
                frame.Width, 
                frame.Height, 
                96, 96, 
                GetWpfPixelFormat(frame.Format), 
                null);

            bitmap.Lock();
            System.Runtime.InteropServices.Marshal.Copy(
                frame.RawBuffer, 0, bitmap.BackBuffer, frame.RawBuffer.Length);
            bitmap.AddDirtyRect(new Int32Rect(0, 0, frame.Width, frame.Height));
            bitmap.Unlock();

            ElderSignImage.Source = bitmap;
            ElderSignSizeText.Text = $"{frame.Width}x{frame.Height}";
            ElderSignPreview.Visibility = Visibility.Visible;
        }
        catch
        {
            // Silently fail preview
        }
    }

    private static System.Windows.Media.PixelFormat GetWpfPixelFormat(CorePixelFormat format)
    {
        return format switch
        {
            CorePixelFormat.Gray8 => PixelFormats.Gray8,
            CorePixelFormat.Bgr24 => PixelFormats.Bgr24,
            CorePixelFormat.Rgb24 => PixelFormats.Rgb24,
            CorePixelFormat.Bgra32 => PixelFormats.Bgra32,
            _ => PixelFormats.Bgr24
        };
    }

    #endregion

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
            
            // Désactiver aussi ElderSign detection
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
        
        // HoughCircles
        if (houghCircles != null)
        {
            houghCircles.IsEnabled = HoughCirclesCheckBox.IsChecked == true;
            HoughCirclesSettings.Visibility = houghCircles.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
            
            if (houghCircles.IsEnabled)
            {
                UpdateHoughCirclesSettings();
            }
        }
    }

    private void HoughSlider_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        UpdateHoughCirclesSettings();
    }

    private void HoughSlider_LostCapture(object sender, System.Windows.Input.MouseEventArgs e)
    {
        UpdateHoughCirclesSettings();
    }

    private void UpdateHoughCirclesSettings()
    {
        if (_pipeline == null) return;
        
        var houghCircles = _pipeline.GetProcessor<HoughCirclesProcessor>("HoughCircles");
        if (houghCircles == null) return;

        houghCircles.MinRadius = (int)HoughMinRadiusSlider.Value;
        houghCircles.MaxRadius = (int)HoughMaxRadiusSlider.Value;
        houghCircles.AccumulatorThreshold = HoughSensitivitySlider.Value;
        
        // Mettre à jour les labels des sliders
        HoughMinRadiusText.Text = $"{houghCircles.MinRadius}";
        HoughMaxRadiusText.Text = $"{houghCircles.MaxRadius}";
        HoughSensitivityText.Text = $"{houghCircles.AccumulatorThreshold:F0}";
    }

    private void VisionView_ImageClicked(object? sender, System.Windows.Point e)
    {
        ClickInfoText.Text = $"Clicked: X={e.X:F1}, Y={e.Y:F1}";
    }

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

            // 🔵 Afficher les infos du plus grand cercle
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

            // 🔯 Afficher les résultats ElderSign
            if (_elderSignDetectionEnabled && _elderSign != null)
            {
                var found = e.GetMetadata<bool>("ElderSignDetector", $"{_elderSign.Name}.Found");
                
                if (found)
                {
                    var ex = e.GetMetadata<double>("ElderSignDetector", $"{_elderSign.Name}.X");
                    var ey = e.GetMetadata<double>("ElderSignDetector", $"{_elderSign.Name}.Y");
                    var score = e.GetMetadata<double>("ElderSignDetector", $"{_elderSign.Name}.Score");
                    
                    var resultText = $"✅ FOUND!\nPos: ({ex:F0}, {ey:F0})\nScore: {score:P0}";
                    
                    // Afficher angle et scale si Feature matcher
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
        });
    }

    private async void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        await VisionView.StopAsync();
        _source?.Dispose();
        _pipeline?.Dispose();
        _recorder?.Dispose();
        _elderSign?.Dispose();
        _elderSignProcessor?.Dispose();
    }
}
