namespace ShortBoxMobile;

internal class BookCoverUriConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        Book book => $"http://{MauiProgram.Host}:5000/book/{book.Id.Value}/cover?height=250",
        _ => string.Empty
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

internal class BookPageUriConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values switch
        {
            [int bookId, int pageNumber] => $"http://{MauiProgram.Host}:5000/book/{bookId}/{pageNumber}",
            _ => default
        };

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
