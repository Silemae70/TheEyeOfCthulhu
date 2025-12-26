using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Core.Processing;
using CoreFrame = TheEyeOfCthulhu.Core.Frame;
using CorePixelFormat = TheEyeOfCthulhu.Core.PixelFormat;

namespace TheEyeOfCthulhu.WPF.Controls;

/// <summary>
/// Contrôle WPF pour afficher un flux vidéo en temps réel.
/// </summary>
public partial class VisionView : UserControl
{
    private WriteableBitmap? _bitmap;
    private IFrameSource? _source;
    private ProcessingPipeline? _pipeline;
    private bool _isRunning;
    private readonly object _frameLock = new();
    private CoreFrame? _pendingFrame;
    private CoreFrame? _lastDisplayedFrame; // Garde une copie pour snapshot

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

    /// <summary>
    /// Afficher le panneau d'info (FPS, résolution, status).
    /// </summary>
    public bool ShowInfo
    {
        get => (bool)GetValue(ShowInfoProperty);
        set => SetValue(ShowInfoProperty, value);
    }

    /// <summary>
    /// Afficher les FPS.
    /// </summary>
    public bool ShowFps
    {
        get => (bool)GetValue(ShowFpsProperty);
        set => SetValue(ShowFpsProperty, value);
    }

    /// <summary>
    /// Mode d'étirement de l'image.
    /// </summary>
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

    /// <summary>
    /// Événement déclenché quand une frame est affichée.
    /// </summary>
    public event EventHandler<CoreFrame>? FrameDisplayed;

    /// <summary>
    /// Événement déclenché quand une frame est traitée (si pipeline actif).
    /// </summary>
    public event EventHandler<PipelineResult>? FrameProcessed;

    /// <summary>
    /// Événement déclenché sur clic dans l'image (coordonnées image).
    /// </summary>
    public event EventHandler<Point>? ImageClicked;

    #endregion

    public VisionView()
    {
        InitializeComponent();
        
        // Timer pour le rendu à 60 FPS max
        CompositionTarget.Rendering += OnCompositionTargetRendering;
        
        // Clic sur l'image
        ImageDisplay.MouseLeftButtonDown += OnImageClicked;
        
        Unloaded += OnUnloaded;
    }

    #region Public Methods

    /// <summary>
    /// Définit la source vidéo.
    /// </summary>
    public void SetSource(IFrameSource? source)
    {
        // Détacher l'ancienne source
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

    /// <summary>
    /// Définit le pipeline de traitement (optionnel).
    /// </summary>
    public void SetPipeline(ProcessingPipeline? pipeline)
    {
        _pipeline = pipeline;
    }

    /// <summary>
    /// Démarre l'affichage.
    /// </summary>
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

    /// <summary>
    /// Arrête l'affichage.
    /// </summary>
    public async Task StopAsync()
    {
        _isRunning = false;

        if (_source != null && _source.State == SourceState.Running)
        {
            await _source.StopAsync();
        }
    }

    /// <summary>
    /// Capture la frame actuelle (dernière frame affichée).
    /// </summary>
    public CoreFrame? CaptureFrame()
    {
        lock (_frameLock)
        {
            return _lastDisplayedFrame?.Clone();
        }
    }

    /// <summary>
    /// Efface l'affichage.
    /// </summary>
    public void ClearDisplay()
    {
        _bitmap = null;
        ImageDisplay.Source = null;
        
        lock (_frameLock)
        {
            _lastDisplayedFrame?.Dispose();
            _lastDisplayedFrame = null;
        }
    }

    #endregion

    #region Private Methods

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
        Dispatcher.BeginInvoke(() => UpdateStatus(state.ToString()));
    }

    private void OnCompositionTargetRendering(object? sender, EventArgs e)
    {
        if (!_isRunning) return;

        CoreFrame? frameToDisplay = null;
        
        lock (_frameLock)
        {
            if (_pendingFrame != null)
            {
                frameToDisplay = _pendingFrame;
                _pendingFrame = null;
            }
        }

        if (frameToDisplay != null)
        {
            try
            {
                // Appliquer le pipeline si présent
                CoreFrame displayFrame = frameToDisplay;
                PipelineResult? pipelineResult = null;

                if (_pipeline != null)
                {
                    pipelineResult = _pipeline.Process(frameToDisplay);
                    displayFrame = pipelineResult.FinalFrame;
                    FrameProcessed?.Invoke(this, pipelineResult);
                }

                // Afficher la frame
                DisplayFrame(displayFrame);
                
                // Mettre à jour les infos
                UpdateInfo(frameToDisplay);
                
                // Garder une copie pour le snapshot
                lock (_frameLock)
                {
                    _lastDisplayedFrame?.Dispose();
                    _lastDisplayedFrame = frameToDisplay.Clone();
                }
                
                FrameDisplayed?.Invoke(this, frameToDisplay);
            }
            finally
            {
                frameToDisplay.Dispose();
            }
        }
    }

    private void DisplayFrame(CoreFrame frame)
    {
        // Créer ou recréer le bitmap si nécessaire
        if (_bitmap == null || _bitmap.PixelWidth != frame.Width || _bitmap.PixelHeight != frame.Height)
        {
            var format = frame.Format switch
            {
                CorePixelFormat.Gray8 => PixelFormats.Gray8,
                CorePixelFormat.Bgr24 => PixelFormats.Bgr24,
                CorePixelFormat.Rgb24 => PixelFormats.Rgb24,
                CorePixelFormat.Bgra32 => PixelFormats.Bgra32,
                CorePixelFormat.Rgba32 => PixelFormats.Pbgra32, // WPF n'a pas Rgba32, utiliser Pbgra32
                _ => PixelFormats.Bgr24
            };

            _bitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, format, null);
            ImageDisplay.Source = _bitmap;
        }

        // Copier les pixels
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
            "Error" => Brushes.Red,
            _ => Brushes.Gray
        };
    }

    private void OnImageClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_bitmap == null) return;

        // Convertir les coordonnées écran en coordonnées image
        var pos = e.GetPosition(ImageDisplay);
        var imageX = pos.X * _bitmap.PixelWidth / ImageDisplay.ActualWidth;
        var imageY = pos.Y * _bitmap.PixelHeight / ImageDisplay.ActualHeight;

        ImageClicked?.Invoke(this, new Point(imageX, imageY));
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CompositionTarget.Rendering -= OnCompositionTargetRendering;
        
        lock (_frameLock)
        {
            _pendingFrame?.Dispose();
            _pendingFrame = null;
            _lastDisplayedFrame?.Dispose();
            _lastDisplayedFrame = null;
        }
    }

    #endregion
}
