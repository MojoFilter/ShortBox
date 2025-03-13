using CommunityToolkit.Maui;
using MauiPageFullScreen;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace ShortBoxMobile;

public static class MauiProgram
{

    public static MauiApp CreateMauiApp()
    {
        var inIt = Debugger.IsAttached;
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseFullScreen()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .Services
                .AddTransient<AppShell>()
                .AddTransient<SeriesPageViewModel>()
                .AddTransient<SeriesPage>()
                .AddTransient<ViewModelFactory>()
                .AddTransient<BookPage>()
                .AddTransient<BookPageViewModel>()
                .AddTransient<MainPage>()
                .AddTransient<MainPageViewModel>()
                .AddShortBoxClient()
                .AddShortBoxAzure(cfg =>
                {
                    cfg.BaseAddress = MobileSettings.FunctionsBaseAddress;
                    cfg.HostKey = MobileSettings.FunctionsKey;
                });

#if DEBUG
		builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
