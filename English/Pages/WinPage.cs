using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using English.Models;
using English.Services;
using Microsoft.Maui.Controls.Shapes;

namespace English.Pages;

public class WinPage : ContentPage
{
    private Label _timeLabel;
    private Border _mainCard;
    Grid? loadingOverlay;

    public WinPage(int finalSeconds)
    {
        int memorizedWords = Preferences.Get("MemorizedWords", 0) + 10;
        Preferences.Set("MemorizedWords", memorizedWords);

        NavigationPage.SetHasNavigationBar(this, false);
        this.FlowDirection = FlowDirection.LeftToRight;

        Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops = new GradientStopCollection
            {
                new GradientStop { Color = Color.FromArgb("#061406"), Offset = 0.0f },
                new GradientStop { Color = Color.FromArgb("#122A12"), Offset = 1.0f }
            }
        };

        _timeLabel = new Label
        {
            Text = "⏱️ 00:00:00",
            TextColor = Colors.White,
            FontSize = 20,
            FontFamily = "CairoBold",
            HorizontalTextAlignment = TextAlignment.Center
        };

        var timeBadge = new Border
        {
            BackgroundColor = Color.FromArgb("#1A401A"),
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#00C853").WithAlpha(0.3f),
            StrokeShape = new RoundRectangle { CornerRadius = 20 },
            Padding = new Thickness(20, 8),
            HorizontalOptions = LayoutOptions.Center,
            Content = _timeLabel
        };

        var backButton = new Button
        {
            Text = "الرئيسية",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#00E676"),
            BorderColor = Color.FromArgb("#00E676"),
            BorderWidth = 2,
            FontFamily = "CairoBold",
            FontSize = 16,
            HeightRequest = 55,
            CornerRadius = 15
        };
        backButton.Clicked += async (s, e) =>
            await Application.Current.MainPage.Navigation.PopToRootAsync(false);

        var saveButton = new Button
        {
            Text = "حفظ النتيجة",
            BackgroundColor = Color.FromArgb("#00C853"),
            TextColor = Colors.White,
            FontFamily = "CairoBold",
            FontSize = 16,
            HeightRequest = 55,
            CornerRadius = 15
        };

        saveButton.Clicked += async (s, e) =>
        {
            long id =  Convert.ToInt64(Preferences.Get("ID", "0"));
            string time = _timeLabel.Text.Replace("⏱️ ", "");
            loadingOverlay.IsVisible = true;

            if (await Service.HasActiveInternetAsync(5))
            {
                int memorizedWords = Preferences.Get("MemorizedWords", 0);
                string result = await Service.UpdateMemorizedWords(new User
                {
                    ID = id,
                    MemorizedWords = memorizedWords
                });

                string message = "تم الحفظ بنجاح 🔥";
                if (result == "1")
                {
                    Preferences.Set("TimeFinalExam", time);
                }
                else
                {
                    message = "حدث خطأ 😓";
                }

                await Toast.Make(message, ToastDuration.Short, 14).Show(new CancellationToken());
            }
            else
            {
                await Toast.Make("يرجى الاتصال بالانترنت 📶", ToastDuration.Short, 14).Show(new CancellationToken());
            }

            loadingOverlay.IsVisible = false;
        };

        var buttonsGrid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() },
            ColumnSpacing = 15
        };
        buttonsGrid.Add(saveButton, 0);
        buttonsGrid.Add(backButton, 1);

        _mainCard = new Border
        {
            WidthRequest = 340,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Padding = new Thickness(25, 30),
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#00C853").WithAlpha(0.4f),
            BackgroundColor = Color.FromArgb("#0B1F0B"),
            StrokeShape = new RoundRectangle { CornerRadius = 30 },
            Opacity = 0,
            Scale = 0.7,
            Content = new VerticalStackLayout
            {
                Spacing = 20,
                Children =
                {
                    new Image { Source = "winner.png", HeightRequest = 140, HorizontalOptions = LayoutOptions.Center },
                    new Label { Text = "أداء ممتاز! 🎉", TextColor = Color.FromArgb("#00E676"), FontSize = 28, FontFamily = "CairoBold", HorizontalTextAlignment = TextAlignment.Center },
                    new Label { Text = "لقد أتممت الاختبار بنجاح، استمر في هذا التألق.", TextColor = Colors.LightGray, FontSize = 14, FontFamily = "Cairo", HorizontalTextAlignment = TextAlignment.Center },
                    timeBadge,
                    buttonsGrid
                }
            }
        };

        // ✅ المحتوى الأساسي
        var rootGrid = new Grid
        {
            Children = { _mainCard }
        };

        // ✅ إنشاء الـ Overlay بعد تحديد المحتوى
        CreateLoadingOverlay();

        // ✅ إضافة الـ Overlay فوق المحتوى
        rootGrid.Children.Add(loadingOverlay);

        Content = rootGrid;

        Loaded += async (s, e) =>
        {
            await Task.WhenAll(
                _mainCard.FadeTo(1, 600, Easing.CubicOut),
                _mainCard.ScaleTo(1, 600, Easing.SpringOut)
            );

            _ = AnimateTime(finalSeconds);
        };
    }

    private async Task AnimateTime(int targetSeconds)
    {
        for (int i = 0; i <= targetSeconds; i++)
        {
            TimeSpan t = TimeSpan.FromSeconds(i);
            _timeLabel.Text = $"⏱️ {t:hh\\:mm\\:ss}";
            await Task.Delay(20);
        }
    }

    protected override bool OnBackButtonPressed()
    {
        Dispatcher.Dispatch(async () =>
        {
            await Application.Current.MainPage.Navigation.PopToRootAsync(false);
        });

        return true;
    }

    void CreateLoadingOverlay()
    {
        loadingOverlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            IsVisible = false,
            ZIndex = 999
        };

        var frame = new Frame
        {
            BackgroundColor = Colors.White,
            CornerRadius = 20,
            Padding = 25,
            HasShadow = true,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Content = new VerticalStackLayout
            {
                Spacing = 15,
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    new ActivityIndicator
                    {
                        IsRunning = true,
                        Color = Color.FromArgb("#00C853"),
                        WidthRequest = 50,
                        HeightRequest = 50
                    },
                    new Label
                    {
                        Text = "جارٍ حفظ إنجازك... 🏆",
                        FontSize = 16,
                        TextColor = Colors.Black,
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                }
            }
        };

        loadingOverlay.Children.Add(frame);
    }
}