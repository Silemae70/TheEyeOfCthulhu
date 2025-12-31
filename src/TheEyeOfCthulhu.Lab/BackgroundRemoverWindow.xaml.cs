using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Sources.Processors;
using CvRect = OpenCvSharp.Rect;
using CvPoint = OpenCvSharp.Point;
using CorePointF = TheEyeOfCthulhu.Core.PointF;
using CorePixelFormat = TheEyeOfCthulhu.Core.PixelFormat;

namespace TheEyeOfCthulhu.Lab;

/// <summary>
/// Fenêtre pour supprimer le fond d'une image et créer un masque/contour.
/// </summary>
public partial class BackgroundRemoverWindow : System.Windows.Window
{
    private readonly Frame _originalFrame;
    private Mat? _originalMat;
    private Mat? _maskMat;
    private CorePointF[]? _contourPoints;
    private double _currentZoom = 1.0;
    private const double ZoomStep = 0.1;
    private const double MinZoom = 0.1;
    private const double MaxZoom = 10.0;
    
    // Pan avec clic molette
    private bool _isPanning;
    private System.Windows.Point _panStart;
    private double _panStartOffsetX;
    private double _panStartOffsetY;
    
    /// <summary>
    /// Le masque résultant (255 = objet, 0 = fond).
    /// </summary>
    public Frame? ResultMask { get; private set; }
    
    /// <summary>
    /// Les points du contour extrait.
    /// </summary>
    public CorePointF[]? ResultContour => _contourPoints;
    
    /// <summary>
    /// Indique si l'utilisateur a appliqué les changements.
    /// </summary>
    public bool Applied { get; private set; }

    public BackgroundRemoverWindow(Frame frame)
    {
        InitializeComponent();
        
        _originalFrame = frame.Clone();
        _originalMat = FrameMatConverter.ToMat(_originalFrame);
        
        // Afficher l'image
        UpdatePreview();
    }

