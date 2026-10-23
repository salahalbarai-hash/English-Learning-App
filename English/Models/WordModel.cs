using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

public class WordModel : INotifyPropertyChanged
{
    public int Id { get; set; }
    public string Tag { get; set; } = "";
    public string Category { get; set; } = "";
    public string ArabicWord { get; set; } = "";
    public string EnglishWord { get; set; } = "";
    public bool Locked { get; set; } = false;
    public int Padding { get; set; } = 20;

    private string _currentLanguage = "EN";
    [JsonIgnore]
    public string CurrentLanguage
    {
        get => _currentLanguage;
        set { _currentLanguage = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayText)); OnPropertyChanged(nameof(DisplayColor)); }
    }

    [JsonIgnore]
    public string DisplayText => CurrentLanguage == "EN" ? EnglishWord : ArabicWord;

    [JsonIgnore]
    public Color DisplayColor => CurrentLanguage == "EN" ? Colors.White : Color.FromArgb("#FACC15");

    private bool _isListening;
    [JsonIgnore]
    public bool IsListening
    {
        get => _isListening;
        set
        {
            _isListening = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MicButtonBgBrush));
            OnPropertyChanged(nameof(MicButtonBorderBrush));
            OnPropertyChanged(nameof(MicButtonIcon));
            OnPropertyChanged(nameof(CardStrokeBrush));
        }
    }

    private bool _isPronouncedCorrectly;
    [JsonIgnore]
    public bool IsPronouncedCorrectly
    {
        get => _isPronouncedCorrectly;
        set
        {
            _isPronouncedCorrectly = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MicButtonBgBrush));
            OnPropertyChanged(nameof(MicButtonBorderBrush));
            OnPropertyChanged(nameof(MicButtonIcon));
            OnPropertyChanged(nameof(CardStrokeBrush));
        }
    }

    private static readonly SolidColorBrush BrushMicBgDefault = new(Color.FromArgb("#0F172A"));
    private static readonly SolidColorBrush BrushMicBgListening = new(Color.FromArgb("#7F1D1D"));
    private static readonly SolidColorBrush BrushMicBgCorrect = new(Color.FromArgb("#064E3B"));

    private static readonly SolidColorBrush BrushMicBorderDefault = new(Color.FromArgb("#334155"));
    private static readonly SolidColorBrush BrushMicBorderListening = new(Color.FromArgb("#EF4444"));
    private static readonly SolidColorBrush BrushMicBorderCorrect = new(Color.FromArgb("#10B981"));

    private static readonly SolidColorBrush BrushCardStrokeDefault = new(Color.FromArgb("#263B5E"));
    private static readonly SolidColorBrush BrushCardStrokeListening = new(Color.FromArgb("#EF4444"));
    private static readonly SolidColorBrush BrushCardStrokeCorrect = new(Color.FromArgb("#10B981"));

    [JsonIgnore]
    public Brush MicButtonBgBrush => IsListening 
        ? BrushMicBgListening 
        : (IsPronouncedCorrectly ? BrushMicBgCorrect : BrushMicBgDefault);

    [JsonIgnore]
    public Brush MicButtonBorderBrush => IsListening
        ? BrushMicBorderListening 
        : (IsPronouncedCorrectly ? BrushMicBorderCorrect : BrushMicBorderDefault);

    [JsonIgnore]
    public string MicButtonIcon => IsListening
        ? "🔴" 
        : (IsPronouncedCorrectly ? "⭐" : "🎙️");

    [JsonIgnore]
    public Brush CardStrokeBrush => IsListening
        ? BrushCardStrokeListening
        : (IsPronouncedCorrectly ? BrushCardStrokeCorrect : BrushCardStrokeDefault);

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}