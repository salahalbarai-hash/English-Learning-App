using Microsoft.Maui.Controls.Shapes;

namespace English.Popups;

public class SingleChallengeResultPopup : Popup
{
    public SingleChallengeResultPopup(int score, int totalQuestions)
    {
        CanBeDismissedByTappingOutsideOfPopup = false;
        Color = Colors.Transparent;

        // حساب النسبة
        double percentage = totalQuestions > 0
            ? (double)score / (totalQuestions * 10) * 100
            : 0;

        string iconText;
        string titleText;
        Color titleColor;
        string messageText;
        Color borderStroke;
        Brush shadowBrush;
        Color buttonBg;

        // يمكنك تغيير نسبة الفوز من هنا
        bool isWin = percentage >= 50;

        if (isWin)
        {
            iconText = "🏆";
            titleText = "أحسنت! لقد فزت 🎉";
            titleColor = Color.FromArgb("#34D399");
            messageText = "ممتاز! لقد حققت نتيجة رائعة وأكملت التحدي بنجاح 🔥";
            borderStroke = Color.FromArgb("#10B981");
            shadowBrush = Brush.Green;
            buttonBg = Color.FromArgb("#10B981");
        }
        else
        {
            iconText = "💪";
            titleText = "حاول مرة أخرى";
            titleColor = Color.FromArgb("#F87171");
            messageText = "لم تحقق نتيجة الفوز هذه المرة، لكن يمكنك المحاولة مجددًا وتحسين نتيجتك!";
            borderStroke = Color.FromArgb("#EF4444");
            shadowBrush = Brush.Red;
            buttonBg = Color.FromArgb("#EF4444");
        }

        // الأيقونة
        var iconLabel = new Label
        {
            Text = iconText,
            FontSize = 60,
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center
        };

        // العنوان
        var titleLabel = new Label
        {
            Text = titleText,
            FontSize = 24,
            FontFamily = "CairoBold",
            TextColor = titleColor,
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center
        };

        // الرسالة
        var messageLabel = new Label
        {
            Text = messageText,
            FontSize = 14,
            FontFamily = "CairoRegular",
            TextColor = Color.FromArgb("#94A3B8"),
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap
        };

        // بطاقة النتيجة
        var scoreTitleLabel = new Label
        {
            Text = "نتيجتك",
            FontSize = 14,
            FontFamily = "CairoRegular",
            TextColor = Color.FromArgb("#94A3B8"),
            HorizontalOptions = LayoutOptions.Center
        };

        var scoreLabel = new Label
        {
            Text = score.ToString(),
            FontSize = 40,
            FontFamily = "CairoBold",
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center
        };

        var percentageLabel = new Label
        {
            Text = $"{percentage:0}%",
            FontSize = 15,
            FontFamily = "CairoBold",
            TextColor = titleColor,
            HorizontalOptions = LayoutOptions.Center
        };

        var scoreCard = new Border
        {
            BackgroundColor = Color.FromArgb("#1E293B"),
            Stroke = Color.FromArgb("#334155"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle
            {
                CornerRadius = 18
            },
            Padding = new Thickness(20, 15),

            Content = new VerticalStackLayout
            {
                Spacing = 3,
                HorizontalOptions = LayoutOptions.Center,

                Children =
                {
                    scoreTitleLabel,
                    scoreLabel,
                    percentageLabel
                }
            }
        };

        // معلومات الأسئلة
        var questionsLabel = new Label
        {
            Text = $"عدد الأسئلة: {totalQuestions}",
            FontSize = 12,
            FontFamily = "CairoRegular",
            TextColor = Color.FromArgb("#64748B"),
            HorizontalOptions = LayoutOptions.Center
        };

        // زر العودة
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

        // الإطار الرئيسي
        var mainBorder = new Border
        {
            WidthRequest = 340,
            BackgroundColor = Color.FromArgb("#0F172A"),
            Stroke = borderStroke,
            StrokeThickness = 2,

            StrokeShape = new RoundRectangle
            {
                CornerRadius = 30
            },

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
                Spacing = 16,
                HorizontalOptions = LayoutOptions.Center,

                Children =
                {
                    iconLabel,
                    titleLabel,
                    messageLabel,
                    scoreCard,
                    questionsLabel,
                    actionButton
                }
            }
        };

        Content = mainBorder;
    }
}