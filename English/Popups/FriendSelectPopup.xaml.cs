using CommunityToolkit.Maui.Views;

namespace English.Popups;

public partial class FriendSelectPopup : Popup
{
    private string _selectedCategory;

    // المُنشئ المعدل ليقبل القسم وقائمة الأصدقاء الفعليين
    public FriendSelectPopup(string category, List<string> friendsList = null)
    {
        InitializeComponent();
        _selectedCategory = category;

        // عرض القسم المستهدف في العنوان الفرعي للنافذة
        CategorySubtitleLabel.Text = $"القسم: {_selectedCategory}";

        // إذا تم تمرير القائمة الفعلية، يتم عرضها، وإلا يتم إظهار رسالة عدم وجود أصدقاء
        if (friendsList != null && friendsList.Count > 0)
        {
            EmptyFriendsLabel.IsVisible = false;
            FriendsCollectionView.IsVisible = true;
            FriendsCollectionView.ItemsSource = friendsList;
        }
        else
        {
            EmptyFriendsLabel.IsVisible = true;
            FriendsCollectionView.IsVisible = false;
            SendButton.IsEnabled = false;
            SendButton.Opacity = 0.5;
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