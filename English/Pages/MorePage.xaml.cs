using Microsoft.Maui.ApplicationModel.DataTransfer;
using English.Helpers;
using English.Services;

namespace English.Pages;

public partial class MorePage : ContentPage
{
    private bool _isNavigating = false;
    private DateTime _lastAppearedTime = DateTime.MinValue;

    public MorePage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        _lastAppearedTime = DateTime.UtcNow;
        _isNavigating = false;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _lastAppearedTime = DateTime.UtcNow;
        _isNavigating = false;
        this.AnimatePageInAsync();

        // تحديث شارة الرسائل غير المقروءة
        RefreshUnreadBadge();

        // الاشتراك في حدث وصول رسالة جديدة لتحديث الشارة فوراً
        ChatStorageService.OnGlobalMessageReceived += OnNewMessageReceived;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        ChatStorageService.OnGlobalMessageReceived -= OnNewMessageReceived;
    }

    private void OnNewMessageReceived(string sender, int messageId, string content)
    {
        MainThread.BeginInvokeOnMainThread(RefreshUnreadBadge);
    }

    private void RefreshUnreadBadge()
    {
        try
        {
            // جمع كل الرسائل غير المقروءة من جميع المحادثات
            int totalUnread = ChatStorageService.GetTotalUnreadCount();

            if (totalUnread > 0)
            {
                UnreadCountLabel.Text = totalUnread > 99 ? "99+" : totalUnread.ToString();
                UnreadBadge.IsVisible = true;
                UnreadDot.IsVisible = false;

                // تأثير نبض خفيف لجذب الانتباه
                AnimateBadge();
            }
            else
            {
                UnreadBadge.IsVisible = false;
                UnreadDot.IsVisible = false;
            }
        }
        catch { }
    }

    private async void AnimateBadge()
    {
        try
        {
            await UnreadBadge.ScaleTo(1.15, 120, Easing.CubicOut);
            await UnreadBadge.ScaleTo(1.0, 100, Easing.CubicIn);
        }
        catch { }
    }

    private async void MenuClicked(object sender, TappedEventArgs e)
    {
        // حماية تامة من اللمسات العابرة (Tap-through) والنقر المزدوج عند العودة السريعة للشاشة
        if (_isNavigating || (DateTime.UtcNow - _lastAppearedTime).TotalMilliseconds < 450) return;
        _isNavigating = true;

        try
        {
            if (sender is not Border card)
                return;

            // تأثير الضغط
            await card.ScaleTo(0.97, 60);
            await card.ScaleTo(1, 60);

            switch (card.ClassId)
            {
                case "Profile":
                    await Shell.Current.GoToAsync(nameof(SettingsPage));
                    break;
                case "Friends":
                    await Shell.Current.GoToAsync(nameof(FriendsPage));
                    break;
                case "Messages":
                    await Shell.Current.GoToAsync(nameof(MessagesPage));
                    break;
                case "PrivacyPolicy":
                    await Shell.Current.GoToAsync(nameof(PrivacyPolicyPage));
                    break;
                case "Share":
                    await Share.Default.RequestAsync(new ShareTextRequest
                    {
                        Title = "مشاركة التطبيق",
                        Text = "حمل تطبيق إنجليش الآن وانطلق في رحلة ممتعة لتعلم اللغة الإنجليزية وتنافس مع أصدقائك! 🚀📚\n\nhttps://english.runasp.net/"
                    });
                    break;
                case "About":
                    await Shell.Current.GoToAsync(nameof(AboutPage));
                    break;
                case "Logout":
                    bool result = await DisplayAlert("تسجيل الخروج", "هل تريد تسجيل الخروج من الحساب؟", "نعم", "إلغاء");
                    if (result)
                    {
                        Preferences.Set("IsLogin", "0");
                        if (Application.Current?.Windows.Count > 0 && Application.Current.Windows[0].Page is AppShell currentShell)
                        {
                            try
                            {
                                await currentShell.GameHub.DisconnectAsync();
                            }
                            catch { }
                        }
                        await Shell.Current.GoToAsync("//LoginPage");
                    }
                    break;
            }
        }
        finally
        {
            _isNavigating = false;
        }
    }
}