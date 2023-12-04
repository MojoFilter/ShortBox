using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace ShortBoxDesktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection()
            .AddShortBoxCommunication(cfg =>
            {
                cfg.BaseAddress = new Uri("http://jweeks-spector:5000/");
                cfg.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddSingleton<ICombineSeriesViewModel, CombineSeriesViewModel>()
            .AddTransient<CombineSeriesView>()
            .AddTransient<MainWindow>()
            .BuildServiceProvider();

        this.MainWindow = services.GetRequiredService<MainWindow>();
        this.MainWindow.Show();
    }
}
