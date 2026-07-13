using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Microsoft.Maui.Controls;
using Plugin.MauiMTAdmob;
using Plugin.MauiMTAdmob.Extra;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace English.Services
{
    public class RewardedAd
    {
        private static readonly string rewardedId = "ca-app-pub-2881776829303885/8040223220";

        public static bool isAdLoading = false;
        public static bool isAdShowing = false;
        public static bool isConnected = false;

        /// <summary>
        /// تحميل إعلان المكافأة إذا كان هناك اتصال بالإنترنت ولم يكن الإعلان محمّل مسبقاً.
        /// </summary>
        public static async Task LoadRewardedAd()
        {
            isConnected = await Service.HasActiveInternetAsync(5);

            if (!isConnected || isAdLoading || CrossMauiMTAdmob.Current.IsRewardedLoaded())
                return;

            isAdLoading = true;
            CrossMauiMTAdmob.Current.LoadRewarded(rewardedId, null);
        }

        /// <summary>
        /// عرض إعلان المكافأة وتنفيذ الإجراء بعد مشاهدة الإعلان.
        /// </summary>
        public static async Task ShowRewardedAd(Action action, ActivityIndicator activityIndicator)
        {
            if (isAdShowing)
                return;

            if (isAdLoading || CrossMauiMTAdmob.Current.IsRewardedLoaded())
            {
                isAdShowing = true;
                activityIndicator.IsRunning = true;

                // الانتظار حتى ينتهي تحميل الإعلان
                while (isAdLoading)
                    await Task.Delay(500);

                bool clicked = false;

                EventHandler onClickHandler = (s, e) =>
                {
                    Toast.Make("تمت المشاهدة بنجاح :)", ToastDuration.Long, 14.0)
                         .Show(new CancellationToken());
                    clicked = true;
                };

                EventHandler onClosedHandler = (s, e) =>
                {
                    if (clicked)
                        action?.Invoke();

                    CrossMauiMTAdmob.Current.OnRewardedClicked -= onClickHandler;
                    CrossMauiMTAdmob.Current.OnRewardedClosed -= onClosedHandler;

                    activityIndicator.IsRunning = false;
                    isAdShowing = false;
                };

                CrossMauiMTAdmob.Current.OnRewardedClicked += onClickHandler;
                CrossMauiMTAdmob.Current.OnRewardedClosed += onClosedHandler;

                CrossMauiMTAdmob.Current.ShowRewarded();
            }
            else if (isConnected)
            {
                await Toast.Make("لم يتم تحميل الإعلان بعد، يرجى المحاولة مرة اخرى 🚨", ToastDuration.Short, 14.0)
                           .Show(new CancellationToken());
            }
            else
            {
                await Toast.Make("مشكلة في الاتصال بالإنترنت", ToastDuration.Short, 14.0)
                           .Show(new CancellationToken());
            }

            await LoadRewardedAd();
        }
    }
}
