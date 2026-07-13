using CommunityToolkit.Maui.Alerts;
using English.Models;
using English.Services;

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

            // التحقق من تسجيل سابق محلي
            if (IsLocalUserValid(username, password))
            {
                await LoginSuccess();
                return;
            }

            if (!await Service.HasActiveInternetAsync(5))
            {
                await Toast.Make("يرجى الاتصال بالإنترنت").Show();
                return;
            }

            // التحقق من المستخدم من السيرفر
            var user = await Service.GetUser(new User
            {
                UserName = username,
                Password = password,
            });

            if (user == null)
            {
                await Toast.Make("بيانات الدخول غير صحيحة").Show();
                return;
            }

            SaveUserPreferences(user);
            await LoginSuccess();
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

    private bool IsLocalUserValid(string username, string password)
    {
        if (!Preferences.ContainsKey("UserName")) return false;

        return Preferences.Get("UserName", "") == username &&
               Preferences.Get("Password", "") == password;
    }

    private void SaveUserPreferences(User user)
    {
        Preferences.Set("ID", user.ID.ToString());
        Preferences.Set("UserName", user.UserName);
        Preferences.Set("Password", user.Password);
        Preferences.Set("PhoneNumber", user.PhoneNumber);
        Preferences.Set("YER", user.YER);
        Preferences.Set("TimeFinalExam", user.TimeFinalExam);
        Preferences.Set("IsLogin", "1");
        Preferences.Set("Day", "0");
        Preferences.Set("ImagesDownloaded", false);
        Preferences.Set("MemorizedWords", user.MemorizedWords);
    }

    private async Task LoginSuccess()
    {
        if (Application.Current?.Windows.Count > 0)
            Application.Current.Windows[0].Page = new AppShell();
        await Toast.Make("تم تسجيل الدخول بنجاح 😊").Show();
    }

    private void SetBusy(bool value)
    {
        _isBusy = value;
        // التعديل هنا: إظهار أو إخفاء الشاشة الزجاجية (Overlay) بالكامل
        LoadingOverlay.IsVisible = value;
    }

    private async void OnRegisterTapped(object sender, EventArgs e)
    {
        if (Preferences.ContainsKey("UserName"))
        {
            var user = Preferences.Get("UserName", "");
            await DisplayAlert("", $"أنت مسجل بالفعل باسم: {user}", "موافق");
            return;
        }

        await Navigation.PushModalAsync(new CreateAccountPage());
    }
}