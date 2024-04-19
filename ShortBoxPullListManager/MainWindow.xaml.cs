using System.Windows;

namespace ShortBoxPullListManager;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel vm)
    {
        this.DataContext = vm;
        InitializeComponent();
    }
}