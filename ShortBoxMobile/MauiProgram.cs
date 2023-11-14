using Microsoft.Extensions.Logging;

namespace ShortBoxMobile
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
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
                    .AddShortBoxCommunication(client => client.BaseAddress = new Uri("http://gordon:5000/"));

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
