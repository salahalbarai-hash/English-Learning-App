using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace English.Pages;

public class MessagesFriendItem
{
    public string Name { get; set; } = string.Empty;
    public string Initials => string.IsNullOrEmpty(Name)
        ? "?"
        : string.Join("", Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => s[0].ToString())).ToUpper();
    public string StatusIcon { get; set; } = "🔴";
    public string StatusText => StatusIcon == "🟢" ? "متصل الآن" : "غير متصل";
}

public partial class MessagesPage : ContentPage
{
    private List<MessagesFriendItem> _allFriends = new();

    public MessagesPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await LoadFriendsAsync();
    }

    private async Task LoadFriendsAsync()
    {
        var shell = Shell.Current as AppShell;
        List<string> friends = new();

        if (shell != null)
        {
            friends = await shell.GetFriendsAsync();
        }
        else
        {
            var userName = Preferences.Get("UserName", "");
            if (!string.IsNullOrEmpty(userName))
            {
                var arr = await Services.Service.GetFriendsAsync(userName);
                if (arr != null) friends = arr.ToList();
            }
        }

        _allFriends = friends.Select(f => new MessagesFriendItem
        {
            Name = f,
            StatusIcon = "🔴"
        }).ToList();

        RefreshList();
    }

    private void RefreshList(string filter = "")
    {
        var filtered = string.IsNullOrWhiteSpace(filter)
            ? _allFriends
            : _allFriends.Where(f => f.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        // Access XAML controls
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
        if (e.CurrentSelection.FirstOrDefault() is MessagesFriendItem item)
        {
            await DisplayAlert("دردشة", $"فتح الدردشة مع {item.Name}", "حسناً");

            if (sender is CollectionView cv) cv.SelectedItem = null;
        }
    }
}