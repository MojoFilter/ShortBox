namespace ShortBoxMobile;

public partial class SettingsPage : ContentPage
{
	public SettingsPage(ReaderSettings settings)
	{
		InitializeComponent();
		this.BindingContext = settings;
	}
}
