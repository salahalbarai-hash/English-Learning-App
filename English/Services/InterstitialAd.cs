using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;

namespace English.Services;

public static class InterstitialAd
{
    private static bool _isLoading;
    private static bool _isShowing;

    private const string AdUnitId = "ca-app-pub-2881776829303885/5286428762";

    public static async Task LoadAsync()
    {
        if (_isLoading || CrossMauiMTAdmob.Current.IsInterstitialLoaded())
            return;

        if (!await Service.HasActiveInternetAsync(5))
            return;

        _isLoading = true;

        CrossMauiMTAdmob.Current.OnInterstitialLoaded += OnLoaded;
        CrossMauiMTAdmob.Current.LoadInterstitial(AdUnitId);
    }

    private static void OnLoaded(object? sender, EventArgs e)
    {
        _isLoading = false;
        CrossMauiMTAdmob.Current.OnInterstitialLoaded -= OnLoaded;
    }

    public static async Task<bool> ShowAsync()
    {
        if (_isShowing)
            return false;

        if (!CrossMauiMTAdmob.Current.IsInterstitialLoaded())
            return false;

        _isShowing = true;
        bool completed = false;

        EventHandler onClicked = (_, __) => completed = true;

        EventHandler onClosed = (_, __) =>
        {
            _isShowing = false;
            CrossMauiMTAdmob.Current.OnInterstitialClicked -= onClicked;
            CrossMauiMTAdmob.Current.OnInterstitialClosed -= onClosed;
        };

        CrossMauiMTAdmob.Current.OnInterstitialClicked += onClicked;
        CrossMauiMTAdmob.Current.OnInterstitialClosed += onClosed;

        CrossMauiMTAdmob.Current.ShowInterstitial();

        await LoadAsync(); // تجهيز الإعلان القادم
        return completed;
    }
}
