using Microsoft.Extensions.Options;

namespace ShortBoxMobile;

internal class BookCoverUriConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    { 
        var opt = IPlatformApplication.Current.Services.GetRequiredService<IOptions<ShortBoxAzureOptions>>().Value;
        return value switch
           {
               Book book => $"{opt.BaseAddress}api/book/{book.Id.Value}/cover?code={opt.HostKey}",
               _ => string.Empty
           };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

internal class BookPageUriConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var opt = IPlatformApplication.Current.Services.GetRequiredService<IOptions<ShortBoxAzureOptions>>().Value;
        return values switch
        {
            [int bookId, int pageNumber] => $"{opt.BaseAddress}api/book/{bookId}/{pageNumber}?code={opt.HostKey}",
            _ => default
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}