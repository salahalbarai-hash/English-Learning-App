namespace English.Pages;

public partial class AboutPage : ContentPage
{
    public AboutPage()
    {
        InitializeComponent();
    }

    // دالة زر تقييم التطبيق
    private async void OnRateAppClicked(object sender, EventArgs e)
    {
        // يمكنك لاحقاً استبدال هذا الرابط برابط تطبيقك الفعلي في متجر جوجل أو آبل
        string storeLink = DeviceInfo.Platform == DevicePlatform.iOS
            ? "https://apps.apple.com"
            : "https://play.google.com/store";

        await Launcher.OpenAsync(storeLink);
    }

    // دالة زر التواصل معنا (تفتح البريد الإلكتروني)
    // دالة زر التواصل معنا (تفتح الواتساب)
    private async void OnContactUsClicked(object sender, EventArgs e)
    {
        try
        {
            string phoneNumber = "967713818034";

            // 2. الرسالة الافتراضية التي ستظهر في المحادثة
            string message = "مرحباً فريق دعم تطبيق إنجليش، لدي استفسار بخصوص التطبيق:";

            // 3. تحويل النص ليكون متوافقاً مع الروابط (تشفير المسافات والرموز)
            string urlEncodedMessage = Uri.EscapeDataString(message);

            // 4. الرابط الرسمي لفتح الواتساب
            string whatsappUrl = $"https://wa.me/{phoneNumber}?text={urlEncodedMessage}";

            // 5. محاولة فتح الرابط
            bool isOpened = await Launcher.OpenAsync(whatsappUrl);

            if (!isOpened)
            {
                await DisplayAlert("تنبيه", "تعذر فتح الواتساب. تأكد من تثبيت التطبيق على جهازك.", "حسناً");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WhatsApp Open Error: {ex.Message}");
            await DisplayAlert("خطأ", "حدث خطأ غير متوقع أثناء محاولة فتح الواتساب.", "حسناً");
        }
    }
}