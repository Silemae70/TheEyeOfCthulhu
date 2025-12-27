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
            .Add(new ContourDetectorProcessor { MinArea = 500, DrawContours = true, IsEnabled = ContoursCheckBox.IsChecked == true });

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
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

    private void CaptureElderSignButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var frame = VisionView.CaptureFrame();
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

            // Créer le processeur
            _elderSignProcessor = new ElderSignProcessor();
            _elderSignProcessor.AddElderSign(_elderSign);
            _elderSignProcessor.DrawMatches = true;
            _elderSignProcessor.ShowLabel = true;
            _elderSignProcessor.IsEnabled = false; // Désactivé par défaut

            // Afficher la preview
            ShowElderSignPreview(frame);
            
            // Activer les contrôles
            ClearElderSignButton.IsEnabled = true;
            EnableElderSignCheckBox.IsEnabled = true;
            ElderSignResultText.Text = "🔯 ElderSign captured! Enable detection to start.";

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

    private void ShowElderSignPreview(CoreFrame frame)
    {
        try
        {
            // Convertir Frame en BitmapSource pour WPF
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

        // Trouver les processeurs par nom
        var grayscale = _pipeline.GetProcessor<GrayscaleProcessor>("Grayscale");
        var blur = _pipeline.GetProcessor<GaussianBlurProcessor>("GaussianBlur");
        var threshold = _pipeline.GetProcessor<ThresholdProcessor>("Threshold");
        var canny = _pipeline.GetProcessor<CannyEdgeProcessor>("CannyEdge");
        var contours = _pipeline.GetProcessor<ContourDetectorProcessor>("ContourDetector");

        if (grayscale != null) grayscale.IsEnabled = GrayscaleCheckBox.IsChecked == true;
        if (blur != null) blur.IsEnabled = BlurCheckBox.IsChecked == true;
        if (threshold != null) threshold.IsEnabled = ThresholdCheckBox.IsChecked == true;
        if (canny != null) canny.IsEnabled = CannyCheckBox.IsChecked == true;
        if (contours != null) contours.IsEnabled = ContoursCheckBox.IsChecked == true;
    }

    private void VisionView_ImageClicked(object? sender, System.Windows.Point e)
    {
        ClickInfoText.Text = $"Clicked: X={e.X:F1}, Y={e.Y:F1}";
    }

    private void OnFrameProcessed(object? sender, PipelineResult e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            // Afficher les infos de processing
            var contourCount = e.GetMetadata<int>("ContourDetector", "ContourCount");
            
            if (contourCount > 0)
            {
                ProcessingInfoText.Text = $"Processing: {e.TotalProcessingTimeMs:F1}ms | Contours: {contourCount}";
            }
            else
            {
                ProcessingInfoText.Text = $"Processing: {e.TotalProcessingTimeMs:F1}ms";
            }

            // 🔯 Afficher les résultats ElderSign
            if (_elderSignDetectionEnabled && _elderSign != null)
            {
                var found = e.GetMetadata<bool>("ElderSignDetector", $"{_elderSign.Name}.Found");
                
                if (found)
                {
                    var x = e.GetMetadata<double>("ElderSignDetector", $"{_elderSign.Name}.X");
                    var y = e.GetMetadata<double>("ElderSignDetector", $"{_elderSign.Name}.Y");
                    var score = e.GetMetadata<double>("ElderSignDetector", $"{_elderSign.Name}.Score");
                    
                    ElderSignResultText.Text = $"✅ FOUND!\nPos: ({x:F0}, {y:F0})\nScore: {score:P0}";
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
