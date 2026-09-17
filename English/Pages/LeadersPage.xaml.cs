using CommunityToolkit.Maui.Core;
using English.ViewModels;

namespace English.Pages;

public partial class LeadersPage : ContentPage
{
    private LeadersPageVM ViewModel => (LeadersPageVM)BindingContext;

    public LeadersPage()
    {
        InitializeComponent();
        BindingContext = new LeadersPageVM();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await Task.Delay(200);

        if (ViewModel.TopTenLeaders.Count == 0)
        {
            await ViewModel.LoadLeadersAsync();
        }
    }

    private async void RefreshView_Refreshing(object sender, EventArgs e)
    {
        try
        {
            int memorizedWords = Preferences.Get("MemorizedWords", 0);
            long friendsChallengeCount = Preferences.Get("FriendsChallengeCount", 0L);
            long coins = Preferences.Get("Coins", 0L);
            long id = Convert.ToInt64(Preferences.Get("ID", "0"));

            if (await Service.HasActiveInternetAsync(5))
            {
                var result = await Service.GetUser(new User
                {
                    UserName = Preferences.Get("UserName", ""),
                    Password = Preferences.Get("Password", ""),
                });

                if (!result.Success)
                {
                    await Toast.Make(result.Message ?? "حدث خطأ").Show();
                    return;
                }

                User user = result.Data!;

                await UpdateMemorizedWords(id, memorizedWords, user.MemorizedWords);            
                await UpdateFriendsChallengeCount(id, friendsChallengeCount, user.FriendsChallengeCount);
                await UpdateCoins(id, coins, user.Coins);

                // 🔥 هنا تحديث لوحة المتصدرين
                await ViewModel.LoadLeadersAsync();
            }
            else
            {
                await Toast.Make("يرجى الاتصال بالانترنت 📶").Show();
            }
        }
        catch (Exception ex)
        {
            await Toast.Make(ex.Message).Show();
        }
    }

    private async Task UpdateMemorizedWords(long userId, int localCount, int serverCount)
    {
        if (localCount < serverCount)
        {
            Preferences.Set("MemorizedWords", serverCount);
        }
        else if (localCount > serverCount)
        {
            await Service.UpdateMemorizedWords(new User
            {
                ID = userId,
                MemorizedWords = localCount
            });
        }
    }

    private async Task UpdateCoins(long userId, long localCount, long serverCount)
    {
        if (localCount < serverCount)
        {
            Preferences.Set("Coins", serverCount);
        }
        else if (localCount > serverCount)
        {
            await Service.UpdateCoins(new User
            {
                ID = userId,
                Coins = localCount
            });
        }
    }

    private async Task UpdateFriendsChallengeCount(long userId, long localCount, long serverCount)
    {
        if (localCount < serverCount)
        {
            Preferences.Set("FriendsChallengeCount", serverCount);
        }
        else if (localCount > serverCount)
        {
            await Service.UpdateFriendsChallengeCount(new User
            {
                ID = userId,
                FriendsChallengeCount = localCount
            });
        }
    }
}