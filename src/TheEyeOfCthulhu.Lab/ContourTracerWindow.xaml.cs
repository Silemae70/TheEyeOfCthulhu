using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using OpenCvSharp;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Sources.Processors;
using CvPoint = OpenCvSharp.Point;
using WpfPoint = System.Windows.Point;
using CorePointF = TheEyeOfCthulhu.Core.PointF;
using CorePixelFormat = TheEyeOfCthulhu.Core.PixelFormat;
using CoreFrame = TheEyeOfCthulhu.Core.Frame;

namespace TheEyeOfCthulhu.Lab;

/// <summary>
/// Fenêtre pour tracer manuellement le contour d'un objet à détecter.
/// Mode AR : l'utilisateur dessine directement sur l'image figée.
/// </summary>
public partial class ContourTracerWindow : System.Windows.Window
{
    private readonly CoreFrame _frozenFrame;
    private readonly Mat _frozenMat;
    private readonly List<WpfPoint> _drawnPoints = new();
    private readonly List<Ellipse> _pointMarkers = new();
    private readonly List<Polyline> _detectedContourLines = new();
    
    private bool _isDrawingFreehand;
    private bool _contourClosed;
    
    // Mode sélection de contour
    private bool _contourSelectionMode;
    private List<CvPoint[]>? _detectedContours;
    private int _selectedContourIndex = -1;
    
    // Zoom/Pan
    private double _currentZoom = 1.0;
    private const double ZoomStep = 0.2;
    private const double MinZoom = 0.1;
    private const double MaxZoom = 10.0;
    private bool _isPanning;
    private WpfPoint _panStart;
    private double _panStartOffsetX;
    private double _panStartOffsetY;
    
    // Couleurs pour les contours détectés
    private static readonly Color[] ContourColors = {
        Colors.Red, Colors.Lime, Colors.Cyan, Colors.Magenta, 
        Colors.Yellow, Colors.Orange, Colors.Pink, Colors.LightBlue,
        Colors.LightGreen, Colors.Coral, Colors.Gold, Colors.Violet
    };
    
    public CorePointF[]? ResultContour { get; private set; }
    public CoreFrame? ResultMask { get; private set; }
    public CoreFrame? ResultTemplate { get; private set; }
    public bool Applied { get; private set; }

    public ContourTracerWindow(CoreFrame frame)
    {
        InitializeComponent();
        
        _frozenFrame = frame.Clone();
        _frozenMat = FrameMatConverter.ToMat(_frozenFrame);
        
        FrozenImage.Source = FrameToBitmapSource(_frozenFrame);
        FrozenImage.Width = _frozenFrame.Width;
        FrozenImage.Height = _frozenFrame.Height;
        
        DrawingCanvas.Width = _frozenFrame.Width;
        DrawingCanvas.Height = _frozenFrame.Height;
        
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        ZoomFit_Click(sender, e);
    }

    #region Drawing Events

