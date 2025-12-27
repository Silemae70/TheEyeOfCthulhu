using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Core.Processing;
using CoreFrame = TheEyeOfCthulhu.Core.Frame;
using CorePixelFormat = TheEyeOfCthulhu.Core.PixelFormat;

namespace TheEyeOfCthulhu.WPF.Controls;

/// <summary>
/// Contrôle WPF pour afficher un flux vidéo en temps réel avec sélection ROI et zoom.
/// </summary>
public partial class VisionView : UserControl
{
    private WriteableBitmap? _bitmap;
    private IFrameSource? _source;
    private ProcessingPipeline? _pipeline;
    private bool _isRunning;
    private readonly object _frameLock = new();
    private CoreFrame? _pendingFrame;
    private CoreFrame? _lastOriginalFrame;
    private CoreFrame? _lastDisplayedFrame;

    // ROI Selection
    private bool _isSelectingRoi;
    private bool _roiSelectionEnabled;
    private Point _roiStartPoint;
    private Int32Rect? _selectedRoi;

    // Zoom & Pan
    private double _zoomLevel = 1.0;
    private const double ZoomMin = 0.5;
    private const double ZoomMax = 10.0;
    private const double ZoomStep = 0.15;
    private bool _isPanning;
    private Point _panStartPoint;
    private Point _panStartOffset;

    #region Dependency Properties

    public static readonly DependencyProperty ShowInfoProperty =
        DependencyProperty.Register(nameof(ShowInfo), typeof(bool), typeof(VisionView),
            new PropertyMetadata(true));

    public static readonly DependencyProperty ShowFpsProperty =
        DependencyProperty.Register(nameof(ShowFps), typeof(bool), typeof(VisionView),
            new PropertyMetadata(true));

    public static readonly DependencyProperty StretchModeProperty =
        DependencyProperty.Register(nameof(StretchMode), typeof(Stretch), typeof(VisionView),
            new PropertyMetadata(Stretch.Uniform, OnStretchModeChanged));

    public bool ShowInfo
    {
        get => (bool)GetValue(ShowInfoProperty);
        set => SetValue(ShowInfoProperty, value);
    }

    public bool ShowFps
    {
        get => (bool)GetValue(ShowFpsProperty);
        set => SetValue(ShowFpsProperty, value);
    }

    public Stretch StretchMode
    {
        get => (Stretch)GetValue(StretchModeProperty);
        set => SetValue(StretchModeProperty, value);
    }

