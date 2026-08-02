using CommunityToolkit.Maui.Views;
using English.Hubs;
using Microsoft.Maui.Controls.Shapes;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace English.Pages;

public class CategoryItem : BindableObject
{
    public string Name { get; set; }

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

    // 🟢 ألوان الفئات (متطابقة مع ألوان تحديد وضع اللعب)
    public Color BackgroundColor => IsSelected ? Color.FromArgb("#112A38") : Color.FromArgb("#151624");
    public Color TextColor => IsSelected ? Colors.White : Color.FromArgb("#94A3B8");
    public Color StrokeColor => IsSelected ? Color.FromArgb("#6366F1") : Color.FromArgb("#312E81");
}

public partial class SmartInspectorPage : ContentPage
{
    public ObservableCollection<CategoryItem> CategoriesList { get; set; } = new();
    private CategoryItem _selectedCategoryItem;
    private bool _isMultiplayer = false;

    // قائمة الفئات المسموح بظهورها (يمكنك التعديل عليها مستقبلاً)
    private readonly List<string> _allowedCategories = new()
    {
        "صفات", "طعام", "افعال", "ادوات", "حيوانات", "مهن", "الاشكال"
    };

    public SmartInspectorPage()
    {
        InitializeComponent();
        UpdateGameModeUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadCategoriesFastAsync();
    }

    private async Task LoadCategoriesFastAsync()
    {
        try
        {
            // جلب عدد الكلمات المحفوظة للطالب
            int memorizedCount = Preferences.Get("MemorizedWords", 0);

            var availableCategories = await Task.Run(async () =>
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync("words.json");
                using var reader = new StreamReader(stream);
                var jsonString = await reader.ReadToEndAsync();

                using var doc = JsonDocument.Parse(jsonString);
                var uniqueCategories = new HashSet<string>();
                int currentWordIndex = 0;

                // الدوران على الكلمات الموجودة في الجيسون
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    // التوقف إذا تجاوزنا عدد الكلمات التي حفظها الطالب
                    if (currentWordIndex >= memorizedCount)
                        break;

                    if (element.TryGetProperty("Category", out var categoryElement) ||
                        element.TryGetProperty("category", out categoryElement))
                    {
                        var cat = categoryElement.GetString();
                        // التحقق مما إذا كانت الفئة ضمن القائمة المسموحة ولم تكن فارغة
                        if (!string.IsNullOrWhiteSpace(cat) && _allowedCategories.Contains(cat))
                        {
                            uniqueCategories.Add(cat);
                        }
                    }
                    currentWordIndex++;
                }

                // ترتيب الفئات لتظهر بنفس الترتيب الموجود في قائمتك الأساسية
                return _allowedCategories.Where(c => uniqueCategories.Contains(c)).ToList();
            });

