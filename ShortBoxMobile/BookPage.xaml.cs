using MauiPageFullScreen;
using ShortBox.Azure;
using ShortBox.Azure.Zoom;
using System.Diagnostics;

namespace ShortBoxMobile;

public partial class BookPage : ContentPage
{
	public BookPage(BookPageViewModel vm)
	{
		InitializeComponent();
		this.BindingContext = vm;
		vm.PropertyChanged += this.OnViewModelPropertyChanged;
	}

	/// <summary>
	/// A new page starts fitted, whatever zoom the last one was left at. Done when the new image arrives rather than when
	/// the page number changes, because the old page stays on screen until then.
	/// </summary>
	private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(BookPageViewModel.PageSource))
		{
			_ = this.zoomView.Reset(animated: false);
		}
	}

	private void OnNavigationRequested(object sender, ZoomNavigationEventArgs e)
	{
		var command = e.Direction == ZoomNavigation.Next ? this.ViewModel.NextPageCommand : this.ViewModel.PreviousPageCommand;
		command.Execute(default);
	}

    protected override void OnAppearing()
    {
		Controls.FullScreen();
    }

	protected override void OnDisappearing()
	{
		Controls.RestoreScreen();
		this.ViewModel?.Suspend();
	}

    private void OnPageTapped(object sender, ZoomTappedEventArgs e)
    {
		var command = GetTapArea(e.Position, this.zoomView) switch
		{
			TapArea.Left => this.ViewModel.PreviousPageCommand,
			TapArea.Right => this.ViewModel.NextPageCommand,
			_ => new AsyncRelayCommand(this.ToggleNavBar)
		};
		command?.Execute(default);
    }

	private async Task ToggleNavBar()
	{
		Shell.SetNavBarIsVisible(this, !Shell.GetNavBarIsVisible(this));
		//if (Shell.GetNavBarIsVisible(this))
		//{
		//	Controls.RestoreScreen();
		//}
		//else
		//{
		//	Controls.FullScreen();
		//}
	}

	private BookPageViewModel ViewModel => this.BindingContext as BookPageViewModel;

	private const double SideAreaPercent = 1.0 / 6.0;

	private TapArea GetTapArea(Point? point, View container) => point switch
	{
		Point p when (p.X < container.Width * SideAreaPercent) => TapArea.Left,
		Point p when (p.X > container.Width - (container.Width * SideAreaPercent)) => TapArea.Right,
		_ => TapArea.Main
	};

	private enum TapArea
	{
		Left, 
		Right,
		Main,
	}
}

//[QueryProperty(nameof(PageNumber), "page")]
[QueryProperty(nameof(BookId), "bookId")]
public sealed partial class BookPageViewModel(IShortBoxReaderClientFactory clientFactory, IPageProvider pages, PagePrefetcher prefetcher) : ObservableObject
{
    [ObservableProperty]
	private int _pageNumber;

	[ObservableProperty]
	private int _bookId;

	[ObservableProperty]
	private Book _book;

	[ObservableProperty]
	private string _title;

	/// <summary>The page being shown. Stays on the previous page while the next one loads, and is null until the first arrives.</summary>
	[ObservableProperty]
	private ImageSource _pageSource;

	/// <summary>Width over height of <see cref="PageSource"/>, or null if it could not be read. Set just before the source, so the zoom bounds never describe the previous page.</summary>
	[ObservableProperty]
	private double? _pageAspectRatio;

	[ObservableProperty]
	private bool _isLoading;

	[ObservableProperty]
	private string _loadingText;

	[ObservableProperty]
	private string _errorText;

	[ObservableProperty]
	private bool _canRetry;

	/// <summary>The server's page count once the book is prepared, otherwise the stored one. The stored one can be off by one.</summary>
	private int PageCount => _preparedPageCount ?? this.Book?.PageCount ?? 0;

	[RelayCommand]
	private Task Retry() => _ready ? this.ShowPageAsync() : this.OpenBookAsync();

	[RelayCommand]
	private Task NextPage() => this.ChangePageAsync(1);

