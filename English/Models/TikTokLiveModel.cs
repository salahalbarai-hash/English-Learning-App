using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace English.Models;

public class TikTokViewerScore : INotifyPropertyChanged
{
    private int _rank;
    private string _rankBadge = "";
    private string _userName = "";
    private string _displayName = "";
    private int _score;
    private int _fastestAnswersCount;

    public int Rank
    {
        get => _rank;
        set
        {
            if (_rank != value)
            {
                _rank = value;
                OnPropertyChanged();
                UpdateRankBadge();
            }
        }
    }

    public string RankBadge
    {
        get => _rankBadge;
        private set { _rankBadge = value; OnPropertyChanged(); }
    }

    public string UserName
    {
        get => _userName;
        set { _userName = value; OnPropertyChanged(); }
    }

    public string DisplayName
    {
        get => _displayName;
        set { _displayName = value; OnPropertyChanged(); }
    }

    public int Score
    {
        get => _score;
        set { _score = value; OnPropertyChanged(); }
    }

    public int FastestAnswersCount
    {
        get => _fastestAnswersCount;
        set { _fastestAnswersCount = value; OnPropertyChanged(); }
    }

    private void UpdateRankBadge()
    {
        RankBadge = _rank switch
        {
            1 => "🥇",
            2 => "🥈",
            3 => "🥉",
            4 => "4️⃣",
            5 => "5️⃣",
            _ => $"{_rank}"
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class OptionVoteState : INotifyPropertyChanged
{
    private int _index;
    private string _numberText = "1";
    private string _optionText = "";
    private int _votesCount = 0;
    private double _percentage = 0.0;
    private bool _isCorrect = false;
    private bool _isRevealed = false;

    public int Index
    {
        get => _index;
        set { _index = value; OnPropertyChanged(); }
    }

    public string NumberText
    {
        get => _numberText;
        set { _numberText = value; OnPropertyChanged(); }
    }

    public string OptionText
    {
        get => _optionText;
        set { _optionText = value; OnPropertyChanged(); }
    }

    public int VotesCount
    {
        get => _votesCount;
        set { _votesCount = value; OnPropertyChanged(); }
    }

    public double Percentage
    {
        get => _percentage;
        set
        {
            _percentage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PercentageString));
        }
    }

    public string PercentageString => $"{(int)(_percentage * 100)}%";

    public bool IsCorrect
    {
        get => _isCorrect;
        set { _isCorrect = value; OnPropertyChanged(); }
    }

    public bool IsRevealed
    {
        get => _isRevealed;
        set { _isRevealed = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class TikTokLiveComment
{
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string CommentText { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public int? ChoiceIndex { get; set; } // 0, 1, 2, 3 if matched "1", "2", "3", "4"
}

public class TikTokLiveConfig
{
    public string HostUsername { get; set; } = "";
    public int TimePerQuestion { get; set; } = 10;
    public int QuestionsCount { get; set; } = 20; // 0 means continuous
    public bool PronounceWord { get; set; } = true;
    public bool AutoNextQuestion { get; set; } = true;
    public bool IsSimulationMode { get; set; } = false;
}
