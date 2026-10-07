namespace ShortBoxMobile;

/// <summary>How the reader behaves, kept across launches in the app's preferences.</summary>
public sealed class ReaderSettings : ObservableObject
{
    private const string AnimatePageTurnsKey = "reader.animatePageTurns";

    /// <summary>
    /// Whether the pager slides when a tap, button or key turns the page. A swipe always follows the finger, so this does not
    /// affect it. Turn it off to jump straight to the next page.
    /// </summary>
    public bool AnimatePageTurns
    {
        get => Preferences.Default.Get(AnimatePageTurnsKey, true);
        set
        {
            if (value != this.AnimatePageTurns)
            {
                Preferences.Default.Set(AnimatePageTurnsKey, value);
                this.OnPropertyChanged();
            }
        }
    }
}
