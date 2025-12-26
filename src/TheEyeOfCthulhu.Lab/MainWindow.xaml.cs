using System.Windows;
using System.Windows.Controls;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Core.Processing;
using TheEyeOfCthulhu.Sources.DroidCam;
using TheEyeOfCthulhu.Sources.Processors;
using TheEyeOfCthulhu.Sources.Recording;
using TheEyeOfCthulhu.Sources.Webcam;

namespace TheEyeOfCthulhu.Lab;

public partial class MainWindow : Window
{
    private IFrameSource? _source;
    private ProcessingPipeline? _pipeline;
    private FrameRecorder? _recorder;

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
        }
    }

    private void ProcessorCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_pipeline == null) return;

        // Mettre à jour l'état des processeurs
        _pipeline.Processors[0].IsEnabled = GrayscaleCheckBox.IsChecked == true;
        _pipeline.Processors[1].IsEnabled = BlurCheckBox.IsChecked == true;
        _pipeline.Processors[2].IsEnabled = ThresholdCheckBox.IsChecked == true;
        _pipeline.Processors[3].IsEnabled = CannyCheckBox.IsChecked == true;
        _pipeline.Processors[4].IsEnabled = ContoursCheckBox.IsChecked == true;
    }

    private void VisionView_ImageClicked(object? sender, Point e)
    {
        ClickInfoText.Text = $"Clicked: X={e.X:F1}, Y={e.Y:F1}";
    }

    private void OnFrameProcessed(object? sender, PipelineResult e)
    {
        // Afficher les infos de processing
        var contourCount = e.GetMetadata<int>("ContourDetector", "ContourCount");
        
        Dispatcher.BeginInvoke(() =>
        {
            if (contourCount > 0)
            {
                ProcessingInfoText.Text = $"Processing: {e.TotalProcessingTimeMs:F1}ms | Contours: {contourCount}";
            }
            else
            {
                ProcessingInfoText.Text = $"Processing: {e.TotalProcessingTimeMs:F1}ms";
            }
        });
    }

    private async void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        await VisionView.StopAsync();
        _source?.Dispose();
        _pipeline?.Dispose();
        _recorder?.Dispose();
    }
}
