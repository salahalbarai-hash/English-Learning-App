using CommunityToolkit.Maui.Core;
using English.Helpers;

namespace English.Pages;

public partial class CreateAccountPage : ContentPage
{
    private bool _isBusy;

    public CreateAccountPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.AnimatePageInAsync();
    }

    private async void OnCreateAccountClicked(object sender, EventArgs e)
    {
        // منع النقر المتكرر
        if (_isBusy) return;

        string userName = $"{UserNameEntry.Text}".Trim();
        string password = $"{PasswordEntry.Text}".Trim();
        string phone = $"{PhoneEntry.Text}".Trim();

        // التحقق من المدخلات
        if (!ValidateInputs(userName, password))
            return;

        try
        {
            SetBusy(true);

            // التحقق من جودة الاتصال بالإنترنت
            if (!await Service.HasActiveInternetAsync(5))
            {
                await ShowToast("يرجى الاتصال بالإنترنت 🌐");
                return;
            }

            var user = new User
            {
                UserName = userName,
                Password = password,
                PhoneNumber = phone
            };

            // إرسال البيانات للسيرفر
            string id = await Service.AddUser(user);

            if (string.IsNullOrWhiteSpace(id))
            {
                await ShowToast("فشل إنشاء الحساب، ربما الاسم مستخدم مسبقاً 😢");
                return;
            }

            await ShowToast("تم إنشاء حسابك بنجاح! مرحباً بك 🎉");

            Application.Current!.Windows[0].Page = new LoginPage();
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ", $"حدث خطأ غير متوقع: {ex.Message}", "موافق");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private bool ValidateInputs(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            _ = ShowToast("يرجى اختيار اسم مستخدم مناسب");
            return false;
        }

        if (password.Length < 4) // أضفت شرطاً بسيطاً لقوة كلمة المرور
        {
            _ = ShowToast("يجب أن تكون كلمة المرور 4 أحرف على الأقل");
            return false;
        }

        return true;
    }

    private async Task ShowToast(string message)
    {
        await Toast.Make(message, ToastDuration.Short).Show();
    }

    private void SetBusy(bool value)
    {
        _isBusy = value;
        // الربط مع شاشة التحميل الزجاجية في XAML
        LoadingOverlay.IsVisible = value;
        ActivityIndicator.IsRunning = value;
    }

    // دالة العودة عند الضغط على "لديك حساب بالفعل؟"
    private async void OnBackToLoginClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}