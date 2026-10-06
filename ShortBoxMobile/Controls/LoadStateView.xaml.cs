using System.Windows.Input;

namespace ShortBoxMobile;

/// <summary>
/// A small card for the loading and error states of content that arrives later: a spinner with a message while
/// <see cref="IsLoading"/>, or an error message with an optional Retry button when <see cref="ErrorText"/> is set.
/// Hidden when neither applies. Error wins over loading.
/// </summary>
public partial class LoadStateView : ContentView
{
    public LoadStateView()
    {
        InitializeComponent();
        this.Refresh();
    }

    public static readonly BindableProperty IsLoadingProperty = Create(nameof(IsLoading), false);
    public static readonly BindableProperty LoadingTextProperty = Create<string>(nameof(LoadingText), null);
    public static readonly BindableProperty ErrorTextProperty = Create<string>(nameof(ErrorText), null);
    public static readonly BindableProperty CanRetryProperty = Create(nameof(CanRetry), true);
    public static readonly BindableProperty RetryCommandProperty = Create<ICommand>(nameof(RetryCommand), null);

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public string LoadingText
    {
        get => (string)GetValue(LoadingTextProperty);
        set => SetValue(LoadingTextProperty, value);
    }

    public string ErrorText
    {
        get => (string)GetValue(ErrorTextProperty);
        set => SetValue(ErrorTextProperty, value);
    }

    /// <summary>Whether the Retry button is offered with an error. False when asking again cannot help.</summary>
    public bool CanRetry
    {
        get => (bool)GetValue(CanRetryProperty);
        set => SetValue(CanRetryProperty, value);
    }

    public ICommand RetryCommand
    {
        get => (ICommand)GetValue(RetryCommandProperty);
        set => SetValue(RetryCommandProperty, value);
    }

    private static BindableProperty Create<T>(string name, T defaultValue) =>
        BindableProperty.Create(name, typeof(T), typeof(LoadStateView), defaultValue,
            propertyChanged: (view, _, _) => ((LoadStateView)view).Refresh());

    private void Refresh()
    {
        var hasError = !string.IsNullOrEmpty(this.ErrorText);
        var isLoading = this.IsLoading && !hasError;
        this.IsVisible = hasError || isLoading;
        this.spinner.IsRunning = isLoading;
        this.spinner.IsVisible = isLoading;
        this.loadingLabel.Text = this.LoadingText;
        this.loadingLabel.IsVisible = isLoading && !string.IsNullOrEmpty(this.LoadingText);
        this.errorLabel.Text = this.ErrorText;
        this.errorLabel.IsVisible = hasError;
        this.retryButton.IsVisible = hasError && this.CanRetry && this.RetryCommand is not null;
    }

    private void OnRetryClicked(object sender, EventArgs e)
    {
        if (this.RetryCommand?.CanExecute(null) == true)
        {
            this.RetryCommand.Execute(null);
        }
    }
}
