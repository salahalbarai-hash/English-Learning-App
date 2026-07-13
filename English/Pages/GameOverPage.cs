using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace English.Pages;

public class GameOverPage : ContentPage
{
    public GameOverPage(string reason)
    {
        NavigationPage.SetHasNavigationBar(this, false);

        BackgroundColor = Color.FromArgb("#111111");
        var backButton = new Button
        {
            Text = "العودة للقائمة الرئيسية",
            BackgroundColor = Color.FromArgb("#FF3D00"),
            TextColor = Colors.White,
            FontFamily = "CairoBold",
            FontSize = 18,
            HeightRequest = 60,
            CornerRadius = 20,
            Margin = new Thickness(0, 10, 0, 0)
        };

        // Clicked event
        backButton.Clicked += async (s, e) =>
        {
            await Application.Current.MainPage.Navigation.PopToRootAsync(false);
        };

        Content = new Grid
        {
            Children =
            {
                new Border
                {
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Center,
                    Padding = 30,
                    StrokeThickness = 2,
                    Stroke = Color.FromArgb("#FF3D00"),
                    BackgroundColor = Color.FromArgb("#111111"),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
                    {
                        CornerRadius = 30
                    },
                    Content = new VerticalStackLayout
                    {
                        Spacing = 20,
                        WidthRequest = 300,
                        Children =
                        {
                            new Image
                            {
                                Source = "loser_cry.GIF",
                                IsAnimationPlaying = true,
                                HeightRequest = 180,
                                HorizontalOptions = LayoutOptions.Center
                            },

                            new Label
                            {
                                Text = "انتهت المحاولات! 💔",
                                TextColor = Color.FromArgb("#FF3D00"),
                                FontSize = 28,
                                FontFamily = "CairoBold",
                                HorizontalTextAlignment = TextAlignment.Center
                            },

                            new Label
                            {
                                Text = reason,
                                TextColor = Colors.Gray,
                                FontSize = 16,
                                HorizontalTextAlignment = TextAlignment.Center
                            },

                            backButton
                        }
                    }
                }
            }
        };
    }

    // زر الرجوع في الهاتف
    protected override bool OnBackButtonPressed()
    {
        Dispatcher.Dispatch(async () =>
        {
            await Application.Current.MainPage.Navigation.PopToRootAsync(false);
        });

        return true;
    }
}