            // تحديث واجهة المستخدم
            CategoriesList.Clear();
            foreach (var cat in availableCategories)
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
                // إظهار رسالة إذا لم يكن هناك أي فئات مفتوحة بعد
                EmptyCategoriesLabel.IsVisible = true;
                _selectedCategoryItem = null;
            }

            BindableLayout.SetItemsSource(CategoriesContainer, CategoriesList);
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ", $"حدث خطأ أثناء معالجة البيانات: {ex.Message}", "حسناً");
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

    private void OnSinglePlayerTapped(object sender, TappedEventArgs e)
    {
        _isMultiplayer = false;
        UpdateGameModeUI();
    }

    private void OnMultiplayerTapped(object sender, TappedEventArgs e)
    {
        _isMultiplayer = true;
        UpdateGameModeUI();
    }

    private void UpdateGameModeUI()
    {
        // 🟢 ألوان بطاقات وضع اللعب
        Color normalBorderColor = Color.FromArgb("#6366F1");
        Color normalBgColor = Color.FromArgb("#151624");
        Color normalTextColor = Color.FromArgb("#94A3B8");

        // الألوان عند التحديد
        Color selectedBorderColor = Color.FromArgb("#6366F1");
        Color selectedBgColor = Color.FromArgb("#112A38");
        Color selectedTextColor = Colors.White;

        // تحديث كرت اللعب الفردي
        SinglePlayerCard.BackgroundColor = !_isMultiplayer ? selectedBgColor : normalBgColor;
        SinglePlayerCard.Stroke = !_isMultiplayer ? selectedBorderColor : normalBorderColor;
        SinglePlayerText.TextColor = !_isMultiplayer ? selectedTextColor : normalTextColor;

        // تحديث كرت اللعب المتعدد (تحدي صديق)
        MultiplayerCard.BackgroundColor = _isMultiplayer ? selectedBgColor : normalBgColor;
        MultiplayerCard.Stroke = _isMultiplayer ? selectedBorderColor : normalBorderColor;
        MultiplayerText.TextColor = _isMultiplayer ? selectedTextColor : normalTextColor;
    }

    private async void OnStartGameClicked(object sender, EventArgs e)
    {
        if (_selectedCategoryItem == null)
        {
            await DisplayAlert("تنبيه", "يجب فتح فئة واحدة على الأقل لبدء اللعب!", "حسناً");
            return;
        }

        if (!_isMultiplayer)
        {
            // --- وضع اللعب الفردي ---
            await Navigation.PushModalAsync(new GuessGamePage(_selectedCategoryItem.Name));
        }
        else
        {
            // --- وضع تحدي صديق ---
            try
            {
                List<string> myFriends = await FetchFriendsFromDatabaseAsync();

                if (myFriends == null || myFriends.Count == 0)
                {
                    await DisplayAlert("عذراً", "ليس لديك أصدقاء مضافين حالياً لتحديهم. قم بإضافة أصدقاء أولاً!", "حسناً");
                    return;
                }

                var popup = new Popups.FriendSelectPopup(_selectedCategoryItem.Name, myFriends);
                var result = await this.ShowPopupAsync(popup);

                if (result is string targetFriend && !string.IsNullOrEmpty(targetFriend))
                {
                    if (Shell.Current is AppShell appShell)
                    {
                        string currentUser = Preferences.Get("UserName", "");
                        if (appShell.GameHub != null && (appShell.GameHub.HubConnection == null || appShell.GameHub.HubConnection.State != Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected))
                        {
                            await appShell.GameHub.ConnectAsync(currentUser);
                        }

                        // إرسال الطلب للسيرفر 
                        await appShell.GameHub.SendChallengeAsync(targetFriend, _selectedCategoryItem.Name);

                        var waitingPopup = new Popups.WaitingChallengePopup(targetFriend);

                        // 🟢 الاستماع لرد السيرفر وتمرير القيمة المنطقية كما هي
                        Action<string, bool, string> onChallengeResponded = (responder, isAccepted, category) =>
                        {
                            if (responder == targetFriend)
                            {
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    waitingPopup.CloseWithResult(isAccepted); // تمرير bool
                                });
                            }
                        };

                        appShell.GameHub.OnChallengeResponseReceived += onChallengeResponded;

                        // عرض نافذة الانتظار
                        var waitResult = await this.ShowPopupAsync(waitingPopup);

                        appShell.GameHub.OnChallengeResponseReceived -= onChallengeResponded;

                        // 🔵 معالجة النتيجة
                        if (waitResult is string statusResult)
                        {
                            if (statusResult == "Accepted")
                            {
                                // الصديق قبل التحدي (تم إزالة كود الانتقال لأن AppShell سيتولى ذلك)
                            }
                            else if (statusResult == "Rejected")
                            {
                                await Toast.Make($"{targetFriend} رفض التحدي أو هو مشغول حالياً.").Show();
                            }
                            else if (statusResult == "Cancel")
                            {
                                // المستخدم ألغى الطلب بنفسه
                                await appShell.GameHub.CancelChallengeAsync(targetFriend);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("خطأ في الاتصال", $"تعذر بدء التحدي: {ex.Message}", "حسناً");
                Console.WriteLine($"Error in multiplayer game start: {ex.Message}");
            }
        }
    }

    // 🟢 دالة مساعدة لجلب الأصدقاء من قاعدة البيانات
    private async Task<List<string>> FetchFriendsFromDatabaseAsync()
    {
        // جلب اسم المستخدم الحالي المخزن في التطبيق
        var currentUserName = Preferences.Get("UserName", "");

        if (string.IsNullOrEmpty(currentUserName))
            return new List<string>();

        // استدعاء دالة الـ API لجلب الأصدقاء فعلياً
        string[] friendsArray = await Service.GetFriendsAsync(currentUserName);

        // تحويل المصفوفة إلى قائمة وإرجاعها
        return [.. friendsArray];
    }    
}