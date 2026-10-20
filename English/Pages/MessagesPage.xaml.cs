using System.Text.Json;
using English.Helpers;

namespace English.Pages;

public partial class MessagesPage : ContentPage
{
    private List<MessagesFriendItem> _allFriends = [];
    private bool _isNavigating = false;
    private bool _isNavigatingBack = false;
    private DateTime _pageAppearedTime = DateTime.MinValue;

    public MessagesPage()
    {
        InitializeComponent();
        Shell.SetNavBarIsVisible(this, false);
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        _pageAppearedTime = DateTime.UtcNow;
        _isNavigating = false;
        _isNavigatingBack = false;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _pageAppearedTime = DateTime.UtcNow;
        _isNavigating = false;
        _isNavigatingBack = false;

        // إزالة التحديد عند العودة للصفحة
        FriendsList.SelectedItem = null;

        // 1. يبدأ بقراءة الكاش المحلي فوراً (0ms) وتحديث الواجهة قبل التوقف عند await الشبكة
        var loadTask = LoadFriendsAsync();

        // 2. تشغيل حركة ظهور الصفحة بالتوازي دون انتظار انتهاء اتصال الشبكة
        var animationTask = this.AnimatePageInAsync();

        // 3. انتظار اكتمال جلب حالة المتصلين وانتهاء الحركة معاً
        await Task.WhenAll(loadTask, animationTask);
    }

    private async Task LoadFriendsAsync()
    {
        List<string> friendsList = [];
        List<string> onlineUsers = [];

        // 🟢 1. القراءة الفورية المباشرة من الكاش المحلي (Instant Load 0ms)
        var cachedJson = Preferences.Get("Cached_Friends_List", "");
        if (!string.IsNullOrEmpty(cachedJson))
        {
            try
            {
                friendsList = JsonSerializer.Deserialize<List<string>>(cachedJson) ?? [];
            }
            catch { }
        }

        // عرض بيانات الكاش المحلي فوراً قبل انتظار الشبكة حتى تبدأ الحركة والقائمة ممتلئة
        // وحتى لا يظهر EmptyLabel بالخطأ أثناء جلب حالة الاتصال
        if (_allFriends.Count == 0 && friendsList.Count > 0)
        {
            _allFriends = friendsList.Select(f => new MessagesFriendItem
            {
                Name = f,
                StatusIcon = "🔴"
            }).ToList();

            RefreshList();
        }

        // 🟢 2. جلب حالة المستخدمين المتصلين فقط لتحديد النقطة الخضراء/الحمراء
        if (Shell.Current is AppShell appShell)
        {
            try
            {
                var fetchedOnline = await appShell.GetOnlineUsersAsync();
                if (fetchedOnline != null) onlineUsers = fetchedOnline;
            }
            catch { }
        }

        _allFriends = friendsList.Select(f =>
        {
            bool isOnline = onlineUsers.Contains(f, StringComparer.OrdinalIgnoreCase);
            return new MessagesFriendItem
            {
                Name = f,
                StatusIcon = isOnline ? "🟢" : "🔴"
            };
        }).ToList();

        _allFriends = _allFriends
            .OrderByDescending(f => f.StatusIcon == "🟢")
            .ThenBy(f => f.Name)
            .ToList();

        RefreshList();
    }

    private void RefreshList(string filter = "")
    {
        var filtered = string.IsNullOrWhiteSpace(filter)
            ? _allFriends
            : _allFriends.Where(f => f.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        FriendsList.ItemsSource = filtered;
        EmptyLabel.IsVisible = filtered.Count == 0;
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var text = e.NewTextValue ?? string.Empty;
        RefreshList(text.Trim());
    }

    private async void OnFriendSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_isNavigating || (DateTime.UtcNow - _pageAppearedTime).TotalMilliseconds < 350) return;

        if (e.CurrentSelection.FirstOrDefault() is MessagesFriendItem item)
        {
            _isNavigating = true;
            try
            {
                // الانتقال للدردشة أولاً
                await Shell.Current.GoToAsync($"ChatPage?FriendName={item.Name}");
            }
            finally
            {
                _isNavigating = false;
                // إزالة التحديد بعد الانتقال لتجنب الوميض المزعج
                if (sender is CollectionView cv)
                    cv.SelectedItem = null;
            }
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (_isNavigatingBack) return true;
        _isNavigatingBack = true;

        Dispatcher.Dispatch(async () =>
        {
            try
            {
                await Shell.Current.GoToAsync("//MorePage");
            }
            catch
            {
                try { await Shell.Current.GoToAsync(".."); } catch { }
            }
            finally
            {
                await Task.Delay(350);
                _isNavigatingBack = false;
            }
        });

        return true;
    }
}

public class MessagesFriendItem
{
    public string Name { get; set; } = string.Empty;
    public string Initials => string.IsNullOrEmpty(Name)
        ? "?"
        : string.Join("", Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => s[0].ToString())).ToUpper();
    public string StatusIcon { get; set; } = "🔴";
    public string StatusText => StatusIcon == "🟢" ? "متصل الآن" : "غير متصل";

    // 🟢 لون نقطة الاتصال: أخضر زاهي للمتصل وأحمر هادئ للغير متصل
    public Color StatusColor => StatusIcon == "🟢" ? Color.FromArgb("#10B981") : Color.FromArgb("#F43F5E");

    // لون نص الحالة: أخضر للمتصل ورمادي للغير متصل
    public Color StatusTextColor => StatusIcon == "🟢" ? Color.FromArgb("#10B981") : Color.FromArgb("#64748B");
}