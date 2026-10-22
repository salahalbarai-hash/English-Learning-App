using CommunityToolkit.Maui.Views;

namespace English.Popups;

public partial class InAppMessageBannerPopup : Popup
{
    private readonly string _senderName;
    private readonly CancellationTokenSource _cts = new();
    private bool _isDismissed = false;
    private readonly object _lock = new();

    public InAppMessageBannerPopup(string senderName, string messageText)
    {
        InitializeComponent();
        _senderName = senderName;

        SenderNameLabel.Text = senderName;
        SenderInitialLabel.Text = string.IsNullOrEmpty(senderName) ? "?" : senderName[0].ToString().ToUpper();
        MessagePreviewLabel.Text = string.IsNullOrWhiteSpace(messageText) ? "أرسل لك رسالة" : messageText;

        // وضع أولي لتحريك الشريط من أعلى الشاشة بنعومة
        BannerCard.Opacity = 0;
        BannerCard.TranslationY = -40;

        Opened += OnPopupOpened;

#if ANDROID
        Opened += ConfigureAndroidGravity;
#endif
    }

#if ANDROID
    private void ConfigureAndroidGravity(object? sender, EventArgs e)
    {
        try
        {
            if (Handler?.PlatformView is Android.App.Dialog dialog && dialog.Window != null)
            {
                dialog.Window.SetGravity(Android.Views.GravityFlags.Top | Android.Views.GravityFlags.CenterHorizontal);
                var lp = dialog.Window.Attributes;
                if (lp != null)
                {
                    lp.Y = 40; // مسافة مناسبة من شريط الحالة
                    dialog.Window.Attributes = lp;
                }
                // إزالة تعتيم الشاشة الخلفية ليبقى التطبيق واضحاً ومرئياً بالكامل
                dialog.Window.ClearFlags(Android.Views.WindowManagerFlags.DimBehind);
            }
        }
        catch { }
    }
#endif

    private async void OnPopupOpened(object? sender, EventArgs e)
    {
        // تشغيل حركة النزول والظهور السلس للشريط
        try
        {
            await Task.WhenAll(
                BannerCard.FadeTo(1, 200, Easing.CubicOut),
                BannerCard.TranslateTo(0, 0, 250, Easing.CubicOut)
            );
        }
        catch { }

        // إخفاء الشريط تلقائياً بعد 3.5 ثوانٍ إذا لم يتفاعل معه المستخدم
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(3500, _cts.Token);
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await AnimateAndCloseAsync();
                });
            }
            catch (TaskCanceledException) { }
            catch { }
        });
    }

    private async void OnBannerTapped(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            if (_isDismissed) return;
            _isDismissed = true;
        }

        try { _cts.Cancel(); } catch { }

        try
        {
            Close();
            // الانتقال الفوري لصفحة المحادثة مع هذا الصديق
            await Shell.Current.GoToAsync($"ChatPage?FriendName={_senderName}");
        }
        catch { }
    }

    private async void OnSwipedUp(object? sender, SwipedEventArgs e)
    {
        await AnimateAndCloseAsync();
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await AnimateAndCloseAsync();
    }

    public void DismissBanner()
    {
        lock (_lock)
        {
            if (_isDismissed) return;
            _isDismissed = true;
        }

        try { _cts.Cancel(); } catch { }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try { Close(); } catch { }
        });
    }

    private async Task AnimateAndCloseAsync()
    {
        lock (_lock)
        {
            if (_isDismissed) return;
            _isDismissed = true;
        }

        try { _cts.Cancel(); } catch { }

        try
        {
            await Task.WhenAll(
                BannerCard.FadeTo(0, 180, Easing.CubicIn),
                BannerCard.TranslateTo(0, -40, 200, Easing.CubicIn)
            );
        }
        catch { }

        try
        {
            Close();
        }
        catch { }
    }
}
