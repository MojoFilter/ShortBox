using ShortBox.Api.Data;
using System.Globalization;

namespace ShortBoxMobile;

internal class BookCoverUriConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        Book book => $"http://gordon:5000/book/{book.Id}/cover?height=250",
        _ => default
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
