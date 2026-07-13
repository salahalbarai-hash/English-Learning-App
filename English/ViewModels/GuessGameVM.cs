namespace English.ViewModels;

public class GuessGameVM : INotifyPropertyChanged
{
    private static readonly HttpClient _httpClient = new HttpClient();

    private string _targetWord = "";
    private string _targetArabic = "";
    private string _systemInstruction = "";
    private readonly List<object> _conversationHistory = [];

    public Action? ScrollToBottomRequested;

    public ObservableCollection<ChatMessage> ChatMessages { get; set; } = new();

    private int _remainingAttempts;
    public int RemainingAttempts
    {
        get => _remainingAttempts;
        set
        {
            _remainingAttempts = value;
            OnPropertyChanged();
        }
    }

    private bool _isWaitingForAI;
    public bool IsWaitingForAI
    {
        get => _isWaitingForAI;
        set
        {
            _isWaitingForAI = value;
            OnPropertyChanged();
        }
    }

    private string _thinkingStatus = ".";
    public string ThinkingStatus
    {
        get => _thinkingStatus;
        set
        {
            _thinkingStatus = value;
            OnPropertyChanged();
        }
    }

    private CancellationTokenSource? _thinkingCancellation;

    private string _userQuestion = "";
    public string UserQuestion
    {
        get => _userQuestion;
        set
        {
            _userQuestion = value;
            OnPropertyChanged();
        }
    }

    public ICommand SendMessageCommand { get; }
    public ICommand StartNewGameCommand { get; }
    public ICommand HelpCommand { get; }

    public GuessGameVM()
    {
        SendMessageCommand = new Command(async () => await SendMessageAsync());
        StartNewGameCommand = new Command(async () => await StartNewGameAsync());
        HelpCommand = new Command(async () => await RequestHelpAsync());

        _ = StartNewGameAsync();
    }

    private void StartThinkingAnimation()
    {
        _thinkingCancellation?.Cancel();
        _thinkingCancellation = new CancellationTokenSource();

        var token = _thinkingCancellation.Token;

        Task.Run(async () =>
        {
            string[] dots = [".", "..", "..."];
            int index = 0;

            while (!token.IsCancellationRequested)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ThinkingStatus = dots[index];
                });

                index = (index + 1) % 3;

                await Task.Delay(400, token);
            }

        }, token);
    }

    private void StopThinkingAnimation()
    {
        _thinkingCancellation?.Cancel();
        ThinkingStatus = "";
    }


    private async Task StartNewGameAsync()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ChatMessages.Clear();
            RemainingAttempts = 20;
            UserQuestion = "";
        });


        var wordItem = await GetRandomWordFromJson();

        _targetWord = wordItem!.EnglishWord!.ToLower();
        _targetArabic = wordItem.ArabicWord!;


        _systemInstruction = $@"أنت 'المفتش الذكي' في تطبيق 'انجليش'، وتدير تحدي (20 سؤال) بصرامة وذكاء عالي.

الكلمة السرية التي يجب على المستخدم تخمينها هي:
'{_targetWord}'

المعنى العربي للكلمة (معلومة داخلية لك فقط ولا يجوز كشفها):
'{_targetArabic}'


القواعد الصارمة للرد:

1. الأسئلة الاستنتاجية:
إذا سأل المستخدم سؤالاً عن طبيعة الكلمة، أجب بكلمة واحدة فقط من:
(نعم - لا - ربما - غالباً - أحياناً - نادراً).

لا تضف أي شرح.


2. الأخطاء الإملائية:
إذا كتب المستخدم كلمة قريبة جداً من الكلمة السرية مع خطأ بسيط في حرف أو حرفين، لا تقل لا.

اسأله فقط:
'هل تقصد {_targetWord}؟'


3. محاولات الغش:
إذا طلب المستخدم:
- الحرف الأول.
- عدد الأحرف.
- الترجمة.
- الكلمة مباشرة.
- معلومة تكشف الإجابة.

ارفض الطلب بأسلوب قصير ومرح.


4. السرية التامة:
يمنع منعاً باتاً كشف الكلمة السرية أو ترجمتها أو إعطاء مرادف مباشر لها.


