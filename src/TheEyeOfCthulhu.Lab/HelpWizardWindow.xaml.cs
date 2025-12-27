using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TheEyeOfCthulhu.Lab;

public partial class HelpWizardWindow : Window
{
    private int _currentPage = 1;
    private const int TotalPages = 5;
    
    private readonly StackPanel[] _pages;
    private readonly Ellipse[] _dots;

    public bool DontShowAgain => DontShowAgainCheckBox.IsChecked == true;

    public HelpWizardWindow()
    {
        InitializeComponent();
        
        _pages = new[] { Page1, Page2, Page3, Page4, Page5 };
        _dots = new[] { Dot1, Dot2, Dot3, Dot4, Dot5 };
        
        UpdateUI();
    }

    private void PrevButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage > 1)
        {
            _currentPage--;
            UpdateUI();
        }
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage < TotalPages)
        {
            _currentPage++;
            UpdateUI();
        }
        else
        {
            // Dernière page -> fermer
            DialogResult = true;
            Close();
        }
    }

    private void UpdateUI()
    {
        // Afficher la bonne page
        for (int i = 0; i < _pages.Length; i++)
        {
            _pages[i].Visibility = (i == _currentPage - 1) ? Visibility.Visible : Visibility.Collapsed;
        }
        
        // Mettre à jour les dots
        var activeBrush = new SolidColorBrush(Color.FromRgb(78, 201, 176)); // #4EC9B0
        var inactiveBrush = new SolidColorBrush(Color.FromRgb(68, 68, 68));  // #444
        
        for (int i = 0; i < _dots.Length; i++)
        {
            _dots[i].Fill = (i == _currentPage - 1) ? activeBrush : inactiveBrush;
        }
        
        // Boutons
        PrevButton.IsEnabled = _currentPage > 1;
        
        if (_currentPage == TotalPages)
        {
            NextButton.Content = "✓ Terminer";
        }
        else
        {
            NextButton.Content = "Suivant ▶";
        }
    }
}
