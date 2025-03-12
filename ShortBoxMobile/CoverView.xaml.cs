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
            using var memStream = new MemoryStream();
            using (var rawStream = await client.GetBookCoverAsync(this.BookId, 250, CancellationToken.None))
            {
                await rawStream.CopyToAsync(memStream);
                memStream.Position = 0;
            }
            await Dispatcher.DispatchAsync(() =>
            {
#if WINDOWS
                var raStream = memStream.AsRandomAccessStream();
                this.coverImage.Source = ImageSource.FromStream(() => raStream.AsStream());
#else
            this.coverImage.Source = ImageSource.FromStream(() => memStream);
#endif
            });
        }
        catch (Exception ex)
        {
            // couldn't load that one.
        }
    }
}