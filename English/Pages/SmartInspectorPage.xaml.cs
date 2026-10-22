using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Views;
using English.Hubs;
using English.Helpers;
using English.Services;
using Microsoft.Maui.Controls.Shapes;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace English.Pages;

public class CategoryItem : BindableObject
{
    public string Name { get; set; } = string.Empty;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
            OnPropertyChanged(nameof(BackgroundColor));
            OnPropertyChanged(nameof(TextColor));
            OnPropertyChanged(nameof(StrokeColor));
        }
    }

    // 🟢 ألوان الفئات (متناسقة مع ثيم البنفسجي الداكن)
    public Color BackgroundColor => IsSelected ? Color.FromArgb("#261E4E") : Color.FromArgb("#141726");
    public Color TextColor => IsSelected ? Colors.White : Color.FromArgb("#94A3B8");
    public Color StrokeColor => IsSelected ? Color.FromArgb("#8B5CF6") : Color.FromArgb("#25283D");
}

public partial class SmartInspectorPage : ContentPage
{
    public ObservableCollection<CategoryItem> CategoriesList { get; set; } = new();
    private CategoryItem? _selectedCategoryItem;
    private bool _isMultiplayer = true; // نشط افتراضياً لتجنب اللعب الفردي
    private bool _isNavigating = false;

    // قائمة الفئات المسموح بظهورها
    private readonly List<string> _allowedCategories = new()
    {
        "صفات", "طعام", "افعال", "ادوات", "حيوانات", "مهن", "الاشكال"
    };

    public SmartInspectorPage()
    {
        InitializeComponent();
        _isMultiplayer = true;
        UpdateGameModeUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _isNavigating = false;
        SetLoadingState(false);
        this.AnimatePageInAsync();
        await LoadCategoriesFastAsync();
    }

    // ⚡ تحميل فائق السرعة بالاعتماد على الكاش الداخلي للكلمات بدون إعادة قراءة الملف من الديسك
    private async Task LoadCategoriesFastAsync()
    {
        try
        {
            int memorizedCount = Preferences.Get("MemorizedWords", 0);
            var allWords = await TenWords.GetAllWordsAsync();

            var uniqueCategories = allWords
                .Take(memorizedCount)
                .Where(w => !string.IsNullOrWhiteSpace(w.Category) && _allowedCategories.Contains(w.Category))
                .Select(w => w.Category)
                .Distinct()
                .ToHashSet();

            var orderedCategories = _allowedCategories.Where(c => uniqueCategories.Contains(c)).ToList();

            CategoriesList.Clear();
            foreach (var cat in orderedCategories)
            {
                CategoriesList.Add(new CategoryItem { Name = cat, IsSelected = false });
            }

            if (CategoriesList.Count > 0)
            {
                EmptyCategoriesLabel.IsVisible = false;
                _selectedCategoryItem = CategoriesList[0];
                _selectedCategoryItem.IsSelected = true;
            }
            else
            {
                EmptyCategoriesLabel.IsVisible = true;
                _selectedCategoryItem = null;
            }

            BindableLayout.SetItemsSource(CategoriesContainer, CategoriesList);
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ", $"حدث خطأ أثناء تحميل الفئات: {ex.Message}", "حسناً");
        }
    }

    private void OnCategoryTapped(object sender, TappedEventArgs e)
    {
        if (sender is Border border && border.BindingContext is CategoryItem tappedItem)
        {
            if (_selectedCategoryItem != null)
                _selectedCategoryItem.IsSelected = false;

            tappedItem.IsSelected = true;
            _selectedCategoryItem = tappedItem;
        }
    }

    private async void OnSinglePlayerTapped(object sender, TappedEventArgs e)
    {
        // إشعار فوري بأن الميزة قيد التطوير مع إبقاء وضع التحدي مفعلاً
        await Toast.Make("طور اللعب الفردي قيد التطوير وستتم إتاحته قريباً ⏳").Show();
        _isMultiplayer = true;
        UpdateGameModeUI();
    }

