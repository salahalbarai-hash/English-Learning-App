using English.Services;
using English.Models;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace English.Pages;

public partial class ChoiceChallengePage : ContentPage
{
    private class QuestionItem
    {
        public string Word { get; set; } = string.Empty;
        public string CorrectAnswer { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
    }

    private readonly List<QuestionItem> _questions = new();
    private int _currentIndex = 0;
    private int _score = 0;
    private System.Timers.Timer? _timer;
    private int _timePerQuestion = 5;
    private int _timeLeft = 5;
    private bool _answered = false;
    private bool _isLeaving = false;
    private List<WordModel> _memorizedWords = new();

    public ChoiceChallengePage()
    {
        InitializeComponent();
        LoadSetupData();
    }

    private void LoadSetupData()
    {
        _memorizedWords = TenWords.GetMemorizedWords();
        MaxWordsLabel.Text = _memorizedWords.Count.ToString();

        if (_memorizedWords.Count > 0)
        {
            int defaultCount = Math.Min(10, _memorizedWords.Count);
            WordsCountEntry.Text = defaultCount.ToString();
        }
        else
        {
            WordsCountEntry.Text = "10";
        }
    }

    private async void OnStartChallengeClicked(object sender, EventArgs e)
    {
        int count = 10;
        if (!string.IsNullOrWhiteSpace(WordsCountEntry.Text))
        {
            if (!int.TryParse(WordsCountEntry.Text, out count) || count <= 0)
            {
                await Toast.Make("يرجى إدخال عدد صحيح أكبر من الصفر للكلمات.", ToastDuration.Short).Show();
                return;
            }
        }

        if (count > _memorizedWords.Count)
        {
            await Toast.Make($"عدد الكلمات لا يمكن أن يتجاوز الكلمات المحفوظة ({_memorizedWords.Count}).", ToastDuration.Short).Show();
            return;
        }
        
        if (_memorizedWords.Count < 4)
        {
            await Toast.Make("يجب أن تحفظ 4 كلمات على الأقل لتتمكن من اللعب.", ToastDuration.Short).Show();
            return;
        }

        int seconds = 5;
        if (!string.IsNullOrWhiteSpace(TimePerQuestionEntry.Text))
        {
            if (!int.TryParse(TimePerQuestionEntry.Text, out seconds) || seconds <= 0)
            {
                await Toast.Make("يرجى إدخال عدد ثوانٍ صحيح للزمن المسموح.", ToastDuration.Short).Show();
                return;
            }
        }

        _timePerQuestion = seconds;

        GenerateQuestions(count);

        SetupView.IsVisible = false;
        GameView.IsVisible = true;

        LoadQuestion();
    }

    private void GenerateQuestions(int count)
    {
        _questions.Clear();
        var selectedWords = _memorizedWords.OrderBy(w => Guid.NewGuid()).Take(count).ToList();
        var allWords = TenWords.All();
        if (allWords.Count < 4) allWords = _memorizedWords; // fallback

        foreach (var word in selectedWords)
        {
            var options = new List<string> { word.ArabicWord };
            var wrongOptions = allWords
                .Where(w => w.ArabicWord != word.ArabicWord)
                .OrderBy(w => Guid.NewGuid())
                .Take(3)
                .Select(w => w.ArabicWord)
                .ToList();
            
            options.AddRange(wrongOptions);

            _questions.Add(new QuestionItem
            {
                Word = word.EnglishWord,
                CorrectAnswer = word.ArabicWord,
                Options = options.OrderBy(o => Guid.NewGuid()).ToList()
            });
        }
    }

    private void LoadQuestion()
    {
        if (_currentIndex >= _questions.Count)
        {
            EndGame();
            return;
        }

        _answered = false;
        NextButton.IsVisible = false;
        ResetOptionButtons();

        var q = _questions[_currentIndex];
        QuestionWordLabel.Text = q.Word;

        OptionBtn0.Text = q.Options[0];
        OptionBtn1.Text = q.Options[1];
        OptionBtn2.Text = q.Options[2];
        OptionBtn3.Text = q.Options[3];

        StartTimer();
    }

    private void StartTimer()
    {
        StopTimer();
        _timeLeft = _timePerQuestion;
        TimerLabel.Text = _timeLeft.ToString();
        TimerProgress.Progress = 1.0;

        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += (s, e) =>
        {
            _timeLeft--;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                TimerLabel.Text = _timeLeft.ToString();
                TimerProgress.Progress = (double)_timeLeft / (double)_timePerQuestion;

                if (_timeLeft <= 0)
                {
                    StopTimer();
                    OnTimeExpired();
                }
            });
        };
        _timer.Start();
    }

    private void StopTimer()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Dispose();
            _timer = null;
        }
    }

    private void OnTimeExpired()
    {
        if (_answered) return;
        _answered = true;
        HighlightCorrectAnswer();
        NextButton.IsVisible = true;
    }

    private void OnOptionClicked(object sender, TappedEventArgs e)
    {
        if (_answered) return;
        _answered = true;
        StopTimer();

        if (sender is Border border)
        {
            string selected = "";
            if (border == OptionBorder0) selected = OptionBtn0.Text;
            else if (border == OptionBorder1) selected = OptionBtn1.Text;
            else if (border == OptionBorder2) selected = OptionBtn2.Text;
            else if (border == OptionBorder3) selected = OptionBtn3.Text;

            var currentQ = _questions[_currentIndex];

            if (selected == currentQ.CorrectAnswer)
            {
                border.BackgroundColor = Color.FromArgb("#10B981"); // Green
                _score += 10;
                ScoreLabel.Text = _score.ToString();
            }
            else
            {
                border.BackgroundColor = Color.FromArgb("#EF4444"); // Red
                HighlightCorrectAnswer();
            }
        }

        NextButton.IsVisible = true;
    }

    private void HighlightCorrectAnswer()
    {
        var correct = _questions[_currentIndex].CorrectAnswer;
        
        if (OptionBtn0.Text == correct) OptionBorder0.BackgroundColor = Color.FromArgb("#10B981");
        if (OptionBtn1.Text == correct) OptionBorder1.BackgroundColor = Color.FromArgb("#10B981");
        if (OptionBtn2.Text == correct) OptionBorder2.BackgroundColor = Color.FromArgb("#10B981");
        if (OptionBtn3.Text == correct) OptionBorder3.BackgroundColor = Color.FromArgb("#10B981");
    }

    private void ResetOptionButtons()
    {
        Border[] borders = { OptionBorder0, OptionBorder1, OptionBorder2, OptionBorder3 };
        foreach (var b in borders)
        {
            b.BackgroundColor = Color.FromArgb("#111827");
        }
    }

    private void OnNextClicked(object sender, EventArgs e)
    {
        _currentIndex++;
        LoadQuestion();
    }

    private async void EndGame()
    {
        if (_isLeaving) return;
        _isLeaving = true;
        StopTimer();
        await Toast.Make($"انتهاء التحدي 🎉! لقد حصلت على {_score} نقطة.", ToastDuration.Long).Show();
        await Navigation.PopModalAsync();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        if (_isLeaving) return;
        _isLeaving = true;
        StopTimer();
        await Navigation.PopModalAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopTimer();
    }
}