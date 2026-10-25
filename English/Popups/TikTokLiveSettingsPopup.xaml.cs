using CommunityToolkit.Maui.Views;
using English.Models;

namespace English.Popups;

public partial class TikTokLiveSettingsPopup : Popup
{
    private int _selectedTime = 10;
    private int _selectedQuestions = 20;

    public TikTokLiveSettingsPopup()
    {
        InitializeComponent();

        // استرجاع الإعدادات السابقة المحفوظة
        string savedHost = Preferences.Get("TikTok_HostUsername", "");
        HostUsernameEntry.Text = savedHost;

        _selectedTime = Preferences.Get("TikTok_TimePerQuestion", 10);
        UpdateTimeChips(_selectedTime);

        _selectedQuestions = Preferences.Get("TikTok_QuestionsCount", 20);
        UpdateQuestionsChips(_selectedQuestions);

        bool audioOn = Preferences.Get("TikTok_AudioEnabled", true);
        AudioSwitch.IsToggled = audioOn;

        bool simOn = Preferences.Get("TikTok_SimulationMode", false);
        SimulationSwitch.IsToggled = simOn;
    }

    private void OnChipTimeTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is string val && int.TryParse(val, out int t))
        {
            _selectedTime = t;
            UpdateTimeChips(_selectedTime);
        }
    }

    private void UpdateTimeChips(int time)
    {
        Border[] chips = [ChipTime5, ChipTime10, ChipTime15, ChipTime20];
        int[] times = [5, 10, 15, 20];

        for (int i = 0; i < chips.Length; i++)
        {
            bool isSel = (times[i] == time);
            chips[i].BackgroundColor = isSel ? Color.FromArgb("#172554") : Color.FromArgb("#131D30");
            chips[i].Stroke = isSel ? Color.FromArgb("#38BDF8") : Color.FromArgb("#263B5E");
            chips[i].StrokeThickness = isSel ? 1.5 : 1;

            if (chips[i].Content is Label lbl)
            {
                lbl.TextColor = isSel ? Color.FromArgb("#38BDF8") : Color.FromArgb("#94A3B8");
            }
        }

        TimerValueLabel.Text = time switch
        {
            10 => "10 ثوانٍ (موصى به للبث)",
            15 => "15 ثانية (مناسب للشات الكبير)",
            20 => "20 ثانية",
            _ => $"{time} ثوانٍ"
        };
    }

    private void OnChipQuestionsTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is string val && int.TryParse(val, out int q))
        {
            _selectedQuestions = q;
            UpdateQuestionsChips(_selectedQuestions);
        }
    }

    private void UpdateQuestionsChips(int count)
    {
        Border[] chips = [ChipQ10, ChipQ20, ChipQ50, ChipQInf];
        int[] counts = [10, 20, 50, 0];

        for (int i = 0; i < chips.Length; i++)
        {
            bool isSel = (counts[i] == count);
            chips[i].BackgroundColor = isSel ? Color.FromArgb("#172554") : Color.FromArgb("#131D30");
            chips[i].Stroke = isSel ? Color.FromArgb("#38BDF8") : Color.FromArgb("#263B5E");
            chips[i].StrokeThickness = isSel ? 1.5 : 1;

            if (chips[i].Content is Label lbl)
            {
                lbl.TextColor = isSel ? Color.FromArgb("#38BDF8") : Color.FromArgb("#94A3B8");
            }
        }

        QuestionsCountLabel.Text = count switch
        {
            0 => "♾️ مستمر حتى الإيقاف",
            10 => "10 أسئلة",
            20 => "20 سؤالاً",
            50 => "50 سؤالاً",
            _ => $"{count} سؤالاً"
        };
    }

    private void OnCloseTapped(object sender, TappedEventArgs e)
    {
        Close(null);
    }

    private void OnStartClicked(object sender, EventArgs e)
    {
        string host = (HostUsernameEntry.Text ?? "").Trim().TrimStart('@');
        bool isSim = SimulationSwitch.IsToggled;

        if (string.IsNullOrWhiteSpace(host) && !isSim)
        {
            HostUsernameEntry.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(host) && isSim)
        {
            host = "demo_host";
        }

        // حفظ الاختيارات للمرات القادمة
        Preferences.Set("TikTok_HostUsername", host);
        Preferences.Set("TikTok_TimePerQuestion", _selectedTime);
        Preferences.Set("TikTok_QuestionsCount", _selectedQuestions);
        Preferences.Set("TikTok_AudioEnabled", AudioSwitch.IsToggled);
        Preferences.Set("TikTok_SimulationMode", isSim);

        var config = new TikTokLiveConfig
        {
            HostUsername = host,
            TimePerQuestion = _selectedTime,
            QuestionsCount = _selectedQuestions,
            PronounceWord = AudioSwitch.IsToggled,
            AutoNextQuestion = true,
            IsSimulationMode = isSim
        };

        Close(config);
    }
}