    private static void OnStretchModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VisionView view)
        {
            view.ImageDisplay.Stretch = (Stretch)e.NewValue;
        }
    }

    #endregion

    #region Events

    public event EventHandler<CoreFrame>? FrameDisplayed;
    public event EventHandler<PipelineResult>? FrameProcessed;
    public event EventHandler<Point>? ImageClicked;
    public event EventHandler<Int32Rect>? RoiSelected;

    #endregion

    #region ROI Properties

    public bool RoiSelectionEnabled
    {
        get => _roiSelectionEnabled;
        set
        {
            _roiSelectionEnabled = value;
            UpdateCursor();
            if (!value)
            {
                ClearRoiSelection();
            }
        }
    }

    public Int32Rect? SelectedRoi => _selectedRoi;

    #endregion

    #region Zoom Properties

    /// <summary>
    /// Niveau de zoom actuel (1.0 = 100%).
    /// </summary>
    public double ZoomLevel => _zoomLevel;

    #endregion

    public VisionView()
    {
        InitializeComponent();
        
        CompositionTarget.Rendering += OnCompositionTargetRendering;
        
        // Événements souris
        RootGrid.MouseLeftButtonDown += OnMouseLeftButtonDown;
        RootGrid.MouseMove += OnMouseMove;
        RootGrid.MouseLeftButtonUp += OnMouseLeftButtonUp;
        
        // Zoom & Pan
        RootGrid.MouseWheel += OnMouseWheel;
        RootGrid.PreviewMouseDown += OnPreviewMouseDown;
        RootGrid.PreviewMouseUp += OnPreviewMouseUp;
        
        SizeChanged += OnSizeChanged;
        Unloaded += OnUnloaded;
    }

    #region Public Methods

    public void SetSource(IFrameSource? source)
    {
        if (_source != null)
        {
            _source.FrameReceived -= OnFrameReceived;
            _source.StateChanged -= OnSourceStateChanged;
        }

        _source = source;

        if (_source != null)
        {
            _source.FrameReceived += OnFrameReceived;
            _source.StateChanged += OnSourceStateChanged;
            
            NoSourceText.Visibility = Visibility.Collapsed;
            UpdateStatus(_source.State.ToString());
        }
        else
        {
            NoSourceText.Visibility = Visibility.Visible;
            UpdateStatus("No source");
            ClearDisplay();
        }
    }

    public void SetPipeline(ProcessingPipeline? pipeline)
    {
        _pipeline = pipeline;
    }

    public async Task StartAsync()
    {
        if (_source == null) return;

        if (_source.State == SourceState.Created)
        {
            await _source.InitializeAsync();
        }

        if (_source.State == SourceState.Ready || _source.State == SourceState.Paused)
        {
            await _source.StartAsync();
        }

        _isRunning = true;
    }

    public async Task StopAsync()
    {
        _isRunning = false;

        if (_source != null && _source.State == SourceState.Running)
        {
            await _source.StopAsync();
        }
    }

    public CoreFrame? CaptureFrame()
    {
        lock (_frameLock)
        {
            return _lastDisplayedFrame?.Clone();
        }
    }

    public CoreFrame? CaptureOriginalFrame()
    {
        lock (_frameLock)
        {
            return _lastOriginalFrame?.Clone();
        }
    }

    public CoreFrame? CaptureRoi()
    {
        lock (_frameLock)
        {
            if (_lastOriginalFrame == null) return null;

            if (_selectedRoi == null || _selectedRoi.Value.Width == 0 || _selectedRoi.Value.Height == 0)
            {
                return _lastOriginalFrame.Clone();
            }

            return CropFrame(_lastOriginalFrame, _selectedRoi.Value);
        }
    }

    public void ClearRoiSelection()
    {
        _selectedRoi = null;
        _isSelectingRoi = false;
        SelectionRect.Visibility = Visibility.Collapsed;
        RoiText.Text = "";
    }

    public void ClearDisplay()
    {
        _bitmap = null;
        ImageDisplay.Source = null;
        
        lock (_frameLock)
        {
            _lastOriginalFrame?.Dispose();
            _lastOriginalFrame = null;
            _lastDisplayedFrame?.Dispose();
            _lastDisplayedFrame = null;
        }
    }

    /// <summary>
    /// Réinitialise le zoom à 100% et recentre l'image.
    /// </summary>
    public void ResetZoom()
    {
        _zoomLevel = 1.0;
        ZoomTransform.ScaleX = 1.0;
        ZoomTransform.ScaleY = 1.0;
        PanTransform.X = 0;
        PanTransform.Y = 0;
        UpdateZoomText();
    }

    #endregion

    #region Zoom & Pan

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Position du curseur dans le ZoomContainer (avant zoom)
        var mousePos = e.GetPosition(ZoomContainer);
        
        // Calculer le nouveau zoom
        var oldZoom = _zoomLevel;
        if (e.Delta > 0)
            _zoomLevel = Math.Min(ZoomMax, _zoomLevel * (1 + ZoomStep));
        else
            _zoomLevel = Math.Max(ZoomMin, _zoomLevel / (1 + ZoomStep));

        // Reset si on revient à ~100%
        if (_zoomLevel >= 0.95 && _zoomLevel <= 1.05)
        {
            _zoomLevel = 1.0;
            ZoomTransform.ScaleX = 1.0;
            ZoomTransform.ScaleY = 1.0;
            PanTransform.X = 0;
            PanTransform.Y = 0;
            UpdateZoomText();
            e.Handled = true;
            return;
        }

        // Calculer le point sous le curseur en coordonnées image (avant transformation)
        var relativeX = (mousePos.X - PanTransform.X) / oldZoom;
        var relativeY = (mousePos.Y - PanTransform.Y) / oldZoom;

        // Appliquer le nouveau zoom
        ZoomTransform.ScaleX = _zoomLevel;
        ZoomTransform.ScaleY = _zoomLevel;

        // Ajuster le pan pour que le point sous le curseur reste au même endroit
        PanTransform.X = mousePos.X - relativeX * _zoomLevel;
        PanTransform.Y = mousePos.Y - relativeY * _zoomLevel;

        UpdateZoomText();
        e.Handled = true;
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Bouton du milieu = Pan
        if (e.ChangedButton == MouseButton.Middle)
        {
            if (_zoomLevel <= 1.0) return;

            _isPanning = true;
            _panStartPoint = e.GetPosition(RootGrid);
            _panStartOffset = new Point(PanTransform.X, PanTransform.Y);
            RootGrid.CaptureMouse();
            UpdateCursor();
            e.Handled = true;
        }
        // Double-clic droit = reset zoom
        else if (e.ChangedButton == MouseButton.Right && e.ClickCount == 2)
        {
            ResetZoom();
            e.Handled = true;
        }
    }

    private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle && _isPanning)
        {
            _isPanning = false;
            RootGrid.ReleaseMouseCapture();
            UpdateCursor();
            e.Handled = true;
        }
    }

    private void UpdateCursor()
    {
        if (_isPanning)
            Cursor = Cursors.Hand;
        else if (_roiSelectionEnabled)
            Cursor = Cursors.Cross;
        else if (_zoomLevel > 1.0)
            Cursor = Cursors.ScrollAll;
        else
            Cursor = Cursors.Arrow;
    }

    private void UpdateZoomText()
    {
        if (_zoomLevel > 1.01 || _zoomLevel < 0.99)
            ZoomText.Text = $"Zoom: {_zoomLevel:P0}";
        else
            ZoomText.Text = "";
    }

    #endregion

    #region ROI Selection

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateRoiRectangle();
    }

    private void UpdateRoiRectangle()
    {
        if (_selectedRoi == null || _lastOriginalFrame == null)
        {
            return;
        }

        var screenRect = ImageToScreenRect(_selectedRoi.Value);
        
        Canvas.SetLeft(SelectionRect, screenRect.X);
        Canvas.SetTop(SelectionRect, screenRect.Y);
        SelectionRect.Width = screenRect.Width;
        SelectionRect.Height = screenRect.Height;
        SelectionRect.Visibility = Visibility.Visible;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_roiSelectionEnabled || _lastOriginalFrame == null) return;

        _isSelectingRoi = true;
        _roiStartPoint = e.GetPosition(ZoomContainer);
        
        Canvas.SetLeft(SelectionRect, _roiStartPoint.X);
        Canvas.SetTop(SelectionRect, _roiStartPoint.Y);
        SelectionRect.Width = 0;
        SelectionRect.Height = 0;
        SelectionRect.Visibility = Visibility.Visible;

        RootGrid.CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        // Pan avec bouton du milieu
        if (_isPanning)
        {
            var currentPos = e.GetPosition(RootGrid);
            PanTransform.X = _panStartOffset.X + (currentPos.X - _panStartPoint.X);
            PanTransform.Y = _panStartOffset.Y + (currentPos.Y - _panStartPoint.Y);
            return;
        }

        // Sélection ROI
        if (!_isSelectingRoi || _lastOriginalFrame == null) return;

        var currentPoint = e.GetPosition(ZoomContainer);

        var x = Math.Min(_roiStartPoint.X, currentPoint.X);
        var y = Math.Min(_roiStartPoint.Y, currentPoint.Y);
        var width = Math.Abs(currentPoint.X - _roiStartPoint.X);
        var height = Math.Abs(currentPoint.Y - _roiStartPoint.Y);

        x = Math.Max(0, x);
        y = Math.Max(0, y);
        width = Math.Min(width, ZoomContainer.ActualWidth - x);
        height = Math.Min(height, ZoomContainer.ActualHeight - y);

        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = width;
        SelectionRect.Height = height;

        var imageRect = ScreenToImageRect(x, y, width, height, _lastOriginalFrame);
        RoiText.Text = $"ROI: {imageRect.Width}x{imageRect.Height}";
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelectingRoi) return;

        _isSelectingRoi = false;
        RootGrid.ReleaseMouseCapture();

        var x = Canvas.GetLeft(SelectionRect);
        var y = Canvas.GetTop(SelectionRect);
        var width = SelectionRect.Width;
        var height = SelectionRect.Height;

        if (width < 10 || height < 10)
        {
            ClearRoiSelection();
            
            if (_lastOriginalFrame != null)
            {
                var imagePoint = ScreenToImagePoint(e.GetPosition(ZoomContainer), _lastOriginalFrame);
                ImageClicked?.Invoke(this, imagePoint);
            }
            return;
        }

        if (_lastOriginalFrame != null)
        {
            _selectedRoi = ScreenToImageRect(x, y, width, height, _lastOriginalFrame);
            RoiText.Text = $"ROI: {_selectedRoi.Value.X},{_selectedRoi.Value.Y} {_selectedRoi.Value.Width}x{_selectedRoi.Value.Height}";
            RoiSelected?.Invoke(this, _selectedRoi.Value);
        }
    }

    private Point ScreenToImagePoint(Point screenPoint, CoreFrame referenceFrame)
    {
        var imageRect = GetImageDisplayRect(referenceFrame);
        if (imageRect.Width <= 0 || imageRect.Height <= 0) return new Point(0, 0);

        var imageX = (screenPoint.X - imageRect.X) * referenceFrame.Width / imageRect.Width;
        var imageY = (screenPoint.Y - imageRect.Y) * referenceFrame.Height / imageRect.Height;

        return new Point(
            Math.Clamp(imageX, 0, referenceFrame.Width),
            Math.Clamp(imageY, 0, referenceFrame.Height));
    }

    private Int32Rect ScreenToImageRect(double x, double y, double width, double height, CoreFrame referenceFrame)
    {
        var imageRect = GetImageDisplayRect(referenceFrame);
        if (imageRect.Width <= 0 || imageRect.Height <= 0) return new Int32Rect();

        var imageX = (int)((x - imageRect.X) * referenceFrame.Width / imageRect.Width);
        var imageY = (int)((y - imageRect.Y) * referenceFrame.Height / imageRect.Height);
        var imageW = (int)(width * referenceFrame.Width / imageRect.Width);
        var imageH = (int)(height * referenceFrame.Height / imageRect.Height);

        imageX = Math.Clamp(imageX, 0, referenceFrame.Width);
        imageY = Math.Clamp(imageY, 0, referenceFrame.Height);
        imageW = Math.Clamp(imageW, 0, referenceFrame.Width - imageX);
        imageH = Math.Clamp(imageH, 0, referenceFrame.Height - imageY);

        return new Int32Rect(imageX, imageY, imageW, imageH);
    }

    private Rect ImageToScreenRect(Int32Rect imageRoi)
    {
        if (_lastOriginalFrame == null) return new Rect();

        var imageRect = GetImageDisplayRect(_lastOriginalFrame);
        if (imageRect.Width <= 0 || imageRect.Height <= 0) return new Rect();

        var scaleX = imageRect.Width / _lastOriginalFrame.Width;
        var scaleY = imageRect.Height / _lastOriginalFrame.Height;

        return new Rect(
            imageRect.X + imageRoi.X * scaleX,
            imageRect.Y + imageRoi.Y * scaleY,
            imageRoi.Width * scaleX,
            imageRoi.Height * scaleY);
    }

    private Rect GetImageDisplayRect(CoreFrame referenceFrame)
    {
        var controlWidth = ZoomContainer.ActualWidth;
        var controlHeight = ZoomContainer.ActualHeight;

        if (controlWidth <= 0 || controlHeight <= 0) return new Rect();

        var scale = Math.Min(controlWidth / referenceFrame.Width, controlHeight / referenceFrame.Height);
        var displayWidth = referenceFrame.Width * scale;
        var displayHeight = referenceFrame.Height * scale;

        var offsetX = (controlWidth - displayWidth) / 2;
        var offsetY = (controlHeight - displayHeight) / 2;

        return new Rect(offsetX, offsetY, displayWidth, displayHeight);
    }

    private static CoreFrame? CropFrame(CoreFrame source, Int32Rect roi)
    {
        if (roi.X < 0 || roi.Y < 0 || 
            roi.X + roi.Width > source.Width || 
            roi.Y + roi.Height > source.Height)
        {
            return null;
        }

        int bytesPerPixel = source.Format switch
        {
            CorePixelFormat.Gray8 => 1,
            CorePixelFormat.Bgr24 or CorePixelFormat.Rgb24 => 3,
            CorePixelFormat.Bgra32 or CorePixelFormat.Rgba32 => 4,
            _ => 3
        };

        var croppedStride = roi.Width * bytesPerPixel;
        var croppedData = new byte[croppedStride * roi.Height];

        for (int y = 0; y < roi.Height; y++)
        {
            var srcOffset = (roi.Y + y) * source.Stride + roi.X * bytesPerPixel;
            var dstOffset = y * croppedStride;
            
            Buffer.BlockCopy(source.RawBuffer, srcOffset, croppedData, dstOffset, croppedStride);
        }

        return new CoreFrame(croppedData, roi.Width, roi.Height, source.Format, source.FrameNumber, croppedStride);
    }

    #endregion

    #region Frame Handling

    private void OnFrameReceived(object? sender, FrameEventArgs e)
    {
        if (!_isRunning) return;

        lock (_frameLock)
        {
            _pendingFrame?.Dispose();
            _pendingFrame = e.Frame.Clone();
        }
    }

    private void OnSourceStateChanged(object? sender, SourceState state)
    {
        Dispatcher.BeginInvoke(() => 
        {
            UpdateStatus(state.ToString());
            
            // Afficher l'overlay de reconnexion
            if (state == SourceState.Reconnecting)
            {
                ReconnectingOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                ReconnectingOverlay.Visibility = Visibility.Collapsed;
            }
        });
    }

    private void OnCompositionTargetRendering(object? sender, EventArgs e)
    {
        if (!_isRunning) return;

        CoreFrame? frameToProcess = null;
        
        lock (_frameLock)
        {
            if (_pendingFrame != null)
            {
                frameToProcess = _pendingFrame;
                _pendingFrame = null;
            }
        }

        if (frameToProcess != null)
        {
            try
            {
                lock (_frameLock)
                {
                    _lastOriginalFrame?.Dispose();
                    _lastOriginalFrame = frameToProcess.Clone();
                }

                CoreFrame displayFrame = frameToProcess;
                PipelineResult? pipelineResult = null;

                if (_pipeline != null)
                {
                    pipelineResult = _pipeline.Process(frameToProcess);
                    displayFrame = pipelineResult.FinalFrame;
                    FrameProcessed?.Invoke(this, pipelineResult);
                }

                DisplayFrame(displayFrame);
                UpdateInfo(frameToProcess);
                
                lock (_frameLock)
                {
                    _lastDisplayedFrame?.Dispose();
                    _lastDisplayedFrame = displayFrame.Clone();
                }
                
                FrameDisplayed?.Invoke(this, frameToProcess);
            }
            finally
            {
                frameToProcess.Dispose();
            }
        }
    }

    private void DisplayFrame(CoreFrame frame)
    {
        if (_bitmap == null || _bitmap.PixelWidth != frame.Width || _bitmap.PixelHeight != frame.Height)
        {
            var format = frame.Format switch
            {
                CorePixelFormat.Gray8 => PixelFormats.Gray8,
                CorePixelFormat.Bgr24 => PixelFormats.Bgr24,
                CorePixelFormat.Rgb24 => PixelFormats.Rgb24,
                CorePixelFormat.Bgra32 => PixelFormats.Bgra32,
                CorePixelFormat.Rgba32 => PixelFormats.Pbgra32,
                _ => PixelFormats.Bgr24
            };

            _bitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, format, null);
            ImageDisplay.Source = _bitmap;
        }

        _bitmap.Lock();
        try
        {
            var rect = new Int32Rect(0, 0, frame.Width, frame.Height);
            _bitmap.WritePixels(rect, frame.RawBuffer, frame.Stride, 0);
        }
        finally
        {
            _bitmap.Unlock();
        }
    }

    private void UpdateInfo(CoreFrame frame)
    {
        if (_source == null) return;

        FpsText.Text = $"{_source.ActualFps:F1} FPS";
        ResolutionText.Text = $"{frame.Width} x {frame.Height}";
    }

    private void UpdateStatus(string status)
    {
        StatusText.Text = status;
        
        StatusText.Foreground = status switch
        {
            "Running" => Brushes.LimeGreen,
            "Ready" => Brushes.Yellow,
            "Reconnecting" => Brushes.Orange,
            "Error" => Brushes.Red,
            _ => Brushes.Gray
        };
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CompositionTarget.Rendering -= OnCompositionTargetRendering;
        SizeChanged -= OnSizeChanged;
        
        lock (_frameLock)
        {
            _pendingFrame?.Dispose();
            _pendingFrame = null;
            _lastOriginalFrame?.Dispose();
            _lastOriginalFrame = null;
            _lastDisplayedFrame?.Dispose();
            _lastDisplayedFrame = null;
        }
    }

    #endregion
}
