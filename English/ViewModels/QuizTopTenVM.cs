using English.Models;
using English.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace English.ViewModels
{
    public enum ExamStage { MultiChoice, Listening, Writing, Finished }

    public class QuizTopTenVM : INotifyPropertyChanged
    {
        public Action OnGameOver { get; set; }
        public Action<int> OnWin { get; set; }

        public List<WordModel> ExamWords { get; set; }
        private List<WordModel> _originalTenWords;

        private int _totalCorrectAnswers = 0;
        private int _wordIndexInStage = 0;
        private int _hearts = 2;
        private ExamStage _currentStage = ExamStage.MultiChoice;
        public int Hearts
        {
            get => _hearts;
            set
            {
                _hearts = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Heart1Opacity));
                OnPropertyChanged(nameof(Heart2Opacity));

                if (_hearts <= 0)
                    OnGameOver?.Invoke();
            }
        }

        public double Heart1Opacity => Hearts >= 1 ? 1.0 : 0.2;
        public double Heart2Opacity => Hearts >= 2 ? 1.0 : 0.2;

        public ExamStage CurrentStage
        {
            get => _currentStage;
            set
            {
                _currentStage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentStageName));
                OnPropertyChanged(nameof(StageColor));

                if (_currentStage == ExamStage.Finished)
                    OnWin?.Invoke(0);
            }
        }

        private ObservableCollection<ExamStage> _currentStageList;
        public ObservableCollection<ExamStage> CurrentStageList
        {
            get => _currentStageList;
            set { _currentStageList = value; OnPropertyChanged(); }
        }

        public string CurrentStageName => CurrentStage switch
        {
            ExamStage.MultiChoice => "💎 جولة الاختيارات (1/3)",
            ExamStage.Listening => "🎧 جولة الاستماع (2/3)",
            ExamStage.Writing => "✍️ جولة الكتابة (3/3)",
            _ => "🏁 انتهى السباق"
        };

        public string StageColor => CurrentStage switch
        {
            ExamStage.MultiChoice => "#A855F7",
            ExamStage.Listening => "#FF3D00",
            ExamStage.Writing => "#2DD4BF",
            _ => "#FFFFFF"
        };

        public WordModel? CurrentWord => 
            (ExamWords != null && _totalCorrectAnswers < ExamWords.Count)
            ? ExamWords[_totalCorrectAnswers]
            : null;

        private ObservableCollection<string> _shuffledOptions;
        public ObservableCollection<string> ShuffledOptions
        {
            get => _shuffledOptions;
            set { _shuffledOptions = value; OnPropertyChanged(); }
        }

        private string _userAnswer;
        public string UserAnswer
        {
            get => _userAnswer;
            set { _userAnswer = value; OnPropertyChanged(); }
        }

        public ICommand SelectAnswerCommand { get; set; }

        public QuizTopTenVM(List<WordModel> words)
        {
            _originalTenWords = [.. words.OrderBy(x => Guid.NewGuid()).Take(10)]; // salah

            ExamWords = new List<WordModel>();
            ExamWords.AddRange(_originalTenWords.OrderBy(x => Guid.NewGuid()));
            ExamWords.AddRange(_originalTenWords.OrderBy(x => Guid.NewGuid()));
            ExamWords.AddRange(_originalTenWords.OrderBy(x => Guid.NewGuid()));

            CurrentStageList = new ObservableCollection<ExamStage> { CurrentStage };

            GenerateShuffledOptions();

            SelectAnswerCommand = new Command<string>(answer => ProcessAnswer(answer));
        }

        public void GenerateShuffledOptions()
        {
            if (CurrentWord == null) return;

            var correct = CurrentWord.EnglishWord;

            var wrongOptions = _originalTenWords
                .Where(w => w.EnglishWord != correct)
                .Select(w => w.EnglishWord)
                .Distinct()
                .OrderBy(x => Guid.NewGuid())
                .ToList();

            var options = new List<string>();

            // نأخذ فقط المتاح
            options.AddRange(wrongOptions.Take(Math.Min(3, wrongOptions.Count)));

            // نضيف الصحيح
            options.Add(correct);

            // نخلط
            ShuffledOptions = new ObservableCollection<string>(
                options.OrderBy(x => Guid.NewGuid())
            );
        }

        public async Task<bool> ProcessAnswer(string userAnswer)
        {
            if (CurrentWord == null || Hearts <= 0) return false;

            bool isCorrect =
                !string.IsNullOrWhiteSpace(userAnswer) &&
                string.Equals(userAnswer.Trim(),
                              CurrentWord.EnglishWord,
                              StringComparison.OrdinalIgnoreCase);

            if (isCorrect)
            {
                await Sounds.PlayAsync(Sounds.Correct());

                MoveNext();
                GenerateShuffledOptions();
            }
            else
            {
                Hearts--;
                await Sounds.PlayAsync(Sounds.Wrong());
            }

            return isCorrect;
        }

        public async Task<bool> CheckWritingAnswerAsync()
        {
            if (CurrentWord == null || Hearts <= 0) return false;

            bool isCorrect =
                !string.IsNullOrWhiteSpace(UserAnswer) &&
                string.Equals(UserAnswer.Trim(),
                              CurrentWord.EnglishWord,
                              StringComparison.OrdinalIgnoreCase);

            if (isCorrect)
                MoveNext();
            else
                Hearts--;

            await Sounds.PlayAsync(isCorrect ? Sounds.Correct() : Sounds.Wrong());
            UserAnswer = string.Empty;
            OnPropertyChanged(nameof(CurrentWord));

            return isCorrect;
        }

        public async Task SpeakCurrentWord()
        {
            if (CurrentWord == null) return;
            await TextToSpeech.SpeakAsync(CurrentWord.EnglishWord);
        }

        private void MoveNext()
        {
            _totalCorrectAnswers++;
            _wordIndexInStage++;

            OnPropertyChanged(nameof(CurrentWord));

            if (_wordIndexInStage >= 10) // salah
            {
                _wordIndexInStage = 0;
                MoveToNextStage();
            }
            else
            {
                CurrentStageList = new ObservableCollection<ExamStage> { CurrentStage };
            }
        }

        private void MoveToNextStage()
        {
            if (CurrentStage == ExamStage.MultiChoice)
                CurrentStage = ExamStage.Listening;
            else if (CurrentStage == ExamStage.Listening)
                CurrentStage = ExamStage.Writing;
            else
                CurrentStage = ExamStage.Finished;

            CurrentStageList = new ObservableCollection<ExamStage> { CurrentStage };
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}