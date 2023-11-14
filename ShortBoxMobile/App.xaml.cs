namespace ShortBoxMobile
{
    public partial class App : Application
    {
        public App(AppShell mainPage, ViewModelFactory viewModelFactory)
        {
            InitializeComponent();
            MainPage = mainPage;
            this.ViewModelFactory = viewModelFactory;
        }

        internal ViewModelFactory ViewModelFactory { get; }

        public static App CurrentApp => Current as App;
    }
}
