﻿using CommunityToolkit.Maui;
using MauiPageFullScreen;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ShortBoxMobile
{
    public static class MauiProgram
    {
        public static string Host { get; set; }

        public static MauiApp CreateMauiApp()
        {
            var inIt = Debugger.IsAttached;
            var builder = MauiApp.CreateBuilder();
            Host = DeviceInfo.Platform switch
            {
                //var p when p == DevicePlatform.Android && Debugger.IsAttached => "10.0.2.2",
                var p when p == DevicePlatform.Android => "192.168.86.31",
                _ => "jweeks-spector"
            };

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
                    .AddShortBoxCommunication(client =>
                    {
                        client.BaseAddress = new Uri($"http://{Host}:5000/");
                        client.Timeout = TimeSpan.FromSeconds(10);
                    });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