    private void OnMultiplayerTapped(object sender, TappedEventArgs e)
    {
        _isMultiplayer = true;
        UpdateGameModeUI();
    }

    private void UpdateGameModeUI()
    {
        Color normalBorderColor = Color.FromArgb("#2D3248");
        Color normalBgColor = Color.FromArgb("#141724");
        Color normalTextColor = Color.FromArgb("#64748B");

        Color selectedBorderColor = Color.FromArgb("#8B5CF6");
        Color selectedBgColor = Color.FromArgb("#1E1B3D");
        Color selectedTextColor = Colors.White;

        // كرت اللعب الفردي (مخصص ليظهر كوضع مستقبلي)
        SinglePlayerCard.BackgroundColor = normalBgColor;
        SinglePlayerCard.Stroke = normalBorderColor;
        SinglePlayerText.TextColor = normalTextColor;
        SinglePlayerCard.Opacity = 0.65;

        // كرت تحدي صديق (الوضع الأساسي والفعال دائماً)
        MultiplayerCard.BackgroundColor = selectedBgColor;
        MultiplayerCard.Stroke = selectedBorderColor;
        MultiplayerText.TextColor = selectedTextColor;
        MultiplayerCard.Opacity = 1.0;
    }

    // 🔄 التحكم بحالة التحميل لزر التحدي (Native Button لمنع أي تقلص في الارتفاع)
    private void SetLoadingState(bool isLoading, string statusText = "")
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StartLoadingIndicator.IsRunning = isLoading;
            StartLoadingIndicator.IsVisible = isLoading;
            StartChallengeButton.Text = isLoading ? statusText : "🚀 بدء التحدي مع صديق";
        });
    }

    private async void OnStartGameClicked(object sender, EventArgs e)
    {
        if (_isNavigating) return;
        _isNavigating = true;

        try
        {
            if (_selectedCategoryItem == null)
            {
                await DisplayAlert("تنبيه", "يجب فتح فئة واحدة على الأقل لبدء اللعب! احفظ مزيداً من الكلمات لفتحها.", "حسناً");
                return;
            }

            // فحص رصيد محاولات التحدي
            long remainingChallenges = Preferences.Get("FriendsChallengeCount", 0L);
            if (remainingChallenges <= 0)
            {
                await Toast.Make("ليس لديك محاولات تحدي كافية ⚔️! احفظ كلمات إضافية لكسب محاولات جديدة.").Show();
                return;
            }

            // تفعيل حالة التحميل والمؤشر على الزر تماماً كزر التحديث في لوحة المتصدرين
            SetLoadingState(true, "جاري فحص المتصلين... 🔍");

            // فحص الاتصال بالإنترنت أولاً بسرعة
            if (!await Service.HasActiveInternetAsync(5))
            {
                await Toast.Make("لا يوجد اتصال بالإنترنت. يرجى التحقق من الشبكة للمتابعة.").Show();
                return;
            }

            AppShell? appShell = Shell.Current as AppShell ?? Application.Current?.MainPage as AppShell;
            if (appShell == null)
            {
                await Toast.Make("تعذر الوصول لخدمات الاتصال.").Show();
                return;
            }

            string currentUser = Preferences.Get("UserName", "");
            if (appShell.GameHub != null && (appShell.GameHub.HubConnection == null || appShell.GameHub.HubConnection.State != Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected))
            {
                SetLoadingState(true, "جاري الاتصال بالخادم... ⚡");
                await appShell.GameHub.ConnectAsync(currentUser);
            }

            // جلب الأصدقاء وقائمة المتصلين بالتوازي لسرعة استجابة فائقة
            SetLoadingState(true, "جاري فحص الأصدقاء المتصلين... 🔍");
            var friendsTask = FetchFriendsFromDatabaseAsync();
            var onlineUsersTask = appShell.GetOnlineUsersAsync();

            await Task.WhenAll(friendsTask, onlineUsersTask);

            List<string> myFriends = await friendsTask;
            List<string> onlineUsers = (await onlineUsersTask) ?? new List<string>();

            // استخدام الكاش المحلي كاحتياطي سريع إذا لم ترجع قاعدة البيانات شيئاً
            if (myFriends == null || myFriends.Count == 0)
            {
                try
                {
                    string cachedFriendsJson = Preferences.Get("Cached_Friends_List", "[]");
                    myFriends = JsonSerializer.Deserialize<List<string>>(cachedFriendsJson) ?? new List<string>();
                }
                catch { }
            }

            if (myFriends == null || myFriends.Count == 0)
            {
                await Toast.Make("ليس لديك أصدقاء مضافين حالياً لتحديهم. قم بإضافة أصدقاء أولاً! 👥").Show();
                return;
            }

            // 🟢 التصفية الصارمة: إظهار الأصدقاء المتصلين فقط
            var onlineFriends = myFriends
                .Where(f => onlineUsers.Contains(f, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (onlineFriends.Count == 0)
            {
                await Toast.Make("لا يوجد أي صديق متصل حالياً للمبارزة 📭").Show();
                return;
            }

            // إعادة الزر لحالته الطبيعية قبل فتح نافذة الاختيار
            SetLoadingState(false);

            // فتح نافذة اختيار الصديق بالمتصلين فقط
            var popup = new Popups.FriendSelectPopup(_selectedCategoryItem.Name, onlineFriends);
            var result = await this.ShowPopupAsync(popup);

            if (result is string targetFriend && !string.IsNullOrEmpty(targetFriend))
            {
                if (appShell.GameHub != null && (appShell.GameHub.HubConnection == null || appShell.GameHub.HubConnection.State != Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected))
                {
                    await appShell.GameHub.ConnectAsync(currentUser);
                }

                if (appShell.GameHub == null || appShell.GameHub.HubConnection == null || appShell.GameHub.HubConnection.State != Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected)
                {
                    await Toast.Make("تعذر الاتصال بخادم التحديات حالياً. يرجى المحاولة بعد لحظات.").Show();
                    return;
                }

                // إرسال الطلب للسيرفر 
                await appShell.GameHub.SendChallengeAsync(targetFriend, _selectedCategoryItem.Name);

                var waitingPopup = new Popups.WaitingChallengePopup(targetFriend);

                Action<string, bool, string> onChallengeResponded = (responder, isAccepted, category) =>
                {
                    if (responder == targetFriend)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            waitingPopup.CloseWithResult(isAccepted);
                        });
                    }
                };

                try
                {
                    appShell.GameHub.OnChallengeResponseReceived += onChallengeResponded;

                    // عرض نافذة الانتظار
                    var waitResult = await this.ShowPopupAsync(waitingPopup);

                    if (waitResult is string statusResult)
                    {
                        if (statusResult == "Accepted")
                        {
                            // الصديق قبل التحدي (AppShell سيتولى فتح صفحة DuelGamePage)
                        }
                        else if (statusResult == "Rejected")
                        {
                            await Toast.Make($"{targetFriend} رفض التحدي أو هو مشغول حالياً.").Show();
                        }
                        else if (statusResult == "Cancel")
                        {
                            await appShell.GameHub.CancelChallengeAsync(targetFriend);
                        }
                    }
                }
                finally
                {
                    // التأكد التام من إزالة المستمع لتجنب تسريب الذاكرة وتكرار الأحداث
                    appShell.GameHub.OnChallengeResponseReceived -= onChallengeResponded;
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ في الاتصال", $"تعذر بدء التحدي: {ex.Message}", "حسناً");
        }
        finally
        {
            SetLoadingState(false);
            _isNavigating = false;
        }
    }

    // دالة مساعدة سريعة لجلب الأصدقاء
    private async Task<List<string>> FetchFriendsFromDatabaseAsync()
    {
        var currentUserName = Preferences.Get("UserName", "");
        if (string.IsNullOrEmpty(currentUserName))
            return new List<string>();

        try
        {
            string[] friendsArray = await Service.GetFriendsAsync(currentUserName);
            return [.. friendsArray];
        }
        catch
        {
            return new List<string>();
        }
    }
}