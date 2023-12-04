using System.Windows;

namespace ShortBoxDesktop
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(CombineSeriesView combineSeriesView)
        {
            InitializeComponent();
            this.Content = combineSeriesView;
        }
    }
}