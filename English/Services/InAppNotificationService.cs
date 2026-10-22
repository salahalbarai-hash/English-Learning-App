using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using English.Pages;

namespace English.Services;

public static class InAppNotificationService
{
    private static FrameLayout? _overlayContainer;
    private static LinearLayout? _bannerCard;
    private static TextView? _senderInitialView;
    private static TextView? _senderNameView;
    private static TextView? _messagePreviewView;
    private static CancellationTokenSource? _dismissCts;
    private static readonly object _lock = new();
    private static string _currentSender = "";

    public static void ShowMessageBanner(string senderName, string messageText)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                // ── التحقق من إعداد المستخدم: هل الإشعارات مفعّلة؟ ──
                bool notifEnabled = Preferences.Get(Pages.ChatSettingsPage.NotificationsEnabledKey, true);
                if (!notifEnabled) return;

                // إذا كان المستخدم حالياً داخل شاشة المحادثة مع هذا الصديق، لا داعي لإظهار الشريط
                if (ChatPage.CurrentActiveFriend != null &&
                    ChatPage.CurrentActiveFriend.Equals(senderName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var activity = Platform.CurrentActivity;
                if (activity == null || activity.IsFinishing || activity.IsDestroyed)
                    return;

                var decorView = activity.Window?.DecorView as ViewGroup;
                if (decorView == null)
                    return;

                float density = activity.Resources?.DisplayMetrics?.Density ?? 2.5f;
                int Dp(float dp) => (int)(dp * density);

                _currentSender = senderName;

                // اهتزاز لمسي خفيف عند وصول الرسالة
                try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

                // إذا كان الشريط معروضاً بالفعل، نقوم فقط بتحديث محتواه وتجديد مؤقت الإخفاء
                if (_overlayContainer != null && _bannerCard != null && _overlayContainer.Parent != null)
                {
                    UpdateBannerContent(senderName, messageText);
                    PulseBanner();
                    ResetDismissTimer();
                    return;
                }

                // إلغاء أي مؤقت سابق
                CancelDismissTimer();

                int statusBarHeight = GetStatusBarHeight(activity);

                // 1. الحاوية الشفافة العلوية (لا تعترض لمسات الشاشة خارج البطاقة)
                _overlayContainer = new FrameLayout(activity)
                {
                    LayoutParameters = new FrameLayout.LayoutParams(
                        ViewGroup.LayoutParams.MatchParent,
                        ViewGroup.LayoutParams.WrapContent)
                    {
                        Gravity = GravityFlags.Top | GravityFlags.CenterHorizontal
                    },
                    Clickable = false,
                    Focusable = false
                };
                _overlayContainer.SetClipChildren(false);
                _overlayContainer.SetClipToPadding(false);

                // 2. بطاقة التنبيه الأنيقة
                _bannerCard = new LinearLayout(activity)
                {
                    Orientation = Orientation.Horizontal,
                    LayoutDirection = Android.Views.LayoutDirection.Rtl, // دعم اللغة العربية RTL
                    Clickable = true,
                    Focusable = true
                };

                var cardParams = new FrameLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent,
                    ViewGroup.LayoutParams.WrapContent)
                {
                    Gravity = GravityFlags.Top | GravityFlags.CenterHorizontal,
                    TopMargin = statusBarHeight + Dp(8),
                    LeftMargin = Dp(14),
                    RightMargin = Dp(14)
                };
                _bannerCard.LayoutParameters = cardParams;
                _bannerCard.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));

                // خلفية دارك نيون مع إطار سماوي وظل
                var cardBg = new GradientDrawable();
                cardBg.SetColor(Android.Graphics.Color.ParseColor("#151E2F"));
                cardBg.SetCornerRadius(Dp(18));
                cardBg.SetStroke(Dp(1.5f), Android.Graphics.Color.ParseColor("#38BDF8"));
                _bannerCard.Background = cardBg;
                _bannerCard.Elevation = Dp(14);
                _bannerCard.TranslationZ = Dp(14);

                // محاولة تحميل خط Cairo-Bold
                Typeface? cairoBold = null;
                try
                {
                    cairoBold = Typeface.CreateFromAsset(activity.Assets, "Cairo-Bold.ttf");
                }
                catch { }
                cairoBold ??= Typeface.DefaultBold;

                // 3. دائرة الصورة الرمزية (الحرف الأول للمرسل)
                var avatarLayout = new FrameLayout(activity)
                {
                    LayoutParameters = new LinearLayout.LayoutParams(Dp(40), Dp(40))
                    {
                        Gravity = GravityFlags.CenterVertical
                    }
                };
                var avatarBg = new GradientDrawable();
                avatarBg.SetColor(Android.Graphics.Color.ParseColor("#1E293B"));
                avatarBg.SetCornerRadius(Dp(20));
                avatarBg.SetStroke(Dp(1.5f), Android.Graphics.Color.ParseColor("#38BDF8"));
                avatarLayout.Background = avatarBg;

                _senderInitialView = new TextView(activity)
                {
                    Text = string.IsNullOrEmpty(senderName) ? "?" : senderName[0].ToString().ToUpper(),
                    TextSize = 16,
                    Typeface = cairoBold,
                    Gravity = GravityFlags.Center,
                    LayoutParameters = new FrameLayout.LayoutParams(
                        ViewGroup.LayoutParams.MatchParent,
                        ViewGroup.LayoutParams.MatchParent)
                };
                _senderInitialView.SetTextColor(Android.Graphics.Color.ParseColor("#38BDF8"));
                avatarLayout.AddView(_senderInitialView);
                _bannerCard.AddView(avatarLayout);

                // 4. نصوص التنبيه (اسم المرسل + ملخص الرسالة)
                var textLayout = new LinearLayout(activity)
                {
                    Orientation = Orientation.Vertical,
                    LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
                    {
                        Gravity = GravityFlags.CenterVertical,
                        LeftMargin = Dp(10),
                        RightMargin = Dp(10)
                    }
                };

                var headerRow = new LinearLayout(activity)
                {
                    Orientation = Orientation.Horizontal,
                    LayoutParameters = new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MatchParent,
                        ViewGroup.LayoutParams.WrapContent)
                };

                _senderNameView = new TextView(activity)
                {
                    Text = senderName,
                    TextSize = 14,
                    Typeface = cairoBold,
                    Ellipsize = Android.Text.TextUtils.TruncateAt.End
                };
                _senderNameView.SetSingleLine(true);
                _senderNameView.SetTextColor(Android.Graphics.Color.White);
                headerRow.AddView(_senderNameView);

                var badgeView = new TextView(activity)
                {
                    Text = " • رسالة جديدة",
                    TextSize = 11,
                    Gravity = GravityFlags.CenterVertical
                };
                badgeView.SetTextColor(Android.Graphics.Color.ParseColor("#38BDF8"));
                headerRow.AddView(badgeView);

                textLayout.AddView(headerRow);

                _messagePreviewView = new TextView(activity)
                {
                    Text = Preferences.Get(Pages.ChatSettingsPage.PreviewEnabledKey, true)
                            ? (string.IsNullOrWhiteSpace(messageText) ? "أرسل لك رسالة" : messageText)
                            : "اضغط لعرض الرسالة",
                    TextSize = 12,
                    Ellipsize = Android.Text.TextUtils.TruncateAt.End
                };
                _messagePreviewView.SetSingleLine(true);
                _messagePreviewView.SetTextColor(Android.Graphics.Color.ParseColor("#94A3B8"));
                textLayout.AddView(_messagePreviewView);

                _bannerCard.AddView(textLayout);

                // 5. زر عرض المحادثة "عرض 💬"
                var actionBtn = new TextView(activity)
                {
                    Text = "عرض 💬",
                    TextSize = 12,
                    Typeface = cairoBold,
                    Gravity = GravityFlags.Center,
                    Clickable = true,
                    Focusable = true
                };
                actionBtn.SetTextColor(Android.Graphics.Color.White);
                actionBtn.SetPadding(Dp(10), Dp(6), Dp(10), Dp(6));

                var btnBg = new GradientDrawable();
                btnBg.SetColor(Android.Graphics.Color.ParseColor("#0284C7"));
                btnBg.SetCornerRadius(Dp(10));
                actionBtn.Background = btnBg;

                var actionParams = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.WrapContent,
                    ViewGroup.LayoutParams.WrapContent)
                {
                    Gravity = GravityFlags.CenterVertical,
                    LeftMargin = Dp(4),
                    RightMargin = Dp(4)
                };
                actionBtn.LayoutParameters = actionParams;

                actionBtn.Click += (s, e) => OnBannerClicked();
                _bannerCard.AddView(actionBtn);

                // 6. زر الإغلاق ✕
                var closeBtn = new TextView(activity)
                {
                    Text = "✕",
                    TextSize = 14,
                    Typeface = cairoBold,
                    Gravity = GravityFlags.Center,
                    Clickable = true,
                    Focusable = true
                };
                closeBtn.SetTextColor(Android.Graphics.Color.ParseColor("#64748B"));
                closeBtn.SetPadding(Dp(6), Dp(4), Dp(6), Dp(4));
                closeBtn.LayoutParameters = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.WrapContent,
                    ViewGroup.LayoutParams.WrapContent)
                {
                    Gravity = GravityFlags.CenterVertical
                };

                closeBtn.Click += (s, e) => DismissBanner();
                _bannerCard.AddView(closeBtn);

                // النقر على البطاقة بالكامل ينقل أيضاً للمحادثة
                _bannerCard.Click += (s, e) => OnBannerClicked();

                // مستمع السحب للأعلى لإغلاق الشريط بإيماءة سريعة
                _bannerCard.SetOnTouchListener(new SwipeUpTouchListener(DismissBanner));

                // 7. إضافة الشريط لـ DecorView ليظهر دائماً فوق كل الصفحات بدون استثناء
                _overlayContainer.AddView(_bannerCard);
                decorView.AddView(_overlayContainer);

                // 8. تحريك ظهور الشريط بنعومة وانزلاق من الأعلى
                _bannerCard.Alpha = 0f;
                _bannerCard.TranslationY = -Dp(80);
                _bannerCard.Animate()?
                    .TranslationY(0f)
                    .Alpha(1f)
                    .SetDuration(250)
                    .SetInterpolator(new DecelerateInterpolator())
                    .Start();

                // 9. جدولة الإغلاق التلقائي بعد 4 ثوانٍ
                ResetDismissTimer();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InAppNotificationService Error] {ex.Message}");
            }
        });
    }

    private static void UpdateBannerContent(string senderName, string messageText)
    {
        if (_senderNameView != null) _senderNameView.Text = senderName;
        if (_senderInitialView != null) _senderInitialView.Text = string.IsNullOrEmpty(senderName) ? "?" : senderName[0].ToString().ToUpper();
        if (_messagePreviewView != null) _messagePreviewView.Text = string.IsNullOrWhiteSpace(messageText) ? "أرسل لك رسالة" : messageText;
    }

    private static void PulseBanner()
    {
        if (_bannerCard == null) return;
        _bannerCard.Animate()?
            .ScaleX(1.03f)
            .ScaleY(1.03f)
            .SetDuration(120)
            .WithEndAction(new Java.Lang.Runnable(() =>
            {
                _bannerCard?.Animate()?
                    .ScaleX(1.0f)
                    .ScaleY(1.0f)
                    .SetDuration(120)
                    .Start();
            }))
            .Start();
    }

    private static void OnBannerClicked()
    {
        string targetFriend = _currentSender;
        DismissBanner();

        if (string.IsNullOrWhiteSpace(targetFriend)) return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Shell.Current.GoToAsync($"ChatPage?FriendName={Uri.EscapeDataString(targetFriend)}");
            }
            catch { }
        });
    }

    public static void DismissBanner()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            CancelDismissTimer();

            if (_bannerCard == null || _overlayContainer == null)
            {
                RemoveOverlay();
                return;
            }

            try
            {
                float density = _bannerCard.Context?.Resources?.DisplayMetrics?.Density ?? 2.5f;
                float hideY = -(_bannerCard.Height + 50 * density);

                _bannerCard.Animate()?
                    .TranslationY(hideY)
                    .Alpha(0f)
                    .SetDuration(220)
                    .SetInterpolator(new AccelerateInterpolator())
                    .WithEndAction(new Java.Lang.Runnable(RemoveOverlay))
                    .Start();
            }
            catch
            {
                RemoveOverlay();
            }
        });
    }

    private static void RemoveOverlay()
    {
        lock (_lock)
        {
            try
            {
                if (_overlayContainer?.Parent is ViewGroup parent)
                {
                    parent.RemoveView(_overlayContainer);
                }
            }
            catch { }

            _overlayContainer = null;
            _bannerCard = null;
            _senderInitialView = null;
            _senderNameView = null;
            _messagePreviewView = null;
        }
    }

    private static void ResetDismissTimer()
    {
        CancelDismissTimer();
        var cts = new CancellationTokenSource();
        _dismissCts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(4000, cts.Token);
                DismissBanner();
            }
            catch (TaskCanceledException) { }
            catch { }
        });
    }

    private static void CancelDismissTimer()
    {
        try
        {
            _dismissCts?.Cancel();
            _dismissCts?.Dispose();
        }
        catch { }
        _dismissCts = null;
    }

    private static int GetStatusBarHeight(Context context)
    {
        int result = 0;
        try
        {
            int resourceId = context.Resources?.GetIdentifier("status_bar_height", "dimen", "android") ?? 0;
            if (resourceId > 0)
            {
                result = context.Resources!.GetDimensionPixelSize(resourceId);
            }
        }
        catch { }

        if (result <= 0)
        {
            float density = context.Resources?.DisplayMetrics?.Density ?? 2.5f;
            result = (int)(28 * density);
        }

        return result;
    }

    private class SwipeUpTouchListener : Java.Lang.Object, Android.Views.View.IOnTouchListener
    {
        private readonly Action _onSwipeUp;
        private float _startY;

        public SwipeUpTouchListener(Action onSwipeUp)
        {
            _onSwipeUp = onSwipeUp;
        }

        public bool OnTouch(Android.Views.View? v, MotionEvent? e)
        {
            if (e == null) return false;

            switch (e.Action)
            {
                case MotionEventActions.Down:
                    _startY = e.RawY;
                    break;
                case MotionEventActions.Up:
                    float deltaY = _startY - e.RawY;
                    if (deltaY > 30) // سحب لأعلى بمقدار كافٍ
                    {
                        _onSwipeUp();
                        return true;
                    }
                    break;
            }
            return false;
        }
    }
}
