using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Views;

namespace English.Popups;

public partial class FriendSelectPopup : Popup
{
    private string _selectedCategory;
    private List<string> _allFriends = new();

    // المُنشئ المعدل ليقبل القسم وقائمة الأصدقاء المتصلين
    public FriendSelectPopup(string category, List<string> friendsList = null)
    {
        InitializeComponent();
        _selectedCategory = category;

        // عرض القسم المستهدف وحالة المتصلين في العنوان الفرعي للنافذة
        CategorySubtitleLabel.Text = $"القسم: {_selectedCategory} • المتصلون الآن 🟢";

        // إذا تم تمرير القائمة الفعلية، يتم عرضها، وإلا يتم إظهار رسالة عدم وجود متصلين
        if (friendsList != null && friendsList.Count > 0)
        {
            _allFriends = friendsList;
            EmptyFriendsLabel.IsVisible = false;
            FriendsCollectionView.IsVisible = true;
            FriendsCollectionView.ItemsSource = _allFriends;
        }
        else
        {
            EmptyFriendsLabel.Text = "لا يوجد أصدقاء متصلون حالياً 📭";
            EmptyFriendsLabel.IsVisible = true;
            FriendsCollectionView.IsVisible = false;
            SendButton.IsEnabled = false;
            SendButton.Opacity = 0.5;
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_allFriends == null || _allFriends.Count == 0) return;

        string searchText = e.NewTextValue?.Trim().ToLower() ?? "";

        var filtered = string.IsNullOrWhiteSpace(searchText)
            ? _allFriends
            : _allFriends.Where(f => f.ToLower().Contains(searchText)).ToList();

        FriendsCollectionView.ItemsSource = filtered;

        if (filtered.Count == 0)
        {
            EmptyFriendsLabel.Text = "لا يوجد أصدقاء متصلون يطابقون هذا البحث 🔍";
            EmptyFriendsLabel.IsVisible = true;
            FriendsCollectionView.IsVisible = false;
        }
        else
        {
            EmptyFriendsLabel.IsVisible = false;
            FriendsCollectionView.IsVisible = true;
        }
    }

    private async void OnSendChallengeClicked(object sender, EventArgs e)
    {
        var selectedFriend = FriendsCollectionView.SelectedItem as string;

        if (string.IsNullOrEmpty(selectedFriend))
        {
            await Toast.Make("الرجاء اختيار صديق من القائمة أولاً").Show();
            return;
        }

        // إرجاع الصديق المختار وإغلاق النافذة بنجاح
        Close(selectedFriend);
    }
}