	[RelayCommand]
	private Task PreviousPage() => this.ChangePageAsync(-1);

	[RelayCommand]
	private async Task MarkReadAsync()
	{
		// A page save landing after mark-read would move CurrentPage back off PageCount.
		this.CancelPendingMark();
		if (await BookReadActions.TryMarkReadAsync(this.BookId, true, _clientFactory.CreateClient()))
		{
			await Shell.Current.GoToAsync("..");
		}
	}

	private async Task ChangePageAsync(int delta) 
	{ 
		if (!_ready)
		{
			// Until the book is prepared there is no page to turn from, and a turn would be saved as reading progress.
			return;
		}

		var newPage = this.PageNumber + delta;
		var pageLimit = this.PageCount - 1;
		switch (newPage)
		{
            case < 0 or _ when newPage > pageLimit:
				await Shell.Current.GoToAsync("..");
                break;
			default:
				this.PageNumber = newPage;
				break;
        };
	}
	
    protected override async void OnPropertyChanged(PropertyChangedEventArgs e)
    {
		base.OnPropertyChanged(e);
        switch (e.PropertyName) {
			case nameof(BookId):
				_ = this.OpenBookAsync();
				break;
			case nameof(Book):
				this.SetTitle();
				break;
			case nameof(PageNumber):
				this.SetTitle();
				if (_ready && !_loadingBook)
				{
					// Show first: the page being turned to may already be prefetching, and the window must see that it is wanted.
					_ = this.ShowPageAsync();
					_prefetcher.MoveTo(this.PageNumber);
				}

				await this.MarkPage();
				break;
			default:
				break;
		}
    }

    private void SetTitle()
    {
		this.Title = $"{this.Book?.Series} #{this.Book?.Number} pg. {this.PageNumber}/{this.PageCount}";
    }

	/// <summary>
	/// Saves the reading position once page turns settle. Rapid turns cancel the pending save so only the last page is sent.
	/// A failed save is logged and dropped; <see cref="Book.CurrentPage"/> is left alone so the next turn tries again.
	/// </summary>
	private async Task MarkPage()
	{
		if (_loadingBook || this.Book is null)
		{
			return;
		}

		var pending = new CancellationTokenSource();
		this.CancelPendingMark(pending);
		var token = pending.Token;
		try
		{
			await Task.Delay(MarkPageDelay, token);
			var book = this.Book;
			var page = this.PageNumber;
			if (book is null || book.CurrentPage == page)
			{
				return;
			}

			await _clientFactory.CreateClient().MarkPageAsync(this.BookId, page, token);
			book.CurrentPage = page;
		}
		catch (OperationCanceledException)
		{
			// Superseded by a newer page turn, or the book was marked read.
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Failed to save page for book {this.BookId}: {ex.Message}");
		}
	}

	private void CancelPendingMark(CancellationTokenSource replacement = null)
	{
		var previous = Interlocked.Exchange(ref _pendingMark, replacement);
		previous?.Cancel();
		previous?.Dispose();
	}

	private static readonly TimeSpan MarkPageDelay = TimeSpan.FromMilliseconds(750);

	private CancellationTokenSource _pendingMark;