    private void DrawingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning) return;
        
        var pos = e.GetPosition(DrawingCanvas);
        
        // Mode sélection de contour
        if (_contourSelectionMode && _detectedContours != null)
        {
            SelectContourAtPoint(pos);
            return;
        }
        
        if (_contourClosed) return;
        
        if (pos.X < 0 || pos.Y < 0 || 
            pos.X >= _frozenFrame.Width || pos.Y >= _frozenFrame.Height)
        {
            return;
        }
        
        if (PointModeRadio.IsChecked == true)
        {
            AddPoint(pos);
        }
        else
        {
            _isDrawingFreehand = true;
            _drawnPoints.Clear();
            ClearPointMarkers();
            AddPoint(pos);
            DrawingCanvas.CaptureMouse();
        }
    }

    private void DrawingCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isPanning) return;
        
        var pos = e.GetPosition(DrawingCanvas);
        
        // Mode sélection : highlight au survol
        if (_contourSelectionMode && _detectedContours != null)
        {
            HighlightContourAtPoint(pos);
            return;
        }
        
        if (_isDrawingFreehand && e.LeftButton == MouseButtonState.Pressed)
        {
            if (_drawnPoints.Count == 0 || Distance(_drawnPoints.Last(), pos) > 3)
            {
                if (pos.X >= 0 && pos.Y >= 0 && 
                    pos.X < _frozenFrame.Width && pos.Y < _frozenFrame.Height)
                {
                    AddPoint(pos);
                }
            }
        }
        
        if (_drawnPoints.Count >= 2 && !_contourClosed && !_isDrawingFreehand)
        {
            var first = _drawnPoints[0];
            ClosingLine.X1 = pos.X;
            ClosingLine.Y1 = pos.Y;
            ClosingLine.X2 = first.X;
            ClosingLine.Y2 = first.Y;
            ClosingLine.Visibility = Visibility.Visible;
        }
    }

    private void DrawingCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDrawingFreehand)
        {
            _isDrawingFreehand = false;
            DrawingCanvas.ReleaseMouseCapture();
            
            if (_drawnPoints.Count >= 10)
            {
                CloseContour();
            }
        }
    }

    private void DrawingCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Annuler le mode sélection
        if (_contourSelectionMode)
        {
            ExitContourSelectionMode();
            e.Handled = true;
            return;
        }
        
        if (_drawnPoints.Count >= 3 && !_contourClosed)
        {
            CloseContour();
            e.Handled = true;
        }
    }

    #endregion

    #region Contour Selection Mode

    private void SelectContourAtPoint(WpfPoint pos)
    {
        if (_detectedContours == null) return;
        
        // Trouver le plus PETIT contour qui contient le point
        int bestIndex = -1;
        double bestArea = double.MaxValue;
        
        for (int i = 0; i < _detectedContours.Count; i++)
        {
            var contour = _detectedContours[i];
            var result = Cv2.PointPolygonTest(contour, new Point2f((float)pos.X, (float)pos.Y), false);
            
            if (result >= 0) // Point inside or on contour
            {
                var area = Cv2.ContourArea(contour);
                if (area < bestArea)
                {
                    bestArea = area;
                    bestIndex = i;
                }
            }
        }
        
        if (bestIndex >= 0)
        {
            _selectedContourIndex = bestIndex;
            UseSelectedContour();
        }
    }

    private void HighlightContourAtPoint(WpfPoint pos)
    {
        if (_detectedContours == null) return;
        
        // Trouver le plus PETIT contour qui contient le point
        int hoveredIndex = -1;
        double bestArea = double.MaxValue;
        
        for (int i = 0; i < _detectedContours.Count; i++)
        {
            var contour = _detectedContours[i];
            var result = Cv2.PointPolygonTest(contour, new Point2f((float)pos.X, (float)pos.Y), false);
            
            if (result >= 0)
            {
                var area = Cv2.ContourArea(contour);
                if (area < bestArea)
                {
                    bestArea = area;
                    hoveredIndex = i;
                }
            }
        }
        
        // Mettre à jour l'épaisseur des polylines
        for (int i = 0; i < _detectedContourLines.Count; i++)
        {
            _detectedContourLines[i].StrokeThickness = (i == hoveredIndex) ? 4 : 2;
            _detectedContourLines[i].Opacity = (i == hoveredIndex) ? 1.0 : 0.6;
        }
        
        if (hoveredIndex >= 0)
        {
            var area = Cv2.ContourArea(_detectedContours[hoveredIndex]);
            InfoText.Text = $"Contour #{hoveredIndex + 1}\nAire: {area:F0} px²\n\n👆 Clic pour sélectionner";
        }
        else
        {
            InfoText.Text = "Survolez un contour pour le sélectionner";
        }
    }

    private void UseSelectedContour()
    {
        if (_detectedContours == null || _selectedContourIndex < 0) return;
        
        var selectedContour = _detectedContours[_selectedContourIndex];
        
        // Quitter le mode sélection
        ExitContourSelectionMode();
        
        // Utiliser ce contour
        _drawnPoints.Clear();
        ClearPointMarkers();
        
        foreach (var pt in selectedContour)
        {
            _drawnPoints.Add(new WpfPoint(pt.X, pt.Y));
        }
        
        if (ShowPointsCheckBox.IsChecked == true)
        {
            foreach (var p in _drawnPoints)
            {
                var marker = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = Brushes.Yellow,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };
                Canvas.SetLeft(marker, p.X - 4);
                Canvas.SetTop(marker, p.Y - 4);
                DrawingCanvas.Children.Add(marker);
                _pointMarkers.Add(marker);
            }
        }
        
        RedrawContour();
        PointCountText.Text = $"Points: {_drawnPoints.Count}";
        UndoButton.IsEnabled = true;
        
        CloseContour();
        
        InfoText.Text = $"✅ Contour #{_selectedContourIndex + 1} sélectionné!";
    }

    private void ExitContourSelectionMode()
    {
        _contourSelectionMode = false;
        _detectedContours = null;
        _selectedContourIndex = -1;
        
        // Supprimer les polylines de contours
        foreach (var line in _detectedContourLines)
        {
            DrawingCanvas.Children.Remove(line);
        }
        _detectedContourLines.Clear();
        
        InstructionText.Text = "🖱️ Clic gauche = Point | Clic droit = Fermer | Molette = Zoom";
        InstructionText.Foreground = new SolidColorBrush(Color.FromRgb(78, 201, 176));
        
        AutoDetectButton.Content = "✨ Détecter contours";
    }

    #endregion

    #region Zoom/Pan

    private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        
        if (e.Delta > 0)
            SetZoom(_currentZoom + ZoomStep);
        else
            SetZoom(_currentZoom - ZoomStep);
    }

    private void ImageScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton == MouseButtonState.Pressed)
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
        if (_isPanning && e.MiddleButton == MouseButtonState.Pressed)
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
        if (_isPanning && e.MiddleButton == MouseButtonState.Released)
        {
            _isPanning = false;
            ImageScrollViewer.Cursor = Cursors.Arrow;
            Mouse.Capture(null);
            e.Handled = true;
        }
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(_currentZoom + ZoomStep);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(_currentZoom - ZoomStep);
    private void Zoom100_Click(object sender, RoutedEventArgs e) => SetZoom(1.0);

    private void ZoomFit_Click(object sender, RoutedEventArgs e)
    {
        var containerWidth = ImageScrollViewer.ActualWidth - 20;
        var containerHeight = ImageScrollViewer.ActualHeight - 20;
        
        if (containerWidth <= 0 || containerHeight <= 0) return;
        
        var scaleX = containerWidth / _frozenFrame.Width;
        var scaleY = containerHeight / _frozenFrame.Height;
        
        SetZoom(Math.Min(scaleX, scaleY));
    }

    private void SetZoom(double zoom)
    {
        _currentZoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        ImageScale.ScaleX = _currentZoom;
        ImageScale.ScaleY = _currentZoom;
        ZoomText.Text = $"{_currentZoom:P0}";
    }

    #endregion

    #region Contour Management

    private void AddPoint(WpfPoint imagePoint)
    {
        _drawnPoints.Add(imagePoint);
        
        if (ShowPointsCheckBox.IsChecked == true)
        {
            var marker = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = Brushes.Yellow,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            Canvas.SetLeft(marker, imagePoint.X - 4);
            Canvas.SetTop(marker, imagePoint.Y - 4);
            DrawingCanvas.Children.Add(marker);
            _pointMarkers.Add(marker);
        }
        
        RedrawContour();
        
        PointCountText.Text = $"Points: {_drawnPoints.Count}";
        UndoButton.IsEnabled = _drawnPoints.Count > 0;
    }

    private void RedrawContour()
    {
        var points = new PointCollection();
        foreach (var p in _drawnPoints)
        {
            points.Add(p);
        }
        ContourPolyline.Points = points;
    }

    private void CloseContour()
    {
        if (_drawnPoints.Count < 3) return;
        
        _contourClosed = true;
        ClosingLine.Visibility = Visibility.Collapsed;
        
        var finalPoints = _drawnPoints.ToList();
        if (SimplifyCheckBox.IsChecked == true && finalPoints.Count > 10)
        {
            finalPoints = SimplifyContour(finalPoints, 1.0 / 100.0);
        }
        
        ResultContour = finalPoints.Select(p => new CorePointF((float)p.X, (float)p.Y)).ToArray();
        
        CreateMaskAndTemplate();
        
        InstructionText.Text = $"✅ Contour fermé ! {ResultContour.Length} points";
        InstructionText.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 100));
        
        InfoText.Text = $"Contour: {ResultContour.Length} points\nBounding box: {GetBoundingBox(ResultContour)}";
        
        ApplyButton.IsEnabled = true;
        
        UpdateContourPreview();
    }

    private List<WpfPoint> SimplifyContour(List<WpfPoint> points, double epsilon)
    {
        var cvPoints = points.Select(p => new CvPoint((int)p.X, (int)p.Y)).ToArray();
        var simplified = Cv2.ApproxPolyDP(cvPoints, epsilon * Cv2.ArcLength(cvPoints, true), true);
        return simplified.Select(p => new WpfPoint(p.X, p.Y)).ToList();
    }

    private void CreateMaskAndTemplate()
    {
        if (ResultContour == null || ResultContour.Length < 3) return;
        
        using var mask = new Mat(_frozenMat.Height, _frozenMat.Width, MatType.CV_8UC1, Scalar.Black);
        var cvPoints = ResultContour.Select(p => new CvPoint((int)p.X, (int)p.Y)).ToArray();
        Cv2.FillPoly(mask, new[] { cvPoints }, Scalar.White);
        
        var maskData = new byte[mask.Total()];
        System.Runtime.InteropServices.Marshal.Copy(mask.Data, maskData, 0, maskData.Length);
        ResultMask = new CoreFrame(maskData, mask.Width, mask.Height, CorePixelFormat.Gray8, 0, (int)mask.Step());
        
        var rect = Cv2.BoundingRect(cvPoints);
        rect = ClampRect(rect, _frozenMat.Width, _frozenMat.Height);
        
        if (rect.Width > 0 && rect.Height > 0)
        {
            using var templateMat = new Mat(_frozenMat, rect);
            ResultTemplate = FrameMatConverter.ToFrame(templateMat, 0);
        }
    }

    private static OpenCvSharp.Rect ClampRect(OpenCvSharp.Rect rect, int maxWidth, int maxHeight)
    {
        var x = Math.Max(0, rect.X);
        var y = Math.Max(0, rect.Y);
        var w = Math.Min(rect.Width, maxWidth - x);
        var h = Math.Min(rect.Height, maxHeight - y);
        return new OpenCvSharp.Rect(x, y, w, h);
    }

    private string GetBoundingBox(CorePointF[] points)
    {
        var minX = points.Min(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxX = points.Max(p => p.X);
        var maxY = points.Max(p => p.Y);
        return $"{(int)(maxX - minX)}x{(int)(maxY - minY)}";
    }

    private void UpdateContourPreview()
    {
        if (ResultContour == null) return;
        
        using var preview = new Mat(100, 100, MatType.CV_8UC3, Scalar.Black);
        
        var minX = ResultContour.Min(p => p.X);
        var minY = ResultContour.Min(p => p.Y);
        var maxX = ResultContour.Max(p => p.X);
        var maxY = ResultContour.Max(p => p.Y);
        var width = maxX - minX;
        var height = maxY - minY;
        
        if (width <= 0 || height <= 0) return;
        
        var scale = Math.Min(90.0 / width, 90.0 / height);
        var offsetX = (100 - width * scale) / 2 - minX * scale;
        var offsetY = (100 - height * scale) / 2 - minY * scale;
        
        var scaledPoints = ResultContour
            .Select(p => new CvPoint((int)(p.X * scale + offsetX), (int)(p.Y * scale + offsetY)))
            .ToArray();
        
        Cv2.Polylines(preview, new[] { scaledPoints }, true, new Scalar(0, 255, 0), 2);
        
        ContourPreview.Source = MatToBitmapSource(preview);
    }

    #endregion

    #region Button Handlers

    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        if (_drawnPoints.Count == 0 || _contourClosed) return;
        
        _drawnPoints.RemoveAt(_drawnPoints.Count - 1);
        
        if (_pointMarkers.Count > 0)
        {
            var marker = _pointMarkers.Last();
            DrawingCanvas.Children.Remove(marker);
            _pointMarkers.RemoveAt(_pointMarkers.Count - 1);
        }
        
        RedrawContour();
        PointCountText.Text = $"Points: {_drawnPoints.Count}";
        UndoButton.IsEnabled = _drawnPoints.Count > 0;
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        // Quitter le mode sélection si actif
        if (_contourSelectionMode)
        {
            ExitContourSelectionMode();
        }
        
        _drawnPoints.Clear();
        ClearPointMarkers();
        ContourPolyline.Points.Clear();
        ClosingLine.Visibility = Visibility.Collapsed;
        
        _contourClosed = false;
        _isDrawingFreehand = false;
        ResultContour = null;
        ResultMask?.Dispose();
        ResultMask = null;
        ResultTemplate?.Dispose();
        ResultTemplate = null;
        
        PointCountText.Text = "Points: 0";
        UndoButton.IsEnabled = false;
        ApplyButton.IsEnabled = false;
        ContourPreview.Source = null;
        InfoText.Text = "";
        
        InstructionText.Text = "🖱️ Clic gauche = Point | Clic droit = Fermer | Molette = Zoom";
        InstructionText.Foreground = new SolidColorBrush(Color.FromRgb(78, 201, 176));
    }

    private void AutoDetectButton_Click(object sender, RoutedEventArgs e)
    {
        // Si déjà en mode sélection, quitter
        if (_contourSelectionMode)
        {
            ExitContourSelectionMode();
            return;
        }
        
        try
        {
            // Clear previous
            ClearButton_Click(sender, e);
            
            using var gray = _frozenMat.Channels() == 1 
                ? _frozenMat.Clone() 
                : _frozenMat.CvtColor(ColorConversionCodes.BGR2GRAY);
            
            using var blurred = new Mat();
            Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(5, 5), 0);
            
            using var edges = new Mat();
            Cv2.Canny(blurred, edges, 50, 150);
            
            using var dilated = new Mat();
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
            Cv2.Dilate(edges, dilated, kernel);
            
            // IMPORTANT: Utiliser TREE pour avoir TOUS les contours (y compris intérieurs)
            Cv2.FindContours(dilated, out var contours, out _, 
                RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);
            
            if (contours.Length == 0)
            {
                MessageBox.Show("Aucun contour détecté", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            // Filtrer les contours trop petits
            var validContours = contours
                .Where(c => Cv2.ContourArea(c) > 100)
                .OrderByDescending(c => Cv2.ContourArea(c))
                .Take(12) // Max 12 contours
                .ToList();
            
            if (validContours.Count == 0)
            {
                MessageBox.Show("Aucun contour valide détecté", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            // Simplifier chaque contour
            _detectedContours = new List<CvPoint[]>();
            foreach (var contour in validContours)
            {
                var epsilon = 0.01 * Cv2.ArcLength(contour, true);
                var simplified = Cv2.ApproxPolyDP(contour, epsilon, true);
                _detectedContours.Add(simplified);
            }
            
            // Afficher tous les contours avec des couleurs différentes
            for (int i = 0; i < _detectedContours.Count; i++)
            {
                var color = ContourColors[i % ContourColors.Length];
                var polyline = new Polyline
                {
                    Stroke = new SolidColorBrush(color),
                    StrokeThickness = 2,
                    Opacity = 0.6
                };
                
                var points = new PointCollection();
                foreach (var pt in _detectedContours[i])
                {
                    points.Add(new WpfPoint(pt.X, pt.Y));
                }
                points.Add(new WpfPoint(_detectedContours[i][0].X, _detectedContours[i][0].Y)); // Fermer
                polyline.Points = points;
                
                DrawingCanvas.Children.Add(polyline);
                _detectedContourLines.Add(polyline);
            }
            
            // Entrer en mode sélection
            _contourSelectionMode = true;
            
            InstructionText.Text = $"🎯 {_detectedContours.Count} contours détectés - Cliquez sur celui à utiliser (clic droit = annuler)";
            InstructionText.Foreground = new SolidColorBrush(Colors.Orange);
            
            AutoDetectButton.Content = "❌ Annuler sélection";
            
            InfoText.Text = "Survolez un contour pour le sélectionner";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erreur: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        Applied = true;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    #endregion

    #region UI Events

    private void FreeModeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (InstructionText != null)
            InstructionText.Text = "✍️ Maintenez le clic gauche et dessinez | Relâchez pour fermer auto";
    }

    private void FreeModeRadio_Unchecked(object sender, RoutedEventArgs e)
    {
        if (InstructionText != null)
            InstructionText.Text = "🖱️ Clic gauche = Point | Clic droit = Fermer | Molette = Zoom";
    }

    private void ShowPointsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        foreach (var marker in _pointMarkers)
        {
            marker.Visibility = ShowPointsCheckBox.IsChecked == true 
                ? Visibility.Visible 
                : Visibility.Collapsed;
        }
    }

    #endregion

    #region Helpers

    private void ClearPointMarkers()
    {
        foreach (var marker in _pointMarkers)
        {
            DrawingCanvas.Children.Remove(marker);
        }
        _pointMarkers.Clear();
    }

    private static double Distance(WpfPoint a, WpfPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static BitmapSource FrameToBitmapSource(CoreFrame frame)
    {
        var format = frame.Format switch
        {
            CorePixelFormat.Gray8 => PixelFormats.Gray8,
            CorePixelFormat.Bgr24 => PixelFormats.Bgr24,
            CorePixelFormat.Bgra32 => PixelFormats.Bgra32,
            _ => PixelFormats.Bgr24
        };
        
        return BitmapSource.Create(
            frame.Width, frame.Height,
            96, 96,
            format, null,
            frame.RawBuffer, frame.Stride);
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
        
        var data = new byte[mat.Total() * mat.ElemSize()];
        System.Runtime.InteropServices.Marshal.Copy(mat.Data, data, 0, data.Length);
        
        return BitmapSource.Create(
            mat.Width, mat.Height,
            96, 96,
            format, null,
            data, (int)mat.Step());
    }

    #endregion

    protected override void OnClosed(EventArgs e)
    {
        _frozenMat.Dispose();
        _frozenFrame.Dispose();
        base.OnClosed(e);
    }
}
