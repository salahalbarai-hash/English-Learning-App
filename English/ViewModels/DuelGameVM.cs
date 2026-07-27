using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using Microsoft.AspNetCore.SignalR.Client;
using English.Models;
using English.Hubs;

namespace English.ViewModels;

public class DuelMessage
{
    public string? Sender { get; set; }
    public string? Text { get; set; }
    public bool IsMyMessage { get; set; }
    public bool IsSystemMessage { get; set; }
    public bool IsOpponentMessage => !IsMyMessage && !IsSystemMessage;
}

public class DuelGameVM : INotifyPropertyChanged, IDisposable
{
    private readonly HubConnection _hubConnection;
    private readonly GameHub _gameHub;
    private readonly string _roomName;
    private readonly string _currentUserName;
    private readonly string _opponentName;
    private readonly string _currentCategory;

    private string _mySecretWord = "";
    private string _myArabicMeaning = "";
    private string _opponentSecretWord = "";
    private string _opponentArabicMeaning = "";

    public Action? ScrollToBottomRequested;
    public ObservableCollection<DuelMessage> ChatMessages { get; set; } = new();

    private bool _isSecretWordVisible = true;
    public bool IsSecretWordVisible
    {
        get => _isSecretWordVisible;
        set
        {
            _isSecretWordVisible = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplaySecretWord));
            OnPropertyChanged(nameof(VisibilityIcon));
        }
    }

    public string DisplaySecretWord => IsSecretWordVisible
        ? $"{_mySecretWord} ({_myArabicMeaning})"
        : "••••••••••••";

    public string VisibilityIcon => IsSecretWordVisible ? "🔒 إخفاء" : "👁️ إظهار";

    private bool _isMyTurnToAsk;
    public bool IsMyTurnToAsk
    {
        get => _isMyTurnToAsk;
        set { _isMyTurnToAsk = value; OnPropertyChanged(); }
    }

    private bool _isMyTurnToAnswer;
    public bool IsMyTurnToAnswer
    {
        get => _isMyTurnToAnswer;
        set { _isMyTurnToAnswer = value; OnPropertyChanged(); }
    }

    private string _userQuestion = "";
    public string UserQuestion
    {
        get => _userQuestion;
        set { _userQuestion = value; OnPropertyChanged(); }
    }

    private string _gameStatusText = "جاري اختيار الكلمات السرية تلقائياً...";
    public string GameStatusText
    {
        get => _gameStatusText;
        set { _gameStatusText = value; OnPropertyChanged(); }
    }

    // 🟢 التعديل: تحويل متغير حالة انتهاء اللعبة إلى خاصية عامة
    public bool IsGameOver { get; private set; }

    public ICommand SendMessageCommand { get; }
    public ICommand AnswerYesCommand { get; }
    public ICommand AnswerNoCommand { get; }
    public ICommand AnswerMaybeCommand { get; }
    public ICommand ToggleSecretWordCommand { get; }

    public DuelGameVM(HubConnection hubConnection, GameHub gameHub, string roomName, string currentUserName, string opponentName, string category, bool isFirstPlayer)
    {
        _hubConnection = hubConnection;
        _gameHub = gameHub;
        _roomName = roomName;
        _currentUserName = currentUserName;
        _opponentName = opponentName;
        _currentCategory = category;

        // 🟢 إعداد الدور الأولي
        if (isFirstPlayer)
        {
            IsMyTurnToAsk = true;
            IsMyTurnToAnswer = false;
        }
        else
        {
            IsMyTurnToAsk = false;
            IsMyTurnToAnswer = false;
        }

        SendMessageCommand = new Command(async () => await SendQuestionOrGuessAsync());
        AnswerYesCommand = new Command(async () => await SendAnswerAsync("نعم 👍"));
        AnswerNoCommand = new Command(async () => await SendAnswerAsync("لا 👎"));
        AnswerMaybeCommand = new Command(async () => await SendAnswerAsync("ربما 🤔"));
        ToggleSecretWordCommand = new Command(() => IsSecretWordVisible = !IsSecretWordVisible);

        RegisterSignalREvents();
        _ = InitializeGameWordsAsync();
    }

    private void RegisterSignalREvents()
    {
        if (_gameHub == null) return;

        _gameHub.OnDuelQuestionReceived += HandleDuelQuestion;
        _gameHub.OnDuelAnswerReceived += HandleDuelAnswer;
        _gameHub.OnSecretWordReceived += HandleSecretWord;
        _gameHub.OnDuelWinnerReceived += HandleDuelWinner;

        // 🟢 الاشتراك في حدث استقبال انسحاب الخصم
        _gameHub.OnDuelWithdrawalReceived += HandleDuelWithdrawal;
    }

    // 🟢 دالة تنفيذ انسحابي (تُستدعى من الصفحة عند التأكيد)
    public async Task SurrenderAsync()
    {
        if (IsGameOver) return;
        IsGameOver = true;

        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("SendDuelWithdrawal", _roomName, _currentUserName);
        }
    }

    // 🟢 دالة استقبال انسحاب الخصم
    private void HandleDuelWithdrawal(string withdrawingUser)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (IsGameOver) return;
            IsGameOver = true;
            IsMyTurnToAsk = false;
            IsMyTurnToAnswer = false;

            AddMessage("النظام", $"⚠️ اللاعب {withdrawingUser} انسحب من المباراة! لقد فزت.", false, true);
            GameStatusText = $"انتهت المباراة بانسحاب {withdrawingUser}";
        });
    }

    private void HandleDuelQuestion(string sender, string question)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (IsGameOver) return;
            AddMessage(sender, question, false);

            // استلمت سؤال الخصم، الآن دورك للإجابة
            IsMyTurnToAsk = false;
            IsMyTurnToAnswer = true;
            GameStatusText = $"دورك: أجب على سؤال {_opponentName}";
        });
    }

    private void HandleDuelAnswer(string responder, string answer)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (IsGameOver) return;
            AddMessage(responder, answer, false);

            // الخصم أجاب على سؤالك، الآن دور الخصم ليسأل
            IsMyTurnToAsk = false;
            IsMyTurnToAnswer = false;
            GameStatusText = $"انتظر! {_opponentName} يطرح سؤالاً الآن...";
        });
    }

    private void HandleSecretWord(string targetUser, string word, string arabic)
    {
        if (targetUser == _currentUserName)
        {
            _opponentSecretWord = word.ToLower();
            _opponentArabicMeaning = arabic;
        }
    }

    private void HandleDuelWinner(string winnerName, string correctWord)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsGameOver = true;
            IsMyTurnToAsk = false;
            IsMyTurnToAnswer = false;
            AddMessage("النظام", $"🏆 انتهت اللعبة! الفائز هو: {winnerName}\nالكلمة كانت: {correctWord}", false, true);
            GameStatusText = $"انتهت الجولة بفوز {winnerName}";
        });
    }

    private async Task InitializeGameWordsAsync()
    {
        var wordItem = await GetRandomWordFromJson();
        if (wordItem != null)
        {
            _mySecretWord = wordItem.EnglishWord!.ToLower();
            _myArabicMeaning = wordItem.ArabicWord!;
            OnPropertyChanged(nameof(DisplaySecretWord));

            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("SyncSecretWordForOpponent", _roomName, _opponentName, _mySecretWord, _myArabicMeaning);
            }
        }

        GameStatusText = IsMyTurnToAsk ? $"دورك: ابدأ بطرح سؤال على {_opponentName}" : $"انتظر! {_opponentName} يطرح السؤال الأول...";
        AddMessage("النظام", $"⚔️ بدأت مبارزة التحدي في قسم ({_currentCategory})!\nتم اختيار الكلمات السرية تلقائياً. راقب كلمتك بالأعلى واحمِها!", false, true);
    }

    private async Task SendQuestionOrGuessAsync()
    {
        if (string.IsNullOrWhiteSpace(UserQuestion) || !IsMyTurnToAsk || IsGameOver)
            return;

        string input = UserQuestion.Trim();
        UserQuestion = "";

        // فحص التخمين
        if (!string.IsNullOrEmpty(_opponentSecretWord) && input.Equals(_opponentSecretWord, StringComparison.OrdinalIgnoreCase))
        {
            AddMessage(_currentUserName, $"🎉 لقد خمنت الكلمة الصحيحة: {_opponentSecretWord}!", true);
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("DeclareDuelWinner", _roomName, _currentUserName, _opponentSecretWord);
            }
            IsGameOver = true;
            IsMyTurnToAsk = false;
            IsMyTurnToAnswer = false;
            GameStatusText = "مبروك، لقد فزت بالمبارزة! 🎉";
            return;
        }

        // إرسال السؤال
        AddMessage(_currentUserName, input, true);
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("SendDuelQuestion", _roomName, _currentUserName, input);
        }

        // أرسلت سؤالك، الآن عليك انتظار إجابة الخصم
        IsMyTurnToAsk = false;
        IsMyTurnToAnswer = false;
        GameStatusText = $"انتظر رد {_opponentName}...";
    }

    private async Task SendAnswerAsync(string answer)
    {
        if (!IsMyTurnToAnswer || IsGameOver) return;

        AddMessage(_currentUserName, answer, true);
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("SendDuelAnswer", _roomName, _currentUserName, answer);
        }

        // أجبت على الخصم، الآن دورك لتسأل
        IsMyTurnToAnswer = false;
        IsMyTurnToAsk = true;
        GameStatusText = $"دورك: اطرح سؤالاً جديداً أو خمن الكلمة!";
    }

    private void AddMessage(string sender, string text, bool isMyMessage, bool isSystem = false)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ChatMessages.Add(new DuelMessage
            {
                Sender = sender,
                Text = text,
                IsMyMessage = isMyMessage,
                IsSystemMessage = isSystem
            });
            ScrollToBottomRequested?.Invoke();
        });
    }

    private async Task<WordItem?> GetRandomWordFromJson()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("words.json");
            using var reader = new StreamReader(stream);
            string json = await reader.ReadToEndAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var words = JsonSerializer.Deserialize<List<WordItem>>(json, options);

            var available = words?
                .Where(x => string.Equals(x.Category, _currentCategory, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (available != null && available.Count > 0)
            {
                return available[Random.Shared.Next(available.Count)];
            }
        }
        catch { }
        return null;
    }

    public void Dispose()
    {
        if (_gameHub != null)
        {
            _gameHub.OnDuelQuestionReceived -= HandleDuelQuestion;
            _gameHub.OnDuelAnswerReceived -= HandleDuelAnswer;
            _gameHub.OnSecretWordReceived -= HandleSecretWord;
            _gameHub.OnDuelWinnerReceived -= HandleDuelWinner;

            // 🟢 إزالة الاشتراك
            _gameHub.OnDuelWithdrawalReceived -= HandleDuelWithdrawal;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? prop = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}