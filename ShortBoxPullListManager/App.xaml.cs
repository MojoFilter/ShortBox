using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace ShortBoxPullListManager;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddUserSecrets<App>()
            .Build();

        var services = new ServiceCollection()
            .AddMarvelApi(configuration)
            .AddShortBoxCommunication(configuration)
            .AddShortBoxClient()
            .AddTransient<MainViewModel>()
            .AddSingleton<MainWindow>()
            .BuildServiceProvider();

        this.MainWindow = services.GetRequiredService<MainWindow>();
        this.MainWindow.Show();
    }
}
