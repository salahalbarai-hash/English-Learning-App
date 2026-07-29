using Newtonsoft.Json.Linq;

namespace English
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            Preferences.Set("MemorizedWords", 100);
        }
        protected override Window CreateWindow(IActivationState? activationState)
        {
            var isLogin = Preferences.Get("IsLogin", "0");

            if (isLogin == "1")
            {
                return new Window(new AppShell());
            }
            else
            {
                SetPreferences();
                return new Window(new LoginPage());
            }
        }

        //private async Task InitializeAdsAsync()
        //{
        //    if (App.areEventsRegistered)
        //        return;
        //    CrossMauiMTAdmob.Current.OnInterstitialLoaded += (EventHandler)((s, e) => InterstitialAd.isAdLoading = false);
        //    CrossMauiMTAdmob.Current.OnInterstitialFailedToLoad += (EventHandler<MTEventArgs>)(async (s, e) =>
        //    {
        //        InterstitialAd.isAdLoading = false;
        //        await InterstitialAd.LoadInterstitialAd();
        //    });
        //    App.areEventsRegistered = true;
        //    await InterstitialAd.LoadInterstitialAd();
        //}

        private void SetPreferences()
        {
            if (!Preferences.ContainsKey("IsLogin"))
                Preferences.Set("IsLogin", "0");
            if (!Preferences.ContainsKey("ApiUrl"))
                Preferences.Set("ApiUrl", "https://en-api.runasp.net/");
            if (!Preferences.ContainsKey("Day"))
                Preferences.Set("Day", "0");
            if (!Preferences.ContainsKey("MemorizedWords"))
                Preferences.Set("MemorizedWords", 0);
        }
        private void SaveJsonFileIfNotExists()
        {
            string path = Path.Combine(FileSystem.AppDataDirectory, "LockFile.json");
            if (File.Exists(path))
                return;
            JObject jobject1 = new JObject();
            jobject1.Add("Group 1.Quiz Options", true);
            jobject1.Add("Group 1.Quiz Writing", true);
            jobject1.Add("Group 1.Quiz Listening", true);
            jobject1.Add("Group 2.Quiz Options", true);
            jobject1.Add("Group 2.Quiz Writing", true);
            jobject1.Add("Group 2.Quiz Listening", true);
            jobject1.Add("Group 3.Quiz Options", true);
            jobject1.Add("Group 3.Quiz Writing", true);
            jobject1.Add("Group 3.Quiz Listening", true);
            jobject1.Add("Group 4.Quiz Options", true);
            jobject1.Add("Group 4.Quiz Writing", true);
            jobject1.Add("Group 4.Quiz Listening", true);
            File.WriteAllText(path, jobject1.ToString());
        }
    }
}
