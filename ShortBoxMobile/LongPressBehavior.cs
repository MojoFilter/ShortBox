using System.Windows.Input;

namespace ShortBoxMobile;

/// <summary>
/// Runs <see cref="Command"/> on long-press (Android, touch) or right-click / touch-hold (Windows).
/// MAUI has no cross-platform long-press gesture, so this hooks the native view.
/// </summary>
public sealed class LongPressBehavior : Behavior<View>
{
    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(LongPressBehavior));

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(LongPressBehavior));

    public ICommand? Command
    {
        get => (ICommand?)this.GetValue(CommandProperty);
        set => this.SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => this.GetValue(CommandParameterProperty);
        set => this.SetValue(CommandParameterProperty, value);
    }

    protected override void OnAttachedTo(View view)
    {
        base.OnAttachedTo(view);
        _view = view;
        this.BindingContext = view.BindingContext;
        view.BindingContextChanged += this.OnViewBindingContextChanged;
        view.HandlerChanging += this.OnHandlerChanging;
        view.HandlerChanged += this.OnHandlerChanged;
        this.Hook(view.Handler?.PlatformView);
    }

    protected override void OnDetachingFrom(View view)
    {
        view.BindingContextChanged -= this.OnViewBindingContextChanged;
        view.HandlerChanging -= this.OnHandlerChanging;
        view.HandlerChanged -= this.OnHandlerChanged;
        this.Unhook(view.Handler?.PlatformView);
        this.BindingContext = null;
        _view = null;
        base.OnDetachingFrom(view);
    }

    private void OnViewBindingContextChanged(object? sender, EventArgs e) =>
        this.BindingContext = _view?.BindingContext;

    private void OnHandlerChanging(object? sender, HandlerChangingEventArgs e) =>
        this.Unhook(e.OldHandler?.PlatformView);

    private void OnHandlerChanged(object? sender, EventArgs e) =>
        this.Hook(_view?.Handler?.PlatformView);

    private void Execute()
    {
        var parameter = this.CommandParameter;
        if (this.Command is { } command && command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }

    private void Hook(object? platformView)
    {
#if ANDROID
        if (platformView is Android.Views.View native)
        {
            native.LongClick += this.OnNativeLongClick;
        }
#elif WINDOWS
        if (platformView is Microsoft.UI.Xaml.UIElement native)
        {
            native.RightTapped += this.OnNativeRightTapped;
        }
#endif
    }

    private void Unhook(object? platformView)
    {
#if ANDROID
        if (platformView is Android.Views.View native)
        {
            native.LongClick -= this.OnNativeLongClick;
        }
#elif WINDOWS
        if (platformView is Microsoft.UI.Xaml.UIElement native)
        {
            native.RightTapped -= this.OnNativeRightTapped;
        }
#endif
    }

#if ANDROID
    private void OnNativeLongClick(object? sender, Android.Views.View.LongClickEventArgs e)
    {
        this.Execute();
        e.Handled = true;
    }
#elif WINDOWS
    private void OnNativeRightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        this.Execute();
        e.Handled = true;
    }
#endif

    private View? _view;
}
