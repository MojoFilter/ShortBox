#if WINDOWS
using Windows.Storage.Streams;
#endif

namespace ShortBoxMobile;

public partial class CoverView : ContentView
{
	public CoverView()
	{
		InitializeComponent();
	}

	public static readonly BindableProperty BookIdProperty =
        BindableProperty.Create(
        nameof(BookId),
        typeof(int),
        typeof(CoverView),
        default(int));

    public int BookId
    {
        get => (int)GetValue(BookIdProperty); 
        set => SetValue(BookIdProperty, value);
    }

    private async void ContentView_Loaded(object sender, EventArgs e)
    {
        var client = Application.Current.Handler.MauiContext.Services.GetRequiredService<IShortBoxReaderClient>();
        try
        {
            ImageSource imageSource;
            using var rawStream = await client.GetBookCoverAsync(this.BookId, 250, CancellationToken.None);
#if WINDOWS
            var raStream = new InMemoryRandomAccessStream();
            using (var outputStream = raStream.GetOutputStreamAt(0))
            {
                await rawStream.CopyToAsync(outputStream.AsStreamForWrite()).ConfigureAwait(false);
                await outputStream.FlushAsync();
            }
            raStream.Seek(0);
            imageSource = ImageSource.FromStream(() => raStream.AsStream());
#else
            imageSource = ImageSource.FromStream(() => rawStream);
#endif
        }
        catch (Exception ex)
        {
            // couldn't load that one.
        }
    }
}