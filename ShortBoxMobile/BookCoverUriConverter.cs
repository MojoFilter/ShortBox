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
