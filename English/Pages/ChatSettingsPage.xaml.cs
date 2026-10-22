using English.Helpers;

namespace English.Pages;

public partial class ChatSettingsPage : ContentPage
{
    // مفتاح حفظ إعداد الإشعارات
    public const string NotificationsEnabledKey = "ChatNotificationsEnabled";
    // مفتاح حفظ إعداد معاينة الرسالة
    public const string PreviewEnabledKey = "ChatPreviewEnabled";

    public ChatSettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // تحميل الإعدادات المحفوظة
        bool notifEnabled = Preferences.Get(NotificationsEnabledKey, true);
        bool previewEnabled = Preferences.Get(PreviewEnabledKey, true);

        NotificationSwitch.IsToggled = notifEnabled;
        PreviewSwitch.IsToggled = previewEnabled;

        UpdateNotificationLabel(notifEnabled);
        UpdatePreviewLabel(previewEnabled);

        this.AnimatePageInAsync();
    }

    private void OnNotificationSwitchToggled(object sender, ToggledEventArgs e)
    {
        Preferences.Set(NotificationsEnabledKey, e.Value);
        UpdateNotificationLabel(e.Value);
    }

    private void OnPreviewSwitchToggled(object sender, ToggledEventArgs e)
    {
        Preferences.Set(PreviewEnabledKey, e.Value);
        UpdatePreviewLabel(e.Value);
    }

    private void UpdateNotificationLabel(bool enabled)
    {
        NotificationStatusLabel.Text = enabled
            ? "تلقّي إشعار عند وصول رسالة من صديق"
            : "الإشعارات مُعطَّلة";
        NotificationStatusLabel.TextColor = enabled
            ? Color.FromArgb("#64748B")
            : Color.FromArgb("#EF4444");
    }

    private void UpdatePreviewLabel(bool enabled)
    {
        PreviewStatusLabel.Text = enabled
            ? "إظهار نص الرسالة في شريط الإشعار"
            : "إخفاء محتوى الرسالة في الإشعار";
        PreviewStatusLabel.TextColor = enabled
            ? Color.FromArgb("#64748B")
            : Color.FromArgb("#F59E0B");
    }

    private async void OnBackClicked(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