    private void PreviewImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_originalMat == null) return;
        
        // Obtenir la position du clic relative à l'image (en tenant compte du zoom)
        var pos = e.GetPosition(PreviewImage);
        
        // Les coordonnées sont déjà en pixels image car Stretch="None"
        var imgX = (int)pos.X;
        var imgY = (int)pos.Y;
        
        // Clamp
        imgX = Math.Clamp(imgX, 0, _originalMat.Width - 1);
        imgY = Math.Clamp(imgY, 0, _originalMat.Height - 1);
        
        InfoText.Text = $"Clicked at ({imgX}, {imgY})...";
        
        if (FloodFillRadio.IsChecked == true)
        {
            ApplyFloodFill(imgX, imgY);
        }
        else if (GrabCutRadio.IsChecked == true)
        {
            ApplyGrabCut(imgX, imgY);
        }
    }

    private void ApplyFloodFill(int x, int y)
    {
        if (_originalMat == null) return;
        
        try
        {
            var tolerance = (int)ToleranceSlider.Value;
            
            // Créer le masque (doit être 2 pixels plus grand que l'image)
            _maskMat?.Dispose();
            _maskMat = new Mat(_originalMat.Height + 2, _originalMat.Width + 2, MatType.CV_8UC1, Scalar.Black);
            
            // FloodFill
            CvRect rect;
            var diff = new Scalar(tolerance, tolerance, tolerance);
            
            Cv2.FloodFill(
                _originalMat.Clone(), // Clone pour ne pas modifier l'original
                _maskMat,
                new CvPoint(x, y),
                new Scalar(255, 0, 255), // Couleur de remplissage (non utilisée car on veut juste le masque)
                out rect,
                diff,
                diff,
                FloodFillFlags.MaskOnly | FloodFillFlags.Link4);
            
            // Recadrer le masque (enlever les 2 pixels de bordure)
            var croppedMask = _maskMat[1, _originalMat.Height + 1, 1, _originalMat.Width + 1].Clone();
            _maskMat.Dispose();
            _maskMat = croppedMask;
            
            // Convertir en masque binaire propre
            Cv2.Threshold(_maskMat, _maskMat, 0, 255, ThresholdTypes.Binary);
            
            // Inverser si demandé (par défaut le flood fill marque le fond, on veut l'objet)
            if (InvertMaskCheckBox.IsChecked != true)
            {
                Cv2.BitwiseNot(_maskMat, _maskMat);
            }
            
            // Nettoyer le masque (morphologie)
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(3, 3));
            Cv2.MorphologyEx(_maskMat, _maskMat, MorphTypes.Close, kernel);
            Cv2.MorphologyEx(_maskMat, _maskMat, MorphTypes.Open, kernel);
            
            // Extraire le contour
            ExtractContour();
            
            // Mettre à jour l'affichage
            UpdatePreview();
            
            var contourInfo = _contourPoints != null ? $"{_contourPoints.Length} points" : "none";
            InfoText.Text = $"✅ Mask created! Contour: {contourInfo}";
            InfoText.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 100));
            
            ApplyButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            InfoText.Text = $"❌ Error: {ex.Message}";
            InfoText.Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100));
        }
    }

    private void ApplyGrabCut(int x, int y)
    {
        if (_originalMat == null) return;
        
        try
        {
            InfoText.Text = "Processing GrabCut (may take a moment)...";
            
            // GrabCut nécessite une estimation initiale du rectangle foreground
            // On utilise le clic comme point de fond, et on suppose que l'objet est au centre
            var margin = Math.Min(_originalMat.Width, _originalMat.Height) / 10;
            var rect = new CvRect(margin, margin, _originalMat.Width - 2 * margin, _originalMat.Height - 2 * margin);
            
            using var bgdModel = new Mat();
            using var fgdModel = new Mat();
            _maskMat?.Dispose();
            _maskMat = new Mat(_originalMat.Size(), MatType.CV_8UC1, Scalar.All(0));
            
            // GrabCut constants
            const int GC_BGD = 0;      // Background
            const int GC_FGD = 1;      // Foreground
            const int GC_PR_BGD = 2;   // Probable background
            const int GC_PR_FGD = 3;   // Probable foreground
            
            // Initialiser avec GC_INIT_WITH_RECT
            _maskMat.SetTo(new Scalar(GC_BGD)); // Tout en fond
            _maskMat[rect].SetTo(new Scalar(GC_PR_FGD)); // Rectangle probable foreground
            
            // Le point cliqué est définitivement fond
            Cv2.Circle(_maskMat, new CvPoint(x, y), 10, new Scalar(GC_BGD), -1);
            
            Cv2.GrabCut(_originalMat, _maskMat, rect, bgdModel, fgdModel, 3, GrabCutModes.InitWithMask);
            
            // Convertir le masque GrabCut en binaire
            using var mask2 = new Mat();
            Cv2.Compare(_maskMat, new Scalar(GC_FGD), mask2, CmpType.EQ);
            using var mask3 = new Mat();
            Cv2.Compare(_maskMat, new Scalar(GC_PR_FGD), mask3, CmpType.EQ);
            
            Cv2.BitwiseOr(mask2, mask3, _maskMat);
            
            // Extraire le contour
            ExtractContour();
            
            // Mettre à jour l'affichage
            UpdatePreview();
            
            var contourInfo = _contourPoints != null ? $"{_contourPoints.Length} points" : "none";
            InfoText.Text = $"✅ GrabCut done! Contour: {contourInfo}";
            InfoText.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 100));
            
            ApplyButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            InfoText.Text = $"❌ GrabCut error: {ex.Message}";
            InfoText.Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100));
        }
    }

    private void ApplyAutoThreshold()
    {
        if (_originalMat == null) return;
        
        try
        {
            // Convertir en grayscale
            using var gray = _originalMat.Channels() == 1 
                ? _originalMat.Clone() 
                : _originalMat.CvtColor(ColorConversionCodes.BGR2GRAY);
            
            // Threshold Otsu
            _maskMat?.Dispose();
            _maskMat = new Mat();
            Cv2.Threshold(gray, _maskMat, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            
            // Inverser si nécessaire (on veut l'objet en blanc)
            // Généralement l'objet est plus sombre que le fond
            var whitePixels = Cv2.CountNonZero(_maskMat);
            var totalPixels = _maskMat.Width * _maskMat.Height;
            if (whitePixels > totalPixels / 2)
            {
                Cv2.BitwiseNot(_maskMat, _maskMat);
            }
            
            // Nettoyer
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(5, 5));
            Cv2.MorphologyEx(_maskMat, _maskMat, MorphTypes.Close, kernel);
            Cv2.MorphologyEx(_maskMat, _maskMat, MorphTypes.Open, kernel);
            
            // Extraire le contour
            ExtractContour();
            
            // Mettre à jour l'affichage
            UpdatePreview();
            
            var contourInfo = _contourPoints != null ? $"{_contourPoints.Length} points" : "none";
            InfoText.Text = $"✅ Auto threshold done! Contour: {contourInfo}";
            InfoText.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 100));
            
            ApplyButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            InfoText.Text = $"❌ Threshold error: {ex.Message}";
            InfoText.Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100));
        }
    }

    private void ExtractContour()
    {
        if (_maskMat == null) return;
        
        Cv2.FindContours(_maskMat, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        
        if (contours.Length == 0)
        {
            _contourPoints = null;
            return;
        }
        
        // Prendre le plus grand contour
        var largest = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
        
        // Simplifier
        var epsilon = 0.005 * Cv2.ArcLength(largest, true);
        var simplified = Cv2.ApproxPolyDP(largest, epsilon, true);
        
        _contourPoints = simplified.Select(p => new CorePointF(p.X, p.Y)).ToArray();
    }

    private void UpdatePreview()
    {
        if (_originalMat == null) return;
        
        // Image de base
        using var display = _originalMat.Clone();
        
        // Dessiner le contour si demandé
        if (ShowContourCheckBox.IsChecked == true && _contourPoints != null && _contourPoints.Length > 0)
        {
            var cvPoints = _contourPoints.Select(p => new CvPoint((int)p.X, (int)p.Y)).ToArray();
            Cv2.Polylines(display, new[] { cvPoints }, true, new Scalar(0, 255, 0), 2);
            
            // Points
            foreach (var p in cvPoints)
            {
                Cv2.Circle(display, p, 4, new Scalar(0, 255, 255), -1);
            }
        }
        
        // Afficher
        PreviewImage.Source = MatToBitmapSource(display);
        
        // Masque overlay
        if (ShowMaskCheckBox.IsChecked == true && _maskMat != null)
        {
            using var colorMask = new Mat();
            Cv2.CvtColor(_maskMat, colorMask, ColorConversionCodes.GRAY2BGR);
            
            // Colorer en vert
            using var greenMask = new Mat(colorMask.Size(), colorMask.Type(), new Scalar(0, 100, 0));
            Cv2.BitwiseAnd(greenMask, colorMask, greenMask);
            
            MaskOverlay.Source = MatToBitmapSource(greenMask);
            MaskOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            MaskOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private static BitmapSource MatToBitmapSource(Mat mat)
    {
        var format = mat.Channels() switch
        {
            1 => PixelFormats.Gray8,
            3 => PixelFormats.Bgr24,
            4 => PixelFormats.Bgra32,
            _ => PixelFormats.Bgr24
        };
        
        var bitmap = new WriteableBitmap(mat.Width, mat.Height, 96, 96, format, null);
        bitmap.Lock();
        
        var dataSize = (int)(mat.Step() * mat.Height);
        var srcPtr = mat.Data;
        var dstPtr = bitmap.BackBuffer;
        
        // Copy sans unsafe
        var buffer = new byte[dataSize];
        System.Runtime.InteropServices.Marshal.Copy(srcPtr, buffer, 0, dataSize);
        System.Runtime.InteropServices.Marshal.Copy(buffer, 0, dstPtr, dataSize);
        
        bitmap.AddDirtyRect(new Int32Rect(0, 0, mat.Width, mat.Height));
        bitmap.Unlock();
        
        return bitmap;
    }

    private void MethodRadio_Changed(object sender, RoutedEventArgs e)
    {
        if (FloodFillSettings == null) return;
        
        FloodFillSettings.Visibility = FloodFillRadio.IsChecked == true 
            ? Visibility.Visible 
            : Visibility.Collapsed;
        
        if (AutoThresholdRadio.IsChecked == true)
        {
            ApplyAutoThreshold();
        }
        else
        {
            // Reset pour les méthodes interactives
            InfoText.Text = "Click on the background...";
            InfoText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
        }
    }

    private void ToleranceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ToleranceText != null)
        {
            ToleranceText.Text = $"{(int)ToleranceSlider.Value}";
        }
    }

    private void InvertMaskCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        // Recalculer si on a déjà un masque
        // Pour simplifier, on reset
    }

    private void ShowMaskCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
    }

    private void ShowContourCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _maskMat?.Dispose();
        _maskMat = null;
        _contourPoints = null;
        
        ApplyButton.IsEnabled = false;
        InfoText.Text = "Click on the background...";
        InfoText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
        
        UpdatePreview();
    }

    private void LoadImageButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Load Image",
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.tiff|All files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                // Charger l'image
                var newMat = Cv2.ImRead(dialog.FileName, ImreadModes.Color);
                
                if (newMat.Empty())
                {
                    MessageBox.Show("Failed to load image", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Remplacer l'image originale
                _originalMat?.Dispose();
                _originalMat = newMat;
                
                // Reset le masque
                _maskMat?.Dispose();
                _maskMat = null;
                _contourPoints = null;
                
                ApplyButton.IsEnabled = false;
                InfoText.Text = $"Loaded: {System.IO.Path.GetFileName(dialog.FileName)} ({_originalMat.Width}x{_originalMat.Height})";
                InfoText.Foreground = new SolidColorBrush(Color.FromRgb(0, 206, 209));
                
                UpdatePreview();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_maskMat != null)
        {
            // Convertir le masque en Frame
            var data = new byte[_maskMat.Total() * _maskMat.ElemSize()];
            System.Runtime.InteropServices.Marshal.Copy(_maskMat.Data, data, 0, data.Length);
            ResultMask = new Frame(data, _maskMat.Width, _maskMat.Height, CorePixelFormat.Gray8, 0, (int)_maskMat.Step());
        }
        
        Applied = true;
        DialogResult = true;
        Close();
    }

    #region Zoom and Pan

    private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Zoom avec Ctrl + molette OU juste molette
        e.Handled = true;
        
        if (e.Delta > 0)
            ZoomIn();
        else
            ZoomOut();
    }

    private void ImageScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Pan avec clic molette OU clic droit
        if (e.MiddleButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed)
        {
            _isPanning = true;
            _panStart = e.GetPosition(ImageScrollViewer);
            _panStartOffsetX = ImageScrollViewer.HorizontalOffset;
            _panStartOffsetY = ImageScrollViewer.VerticalOffset;
            ImageScrollViewer.Cursor = Cursors.SizeAll;
            Mouse.Capture(ImageScrollViewer);
            e.Handled = true;
        }
    }

    private void ImageScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_isPanning)
        {
            var pos = e.GetPosition(ImageScrollViewer);
            var deltaX = pos.X - _panStart.X;
            var deltaY = pos.Y - _panStart.Y;
            
            ImageScrollViewer.ScrollToHorizontalOffset(_panStartOffsetX - deltaX);
            ImageScrollViewer.ScrollToVerticalOffset(_panStartOffsetY - deltaY);
            e.Handled = true;
        }
    }

    private void ImageScrollViewer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning && (e.MiddleButton == MouseButtonState.Released || e.RightButton == MouseButtonState.Released))
        {
            _isPanning = false;
            ImageScrollViewer.Cursor = Cursors.Arrow;
            Mouse.Capture(null);
            e.Handled = true;
        }
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => ZoomIn();
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => ZoomOut();
    
    private void Zoom100_Click(object sender, RoutedEventArgs e)
    {
        SetZoom(1.0);
    }

    private void ZoomFit_Click(object sender, RoutedEventArgs e)
    {
        if (_originalMat == null) return;
        
        var containerWidth = ImageScrollViewer.ActualWidth - 20; // Marge pour scrollbars
        var containerHeight = ImageScrollViewer.ActualHeight - 20;
        
        var scaleX = containerWidth / _originalMat.Width;
        var scaleY = containerHeight / _originalMat.Height;
        
        SetZoom(Math.Min(scaleX, scaleY));
    }

    private void ZoomIn()
    {
        SetZoom(_currentZoom + ZoomStep);
    }

    private void ZoomOut()
    {
        SetZoom(_currentZoom - ZoomStep);
    }

    private void SetZoom(double zoom)
    {
        _currentZoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        
        ImageScale.ScaleX = _currentZoom;
        ImageScale.ScaleY = _currentZoom;
        
        ZoomText.Text = $"{_currentZoom:P0}";
    }

    #endregion

    protected override void OnClosed(EventArgs e)
    {
        _originalMat?.Dispose();
        _maskMat?.Dispose();
        _originalFrame.Dispose();
        base.OnClosed(e);
    }
}
