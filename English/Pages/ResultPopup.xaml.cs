namespace English.Pages;

public partial class ResultPopup : Popup
{
    public ResultPopup(bool isWin, string correctWord)
    {
        InitializeComponent();

        // تعيين الكلمة السرية
        WordLabel.Text = correctWord;

        if (isWin)
        {
            // تصميم الفوز (أخضر نيون متوهج)
            MainBorder.Stroke = Color.FromArgb("#10B981");
            GlowShadow.Brush = Brush.Green;
            IconLabel.Text = "🏆";

            TitleLabel.Text = "عمل أسطوري!";
            TitleLabel.TextColor = Color.FromArgb("#34D399");

            MessageLabel.Text = "أحسنت! لقد اكتشفت الكلمة:";

            ActionButton.BackgroundColor = Color.FromArgb("#10B981");
            ActionButton.TextColor = Colors.White;
            ActionButton.Text = "لعبة جديدة 🚀";
        }
        else
        {
            // تصميم الخسارة (أحمر متوهج)
            MainBorder.Stroke = Color.FromArgb("#EF4444");
            GlowShadow.Brush = Brush.Red;
            IconLabel.Text = "💔";

            TitleLabel.Text = "انتهت اللعبة";
            TitleLabel.TextColor = Color.FromArgb("#F87171");

            MessageLabel.Text = "نفدت محاولاتك! الكلمة كانت:";

            ActionButton.BackgroundColor = Color.FromArgb("#EF4444");
            ActionButton.TextColor = Colors.White;
            ActionButton.Text = "حاول مرة أخرى 🔄";
        }
    }

    private void OnNewGameClicked(object sender, EventArgs e)
    {
        // إغلاق النافذة عند الضغط على الزر
        Close();
    }
}