	/// <summary>
	/// Loads the book, waits for the server to have it extracted (which can take minutes the first time), then shows the
	/// page to resume at. Also what Retry runs when any of that failed.
	/// </summary>
	private async Task OpenBookAsync()
	{
		var token = this.BeginLoad();
		try
		{
			this.Book ??= await _clientFactory.CreateClient().GetBookAsync(this.BookId, token)
				?? throw new PageLoadException(PageLoadFailure.NotFound, "That book was not found.");

			this.LoadingText = "Preparing book…";
			this.IsLoading = true;
			var progress = new Progress<PrepareProgress>(p => this.LoadingText = PreparingText(p));
			var info = await _pages.PrepareAsync(this.BookId, progress, token);
			_preparedPageCount = info.PageCount;

			// A book marked read has CurrentPage == PageCount, one past the last page, and reopening it must not rewrite that.
			_loadingBook = true;
			try
			{
				this.PageNumber = Math.Clamp(this.Book.CurrentPage, 0, Math.Max(this.PageCount - 1, 0));
			}
			finally { _loadingBook = false; }

			this.SetTitle();
			_ready = true;
			this.LoadingText = "Loading page…";
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
		catch (Exception ex)
		{
			this.Fail(ex);
			return;
		}

		// The visible page asks first, so the window's downloads queue behind it. Only a prepared book gets a window.
		var load = this.LoadPageAsync(token);
		_prefetcher.Open(this.BookId, this.PageCount, this.PageNumber);
		await load;
	}

	/// <summary>The reader left the screen: stop prefetching. The next page turn starts the window again.</summary>
	public void Suspend() => _prefetcher.Suspend();

	private static string PreparingText(PrepareProgress progress) => progress.Stage == PrepareStage.Starting
		? "Preparing book…"
		: $"Preparing book… {progress.Elapsed:m\\:ss}\nThe first time a book is opened it takes a little while.";

	private Task ShowPageAsync() => this.LoadPageAsync(this.BeginLoad());

	private async Task LoadPageAsync(CancellationToken token)
	{
		var page = this.PageNumber;
		var finished = false;
		_ = ShowSpinnerSoonAsync();
		try
		{
			var path = await _pages.GetPageFileAsync(this.BookId, page, token);
			var aspectRatio = await Task.Run(() => ReadAspectRatio(path));
			token.ThrowIfCancellationRequested();
			this.PageAspectRatio = aspectRatio;
			this.PageSource = ImageSource.FromStream(() => File.OpenRead(path));
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
		catch (Exception ex)
		{
			this.Fail(ex);
			return;
		}
		finally { finished = true; }

		this.IsLoading = false;

		// Cached pages arrive at once, so only a slow one gets a spinner.
		async Task ShowSpinnerSoonAsync()
		{
			try
			{
				await Task.Delay(LoadingIndicatorDelay, token);
			}
			catch (OperationCanceledException) { return; }

			if (!finished)
			{
				this.LoadingText = "Loading page…";
				this.IsLoading = true;
			}
		}
	}

	/// <summary>Reads the page's shape from its header so the zoom bounds follow the image, not the letterbox. Null if unreadable.</summary>
	private static double? ReadAspectRatio(string path)
	{
		try
		{
			using var stream = File.OpenRead(path);
			return ImageDimensions.TryReadAspectRatio(stream);
		}
		catch (IOException)
		{
			return null;
		}
	}

	private void Fail(Exception ex)
	{
		Debug.WriteLine($"Failed to open book {this.BookId}: {ex}");
		this.IsLoading = false;
		this.CanRetry = ex is not PageLoadException { CanRetry: false };
		this.ErrorText = ex switch
		{
			PageLoadException { Kind: PageLoadFailure.PreparationFailed } => $"The server could not prepare this book.\n{ex.Message}",
			PageLoadException { Kind: PageLoadFailure.Timeout } => "This is taking too long. Try again in a moment.",
			PageLoadException { Kind: PageLoadFailure.Network } => "Couldn't reach the server.",
			PageLoadException => ex.Message,
			_ => "Something went wrong opening this book.",
		};
	}

	/// <summary>
	/// Starts a new load, cancelling whatever was in flight, and clears the previous error. The token sources are
	/// not disposed: they hold no timers or registrations, so letting them go is free and avoids racing a load that is still unwinding.
	/// </summary>
	private CancellationToken BeginLoad()
	{
		var next = new CancellationTokenSource();
		Interlocked.Exchange(ref _currentLoad, next)?.Cancel();
		this.ErrorText = null;
		return next.Token;
	}

	private static readonly TimeSpan LoadingIndicatorDelay = TimeSpan.FromMilliseconds(300);

	private CancellationTokenSource _currentLoad;
	private int? _preparedPageCount;
	private bool _ready;
	private bool _loadingBook;

	private readonly IShortBoxReaderClientFactory _clientFactory = clientFactory;
	private readonly IPageProvider _pages = pages;
	private readonly PagePrefetcher _prefetcher = prefetcher;

}
