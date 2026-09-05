using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls.Shapes;

namespace English.Popups;

public class ChallengeResultPopup : Popup
{
    public ChallengeResultPopup(string myName, int myScore, string opponentName, int opponentScore)
    {
        CanBeDismissedByTappingOutsideOfPopup = false;
        Color = Colors.Transparent;

        string iconText;
        string titleText;
        Color titleColor;
        string messageText;
        Color borderStroke;
        Brush shadowBrush;
        Color buttonBg;

        if (myScore > opponentScore)
        {
            // Win
            iconText = "🏆";
            titleText = "تهانينا! لقد فزت 🥳";
            titleColor = Color.FromArgb("#34D399"); // Emerald Green
            messageText = "أداء رائع! استطعت التغلب على خصمك بنجاح 🔥";
            borderStroke = Color.FromArgb("#10B981");
            shadowBrush = Brush.Green;
            buttonBg = Color.FromArgb("#10B981");
        }
        else if (myScore < opponentScore)
        {
            // Loss
            iconText = "😔";
            titleText = "خيرها في غيرها 💔";
            titleColor = Color.FromArgb("#F87171"); // Coral Red
            messageText = "للأسف تفوق عليك الخصم هذه المرة، حاول مجدداً!";
            borderStroke = Color.FromArgb("#EF4444");
            shadowBrush = Brush.Red;
            buttonBg = Color.FromArgb("#EF4444");
        }
        else
        {
            // Tie
            iconText = "🤝";
            titleText = "تعادل ممتاز! 🤝";
            titleColor = Color.FromArgb("#38BDF8"); // Sky Blue
            messageText = "مباراة متكافئة وقوية جداً بين الطرفين!";
            borderStroke = Color.FromArgb("#0EA5E9");
            shadowBrush = Brush.LightBlue;
            buttonBg = Color.FromArgb("#0EA5E9");
        }

        var iconLabel = new Label
        {
            Text = iconText,
            FontSize = 60,
            HorizontalOptions = LayoutOptions.Center
        };

        var titleLabel = new Label
        {
            Text = titleText,
            FontSize = 24,
            FontFamily = "CairoBold",
            TextColor = titleColor,
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var messageLabel = new Label
        {
            Text = messageText,
            FontSize = 14,
            TextColor = Color.FromArgb("#94A3B8"),
            FontFamily = "CairoRegular",
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center
        };

        // Score Cards
        var myNameLabel = new Label
        {
            Text = string.IsNullOrWhiteSpace(myName) ? "أنا" : myName,
            FontSize = 13,
            FontFamily = "CairoBold",
            TextColor = Color.FromArgb("#38BDF8"),
            HorizontalOptions = LayoutOptions.Center
        };

        var myScoreLabel = new Label
        {
            Text = myScore.ToString(),
            FontSize = 24,
            FontFamily = "CairoBold",
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center
        };

        var myCard = new Border
        {
            BackgroundColor = Color.FromArgb("#1E293B"),
            Stroke = Color.FromArgb("#334155"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(12, 10),
            Content = new VerticalStackLayout
            {
                Spacing = 4,
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    myNameLabel,
                    myScoreLabel,
                    new Label { Text = "نقاط", FontSize = 11, FontFamily = "CairoRegular", TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center }
                }
            }
        };

        var opponentNameLabel = new Label
        {
            Text = string.IsNullOrWhiteSpace(opponentName) ? "الخصم" : opponentName,
            FontSize = 13,
            FontFamily = "CairoBold",
            TextColor = Color.FromArgb("#F43F5E"),
            HorizontalOptions = LayoutOptions.Center
        };

        var opponentScoreLabel = new Label
        {
            Text = opponentScore.ToString(),
            FontSize = 24,
            FontFamily = "CairoBold",
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center
        };

        var opponentCard = new Border
        {
            BackgroundColor = Color.FromArgb("#1E293B"),
            Stroke = Color.FromArgb("#334155"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(12, 10),
            Content = new VerticalStackLayout
            {
                Spacing = 4,
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    opponentNameLabel,
                    opponentScoreLabel,
                    new Label { Text = "نقاط", FontSize = 11, FontFamily = "CairoRegular", TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center }
                }
            }
        };

        var scoresGrid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = GridLength.Star } },
            ColumnSpacing = 12,
            Margin = new Thickness(0, 10)
        };
        scoresGrid.Add(myCard, 0);
        scoresGrid.Add(opponentCard, 1);

        var actionButton = new Button
        {
            Text = "العودة للقائمة الرئيسية ➔",
            HeightRequest = 52,
            CornerRadius = 20,
            FontFamily = "CairoBold",
            FontSize = 16,
            BackgroundColor = buttonBg,
            TextColor = Colors.White,
            Margin = new Thickness(0, 5, 0, 0)
        };
        actionButton.Clicked += (s, e) => Close();

        var mainBorder = new Border
        {
            WidthRequest = 340,
            BackgroundColor = Color.FromArgb("#0F172A"),
            StrokeThickness = 2,
            Stroke = borderStroke,
            StrokeShape = new RoundRectangle { CornerRadius = 30 },
            Padding = new Thickness(25, 30, 25, 25),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Shadow = new Shadow
            {
                Offset = new Point(0, 0),
                Radius = 25,
                Opacity = 0.5f,
                Brush = shadowBrush
            },
            Content = new VerticalStackLayout
            {
                Spacing = 18,
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    iconLabel,
                    titleLabel,
                    messageLabel,
                    scoresGrid,
                    actionButton
                }
            }
        };

        Content = mainBorder;
    }
}
