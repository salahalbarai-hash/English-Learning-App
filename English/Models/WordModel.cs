using System.ComponentModel;
using System.Runtime.CompilerServices;

public class WordModel : INotifyPropertyChanged
{
    public string Tag { get; set; } = "";
    public string ArabicWord { get; set; } = "";
    public string EnglishWord { get; set; } = "";
    public bool Locked { get; set; } = false;
    public int Padding { get; set; } = 20;

    private string _currentLanguage = "EN";
    public string CurrentLanguage
    {
        get => _currentLanguage;
        set { _currentLanguage = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayText)); OnPropertyChanged(nameof(DisplayColor)); }
    }

    public string DisplayText => CurrentLanguage == "EN" ? EnglishWord : ArabicWord;
    public Color DisplayColor => CurrentLanguage == "EN" ? Colors.White : Color.FromArgb("#FACC15");

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}