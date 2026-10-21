using English.Helpers;

namespace English.Pages;

public partial class LoginPage : ContentPage
{
    private bool _isBusy;

    public LoginPage()
    {
        InitializeComponent();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        if (_isBusy) return;

        string username = $"{UserNameEntry.Text}".Trim();
        string password = $"{PasswordEntry.Text}".Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("تنبيه", "يرجى إدخال اسم المستخدم وكلمة المرور", "موافق");
            return;
        }

        try
        {
            SetBusy(true);

            if (!await Service.HasActiveInternetAsync(5))
            {
                await Toast.Make("يرجى الاتصال بالإنترنت").Show();
                return;
            }

            // التحقق من المستخدم من السيرفر
            var result = await Service.GetUser(new User
            {
                UserName = username,
                Password = password,
            });

            if (!result.Success)
            {
                await Toast.Make(result.Message ?? "حدث خطأ").Show();
                return;
            }
            User user = result.Data!;
            await SaveUserPreferencesAsync(user);
            await LoginSuccess(user.UserName!);
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ", ex.Message, "موافق");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task SaveUserPreferencesAsync(User user)
    {
        Preferences.Set("ID", user.ID.ToString());
        Preferences.Set("UserName", user.UserName);
        await SecureStorage.SetAsync("Password", user.Password ?? "");
        Preferences.Set("PhoneNumber", user.PhoneNumber);
        Preferences.Set("YER", user.YER);
        Preferences.Set("TimeFinalExam", user.TimeFinalExam);
        Preferences.Set("Day", "0");
        Preferences.Set("ImagesDownloaded", false);
        Preferences.Set("MemorizedWords", user.MemorizedWords);
        Preferences.Set("Coins", user.Coins);
        Preferences.Set("FriendsChallengeCount", user.FriendsChallengeCount);
    }

    private async Task LoginSuccess(string username)
    {
        Preferences.Set("IsLogin", "1");
        await Toast.Make("تم تسجيل الدخول بنجاح 😊").Show();

        // 🟢 جلب الأصدقاء وحفظهم في الكاش المحلي لضمان وجودهم للأجهزة الجديدة
        try
        {
            var friends = await Service.GetFriendsAsync(username);
            if (friends != null && friends.Length > 0)
            {
                Preferences.Set("Cached_Friends_List", JsonSerializer.Serialize(friends.ToList()));
            }
        }
        catch { }

        if (Application.Current?.Windows.Count > 0)
        {
            // فصل الاتصال القديم لمنع تكرار الأحداث وتسريب الذاكرة (Memory Leak)
            if (Application.Current.Windows[0].Page is AppShell oldShell)
            {
                try
                {
                    await oldShell.GameHub.DisconnectAsync();
                }
                catch { }
            }

            // 1. إنشاء و تعيين AppShell كصفحة رئيسية جديدة
            var appShell = new AppShell();
            Application.Current.Windows[0].Page = appShell;

            // 2. بدء اتصال SignalR وربط الأحداث باسم المستخدم الحالي
            await appShell.StartGameHubAsync(username);
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.AnimatePageInAsync();
    }

    private void SetBusy(bool value)
    {
        _isBusy = value;
        LoadingOverlay.IsVisible = value;
    }

    private async void OnRegisterTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(CreateAccountPage));
    }

    private async void OnPrivacyPolicyTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(PrivacyPolicyPage));
    }
}