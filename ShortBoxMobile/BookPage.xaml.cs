using MauiPageFullScreen;
using ShortBox.Azure;
using System.Diagnostics;

namespace ShortBoxMobile;

public partial class BookPage : ContentPage
{
	private bool _resyncing;

	public BookPage(BookPageViewModel vm)
	{
		InitializeComponent();
		this.BindingContext = vm;
		vm.PropertyChanged += this.OnViewModelPropertyChanged;
	}

	/// <summary>
	/// The pager follows the view model: when the pages first arrive the carousel is told where to open, because a position set
	/// before it had any items is not reliably honoured.
	/// </summary>
	private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(BookPageViewModel.Pages))
		{
			this.Dispatcher.Dispatch(this.SyncPagerToViewModel);
		}
	}

	/// <summary>
	/// A new position from the pager is a swipe (or the echo of a turn the view model asked for). The view model takes it only if
	/// it is one page from where it is; anything else is the carousel settling at the wrong place, and it is sent back.
	/// </summary>
	private void OnPagerPositionChanged(object sender, PositionChangedEventArgs e)
	{
		if (_resyncing || this.ViewModel is not { } vm)
		{
			return;
		}

		if (!vm.TryAcceptPagerPosition(e.CurrentPosition))
		{
			this.SyncPagerToViewModel();
		}
	}

	private void SyncPagerToViewModel()
	{
		if (this.ViewModel is not { Pages.Count: > 0 } vm || this.pager.Position == vm.PageNumber)
		{
			return;
		}

		_resyncing = true;
		try
		{
			this.pager.Position = vm.PageNumber;
		}
		finally
		{
			_resyncing = false;
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
		var command = GetTapArea(e.Position, (View)sender) switch
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
public sealed partial class BookPageViewModel(IShortBoxReaderClientFactory clientFactory, IPageProvider provider, PagePrefetcher prefetcher, ReaderSettings settings) : ObservableObject
{
    [ObservableProperty]
	private int _pageNumber;

	[ObservableProperty]
	private int _bookId;

	[ObservableProperty]
	private Book _book;

	[ObservableProperty]
	private string _title;

	/// <summary>Every page of the book, once it is prepared; null until then. Each decides for itself whether to hold an image.</summary>
	[ObservableProperty]
	private IReadOnlyList<ReaderPage> _pages;

	/// <summary>The book is prepared and the pager has pages to show.</summary>
	[ObservableProperty]
	private bool _isReady;

	/// <summary>Loading and error state of opening the book. Each page has its own.</summary>
	[ObservableProperty]
	private bool _isLoading;

	[ObservableProperty]
	private string _loadingText;

	[ObservableProperty]
	private string _errorText;

	[ObservableProperty]
	private bool _canRetry;

	/// <summary>Whether the pager slides when a tap, button or key turns the page. Read when the reader opens.</summary>
	public bool AnimatePageTurns => settings.AnimatePageTurns;

	/// <summary>The server's page count once the book is prepared, otherwise the stored one. The stored one can be off by one.</summary>
	private int PageCount => _preparedPageCount ?? this.Book?.PageCount ?? 0;

	[RelayCommand]
	private Task Retry() => this.OpenBookAsync();

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

	/// <summary>
	/// The pager scrolled to <paramref name="position"/>. Returns false if that cannot be right, in which case the pager is out
	/// of step and should be put back. A swipe moves one page, and a carousel that is still settling can report any position
	/// (it starts at 0 before it has been told where to open), which must never be saved as reading progress.
	/// </summary>
	public bool TryAcceptPagerPosition(int position)
	{
		if (!this.IsReady || position == this.PageNumber)
		{
			return true;
		}

		if (Math.Abs(position - this.PageNumber) != 1 || position < 0 || position >= this.PageCount)
		{
			return false;
		}

		this.PageNumber = position;
		return true;
	}

	private async Task ChangePageAsync(int delta)
	{
		if (!this.IsReady)
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
				if (this.IsReady && !_loadingBook)
				{
					// Pages first: the one being turned to may already be prefetching, and the window must see that it is wanted.
					this.UpdatePages();
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
	/// Tells each page how near the reader is: the one on screen loads first, its neighbours load behind it, and pages that fell
	/// out of reach let go of their images. Only the pages that were or are in reach are visited, however long the book.
	/// </summary>
	private void UpdatePages()
	{
		if (this.Pages is not { Count: > 0 } pages)
		{
			return;
		}

		var current = Math.Clamp(this.PageNumber, 0, pages.Count - 1);
		var live = PagerWindow.Live(current, pages.Count);
		foreach (var index in live)
		{
			pages[index].SetRole(PagerWindow.RoleOf(index, current));
		}

		foreach (var index in _livePages.Except(live).ToArray())
		{
			pages[index].SetRole(PageRole.Far);
		}

		_livePages = [.. live];
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
	/// Loads the book, waits for the server to have it extracted (which can take minutes the first time), then gives the pager
	/// its pages, opened at the page to resume at. Also what Retry runs when any of that failed.
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
			var info = await _provider.PrepareAsync(this.BookId, progress, token);
			_preparedPageCount = info.PageCount;

			// A book marked read has CurrentPage == PageCount, one past the last page, and reopening it must not rewrite that.
			_loadingBook = true;
			try
			{
				this.PageNumber = Math.Clamp(this.Book.CurrentPage, 0, Math.Max(this.PageCount - 1, 0));
			}
			finally { _loadingBook = false; }

			this.SetTitle();
			_livePages = [];
			this.Pages = [.. Enumerable.Range(0, info.PageCount).Select(i => new ReaderPage(_provider, this.BookId, i))];
			this.IsReady = true;
			this.IsLoading = false;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
		catch (Exception ex)
		{
			this.Fail(ex);
			return;
		}

		// The visible page asks first, so the window's downloads queue behind it. Only a prepared book gets a window.
		this.UpdatePages();
		_prefetcher.Open(this.BookId, this.PageCount, this.PageNumber);
	}

	/// <summary>The reader left the screen: stop prefetching. The next page turn starts the window again.</summary>
	public void Suspend() => _prefetcher.Suspend();

	private static string PreparingText(PrepareProgress progress) => progress.Stage == PrepareStage.Starting
		? "Preparing book…"
		: $"Preparing book… {progress.Elapsed:m\\:ss}\nThe first time a book is opened it takes a little while.";

	private void Fail(Exception ex)
	{
		Debug.WriteLine($"Failed to open book {this.BookId}: {ex}");
		this.IsLoading = false;
		this.ErrorText = LoadErrors.Describe(ex, out var canRetry);
		this.CanRetry = canRetry;
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

	private CancellationTokenSource _currentLoad;
	private int? _preparedPageCount;
	private bool _loadingBook;
	private HashSet<int> _livePages = [];

	private readonly IShortBoxReaderClientFactory _clientFactory = clientFactory;
	private readonly IPageProvider _provider = provider;
	private readonly PagePrefetcher _prefetcher = prefetcher;

}
