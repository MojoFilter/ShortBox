namespace ShortBoxMobile;

public sealed class ViewModelFactory
{
    public ViewModelFactory(IServiceProvider services)
    {
        _services = services;
    }

    public SeriesPageViewModel CreateSeriesPageViewModel() => _services.GetService<SeriesPageViewModel>();

    private readonly IServiceProvider _services;
}