5. التلميحات:
عند طلب المستخدم تلميحاً:
- اعتمد على المعنى العربي لتحديد المقصود.
- لا تستخدم معنى آخر للكلمة إذا كانت متعددة المعاني.
- لا تذكر الكلمة السرية.
- لا تذكر الترجمة العربية.
- لا تعطِ مرادفاً مباشراً.
- لا تستخدم استعارات أو تشبيهات.
- اجعل التلميح معلومة عملية تساعد اللاعب على الاستنتاج.
- اجعل التلميح قصيراً جداً.


6. الخروج عن النص:
إذا تحدث المستخدم خارج اللعبة، أجب فقط:
'دعنا نركز في التحدي!'";


        _conversationHistory.Clear();


        AddMessage("🎯 اخترت كلمة سرية جديدة!\nلديك 20 محاولة. ابدأ بطرح أسئلة نعم أو لا.", true);
    }
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(UserQuestion) || IsWaitingForAI || RemainingAttempts <= 0)
            return;


        bool hasInternet = await Service.HasActiveInternetAsync(4);

        if (!hasInternet)
        {
            await Toast.Make("لا يوجد إنترنت").Show();
            return;
        }


        IsWaitingForAI = true;
        StartThinkingAnimation();


        try
        {
            string input = UserQuestion.Trim();
            UserQuestion = "";


            AddMessage(input, false);


            // التحقق من الفوز
            if (input.Equals(_targetWord, StringComparison.OrdinalIgnoreCase))
            {
                AddMessage($"🎉 ممتاز! الكلمة هي {_targetWord}", true);


                var popup = new ResultPopup(isWin: true, correctWord: _targetWord);

                if (Application.Current?.MainPage != null)
                {
                    await Application.Current.MainPage.ShowPopupAsync(popup);
                }


                await StartNewGameAsync();
                return;
            }


            RemainingAttempts--;


            _conversationHistory.Add(new
            {
                role = "user",
                parts = new[]
                {
                    new
                    {
                        text = input
                    }
                }
            });



            string response = await CallGeminiApiAsync(_systemInstruction, _conversationHistory);



            _conversationHistory.Add(new
            {
                role = "model",
                parts = new[]
                {
                    new
                    {
                        text = response
                    }
                }
            });



            AddMessage(response, true);


            await CheckGameOverAsync();
        }
        finally
        {
            StopThinkingAnimation();
            IsWaitingForAI = false;
        }
    }



    private async Task CheckGameOverAsync()
    {
        if (RemainingAttempts <= 0)
        {
            AddMessage($"😞 انتهت المحاولات.\nالكلمة كانت: {_targetWord}", true);


            var popup = new ResultPopup(isWin: false, correctWord: _targetWord);


            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.ShowPopupAsync(popup);
            }


            await StartNewGameAsync();
        }
    }



    private async Task RequestHelpAsync()
    {
        if (IsWaitingForAI)
            return;


        if (RemainingAttempts < 5)
        {
            await Toast.Make("تحتاج إلى 5 محاولات للحصول على تلميح").Show();
            return;
        }


        bool hasInternet = await Service.HasActiveInternetAsync(4);


        if (!hasInternet)
        {
            await Toast.Make("لا يوجد إنترنت").Show();
            return;
        }


        RemainingAttempts -= 5;


        AddMessage("💡 طلبت تلميحاً (خصم 5 محاولات)", false);



        IsWaitingForAI = true;
        StartThinkingAnimation();


        try
        {
            string prompt = $@"أنت مساعد في لعبة تخمين كلمات.

الكلمة السرية:
'{_targetWord}'

المعنى العربي للكلمة (للمساعدة الداخلية فقط):
'{_targetArabic}'


مهمتك: إعطاء تلميح يساعد اللاعب بدون كشف الإجابة.


القواعد الصارمة:
- لا تذكر الكلمة السرية.
- لا تذكر الترجمة العربية.
- لا تذكر وظيفة الكلمة بشكل مباشر.
- لا تذكر العضو أو الشيء نفسه.
- لا تصف ما تفعله الكلمة أو الشيء بطريقة تجعل الإجابة واضحة.
- لا تعطِ تعريفاً قاموسياً.
- لا تستخدم مرادفات للكلمة.
- لا تستخدم أمثلة تكشف الإجابة.

طريقة إنشاء التلميح:
- اجعل التلميح عن الفئة العامة أو السياق المرتبط بالكلمة.
- اجعله يحتاج إلى استنتاج.
- اجعله متوسط الصعوبة.
- لا تجعله غامضاً جداً ولا واضحاً جداً.

مثال:
الكلمة: ear
تلميح سيئ:
'عضو في جسم الإنسان نستخدمه للسمع.'

تلميح جيد:
'يرتبط بأحد الحواس الأساسية التي تساعد الإنسان على إدراك العالم من حوله.'

أعطِ تلميحاً واحداً فقط وفي جملة قصيرة.";


            string hint = await CallGeminiApiAsync(prompt);


            AddMessage(hint, true);


            await CheckGameOverAsync();
        }
        finally
        {
            StopThinkingAnimation();
            IsWaitingForAI = false;
        }
    }
    private async Task<string> CallGeminiApiAsync(string systemPrompt, List<object>? history = null)
    {
        int maxRetries = 3;
        int delaySeconds = 2;

        string resultJson = string.Empty;
        bool isRequestSuccessful = false;


        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                string geminiApiKey = await Service.GetApiKey();


                if (string.IsNullOrEmpty(geminiApiKey))
                {
                    return "لا يوجد مفتاح متاح حالياً";
                }


                var contents = new List<object>();


                if (history != null && history.Count > 0)
                {
                    contents.AddRange(history);
                }
                else
                {
                    contents.Add(new
                    {
                        role = "user",
                        parts = new[]
                        {
                            new
                            {
                                text = systemPrompt
                            }
                        }
                    });
                }



                var requestBody = new
                {
                    systemInstruction = new
                    {
                        parts = new[]
                        {
                            new
                            {
                                text = systemPrompt
                            }
                        }
                    },

                    contents
                };



                string json = JsonSerializer.Serialize(requestBody);


                using var content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json"
                );



                var response = await _httpClient.PostAsync(
                    $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent?key={geminiApiKey}",
                    content
                );



                if (response.IsSuccessStatusCode)
                {
                    resultJson = await response.Content.ReadAsStringAsync();
                    isRequestSuccessful = true;
                    break;
                }



                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests || response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                        delaySeconds *= 2;
                        continue;
                    }
                }



                return await response.Content.ReadAsStringAsync();

            }
            catch (Exception ex)
            {
                if (attempt < maxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                    delaySeconds *= 2;
                    continue;
                }

                return $"خطأ في الاتصال: {ex.Message}";
            }
        }



        if (!isRequestSuccessful)
        {
            return "فشل الاتصال بالخادم بعد عدة محاولات.";
        }



        try
        {
            using JsonDocument doc = JsonDocument.Parse(resultJson);

            var root = doc.RootElement;


            if (root.TryGetProperty("candidates", out JsonElement candidates) &&
                candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];


                if (firstCandidate.TryGetProperty("content", out JsonElement contentElement) &&
                    contentElement.TryGetProperty("parts", out JsonElement parts) &&
                    parts.GetArrayLength() > 0)
                {
                    return parts[0]
                        .GetProperty("text")
                        .GetString()!
                        .Trim();
                }
            }
            else if (root.TryGetProperty("promptFeedback", out JsonElement feedback))
            {
                return "عذراً، لم أتمكن من الرد بسبب سياسات الأمان أو لأن السؤال غير واضح.";
            }



            return "لم يتم العثور على رد صالح من الخادم.";
        }
        catch (Exception ex)
        {
            return $"حدث خطأ أثناء قراءة الرد: {ex.Message}";
        }
    }




    private void AddMessage(string text, bool isAi)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ChatMessages.Add(new ChatMessage
            {
                Text = text,
                IsAI = isAi,
                IsUser = !isAi
            });


            ScrollToBottomRequested?.Invoke();
        });
    }




    private async Task<WordItem?> GetRandomWordFromJson()
    {
        int memorizedCount = Preferences.Get("MemorizedWords", 10);


        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("words.json");

            using var reader = new StreamReader(stream);


            string json = await reader.ReadToEndAsync();


            var words = JsonSerializer.Deserialize<List<WordItem>>(json);



            var available = words?
                .Where(x => x.Id <= memorizedCount)
                .ToList();



            if (available != null && available.Count > 0)
            {
                return available[
                    Random.Shared.Next(available.Count)
                ];
            }

        }
        catch
        {

        }



        return new WordItem
        {
            EnglishWord = "apple",
            ArabicWord = "تفاحة"
        };
    }




    public event PropertyChangedEventHandler? PropertyChanged;


    protected void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName)
        );
    }
}