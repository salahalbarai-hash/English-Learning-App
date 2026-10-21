using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using English.Services;
using Shared.Models;
using System.Collections.ObjectModel;

namespace English.ViewModels
{
    public partial class LeadersPageVM : ObservableObject
    {
        public ObservableCollection<Leader> TopTenLeaders { get; } = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        private bool isBusy;

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private string refreshButtonText = "تحديث";

        [ObservableProperty]
        private bool isErrorVisible;

        [ObservableProperty]
        private bool showOverlay;

        public bool IsNotBusy => !IsBusy;

        public LeadersPageVM() { }

        public async Task LoadLeadersAsync(bool isManualRefresh = false)
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                IsErrorVisible = false;

                if (isManualRefresh)
                {
                    IsRefreshing = true;
                    RefreshButtonText = "جاري التحديث...";
                }
                else if (TopTenLeaders.Count == 0)
                {
                    ShowOverlay = true;
                }

                // 1. فحص الاتصال بالإنترنت
                if (!await Service.HasActiveInternetAsync(4))
                {
                    HandleNetworkError(isManualRefresh, "يرجى الاتصال بالإنترنت 📶");
                    return;
                }

                // 2. مزامنة بيانات حساب المستخدم الحالي إن وُجد بالتوازي
                if (isManualRefresh)
                {
                    await SyncCurrentUserDataAsync();
                }

                // 3. جلب قائمة المتصدرين من الخادم
                var leaders = await Service.GetStudents();

                if (leaders != null && leaders.Length > 0)
                {
                    var sortedLeaders = leaders
                        .OrderByDescending(l => l.Coins)
                        .ThenByDescending(l => l.MemorizedWords)
                        .ToList();

                    for (int i = 0; i < sortedLeaders.Count; i++)
                    {
                        sortedLeaders[i].Rank = i + 1;
                    }

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        TopTenLeaders.Clear();
                        foreach (var leader in sortedLeaders)
                        {
                            TopTenLeaders.Add(leader);
                        }

                        ShowOverlay = false;
                        IsErrorVisible = false;
                    });

                    if (isManualRefresh)
                    {
                        await Toast.Make("تم التحديث بنجاح ✔", ToastDuration.Short).Show();
                    }
                }
                else
                {
                    HandleNetworkError(isManualRefresh, "تعذر جلب بيانات المتصدرين من الخادم");
                }
            }
            catch (Exception ex)
            {
                HandleNetworkError(isManualRefresh, ex.Message);
            }
            finally
            {
                IsBusy = false;
                IsRefreshing = false;
                RefreshButtonText = "تحديث";

                if (!IsErrorVisible && TopTenLeaders.Count > 0)
                {
                    ShowOverlay = false;
                }
            }
        }

        private async Task SyncCurrentUserDataAsync()
        {
            try
            {
                string userName = Preferences.Get("UserName", "");
                string password = Preferences.Get("Password", "");

                if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
                    return;

                var result = await Service.GetUser(new User
                {
                    UserName = userName,
                    Password = password,
                });

                if (result.Success && result.Data != null)
                {
                    User user = result.Data;
                    int memorizedWords = Preferences.Get("MemorizedWords", 0);
                    long friendsChallengeCount = Preferences.Get("FriendsChallengeCount", 0L);
                    long coins = Preferences.Get("Coins", 0L);

                    if (!long.TryParse(Preferences.Get("ID", "0")?.Trim('"'), out long id))
                    {
                        id = user.ID;
                    }

                    var syncTasks = new List<Task>();

                    if (memorizedWords < user.MemorizedWords)
                    {
                        Preferences.Set("MemorizedWords", user.MemorizedWords);
                    }
                    else if (memorizedWords > user.MemorizedWords && id > 0)
                    {
                        syncTasks.Add(Service.UpdateMemorizedWords(new User
                        {
                            ID = id,
                            MemorizedWords = memorizedWords
                        }));
                    }

                    if (friendsChallengeCount < user.FriendsChallengeCount)
                    {
                        Preferences.Set("FriendsChallengeCount", user.FriendsChallengeCount);
                    }
                    else if (friendsChallengeCount > user.FriendsChallengeCount && id > 0)
                    {
                        syncTasks.Add(Service.UpdateFriendsChallengeCount(new User
                        {
                            ID = id,
                            FriendsChallengeCount = friendsChallengeCount
                        }));
                    }

                    if (coins < user.Coins)
                    {
                        Preferences.Set("Coins", user.Coins);
                    }
                    else if (coins > user.Coins && id > 0)
                    {
                        syncTasks.Add(Service.UpdateCoins(new User
                        {
                            ID = id,
                            Coins = coins
                        }));
                    }

                    if (syncTasks.Count > 0)
                    {
                        await Task.WhenAll(syncTasks);
                    }
                }
            }
            catch
            {
                // لا نوقف تحديث المتصدرين في حال حدوث خطأ في مزامنة الحساب الفردي
            }
        }

        private void HandleNetworkError(bool isManualRefresh, string errorMessage)
        {
            if (TopTenLeaders.Count == 0)
            {
                IsErrorVisible = true;
                ShowOverlay = true;
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Toast.Make(errorMessage, ToastDuration.Short).Show();
                });
            }
        }
    }
}