using System.Text.RegularExpressions;
using English.Models;

namespace English.Services;

public class TikTokLiveService
{
    private static TikTokLiveService? _instance;
    public static TikTokLiveService Instance => _instance ??= new TikTokLiveService();

    public event Action<TikTokLiveComment>? OnCommentReceived;
    public event Action<string>? OnStatusChanged;
    public event Action<bool>? OnConnectionStateChanged;

    private readonly HttpClient _httpClient;
    private CancellationTokenSource? _cts;
    private bool _isConnected;
    private bool _isSimulation;
    private string _currentHost = "";
    private string _roomId = "";

    public bool IsConnected => _isConnected;
    public bool IsSimulation => _isSimulation;
    public string CurrentHost => _currentHost;

    private static readonly string[] SimulatedUsers =
    [
        "fahad_sa", "nour_english", "sultan_88", "layla_uk", "omar_vip",
        "sarah_talks", "khalid_riyadh", "maryam_99", "hamad_star", "reem_learn",
        "yousef_dxb", "huda_voice", "tariq_challenge", "mona_en", "ali_legend"
    ];

    public TikTokLiveService()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        };
        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<(bool isLive, string roomId, string message)> CheckLiveStatusAsync(string hostUsername)
    {
        string host = hostUsername.Trim().TrimStart('@');
        if (string.IsNullOrWhiteSpace(host))
        {
            return (false, "", "يرجى إدخال اسم حساب تيك توك صحيح");
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            string url = $"https://www.tiktok.com/@{host}/live";
            var response = await _httpClient.GetAsync(url, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                return (false, "", $"الحساب @{host} غير موجود أو البث مغلق");
            }

            string html = await response.Content.ReadAsStringAsync(cts.Token);

            // فحص حالة الغرفة (في تيك توك status 4 تعني أن البث مغلق offline)
            bool isOffline = html.Contains("\"status\":4") || html.Contains("\"liveRoomStatus\":4");

            var match = Regex.Match(html, @"(?:room_id|roomId)""?\s*[:=]\s*""?(\d{10,25})""?");
            if (match.Success && !isOffline)
            {
                string rId = match.Groups[1].Value;
                return (true, rId, $"تم اكتشاف البث المباشر (غرفة: {rId})");
            }

            return (false, "", $"لا يوجد بث مباشر نشط حالياً على حساب @{host}");
        }
        catch (Exception ex)
        {
            return (false, "", $"تعذر التحقق من البث: {ex.Message}");
        }
    }

    public async Task<bool> ConnectAsync(string hostUsername, bool isSimulation = false)
    {
        await DisconnectAsync();

        _currentHost = hostUsername.Trim().TrimStart('@');
        _isSimulation = isSimulation;
        _cts = new CancellationTokenSource();

        if (string.IsNullOrWhiteSpace(_currentHost) && !_isSimulation)
        {
            OnStatusChanged?.Invoke("يرجى إدخال اسم حساب تيك توك صحيح");
            return false;
        }

        if (_isSimulation)
        {
            _isConnected = true;
            OnStatusChanged?.Invoke("🎮 وضع المحاكاة والتجربة نشط");
            OnConnectionStateChanged?.Invoke(true);
            _ = RunSimulationLoopAsync(_cts.Token);
            return true;
        }

        OnStatusChanged?.Invoke($"جاري فحص حالة البث لـ @{_currentHost}...");

        var (isLive, roomId, msg) = await CheckLiveStatusAsync(_currentHost);
        if (isLive && !string.IsNullOrEmpty(roomId))
        {
            _roomId = roomId;
            _isConnected = true;
            OnStatusChanged?.Invoke($"🟢 متصل ببث @{_currentHost}");
            OnConnectionStateChanged?.Invoke(true);
            _ = RunWebcastPollingLoopAsync(_cts.Token);
            return true;
        }
        else
        {
            _isConnected = false;
            OnStatusChanged?.Invoke(msg);
            OnConnectionStateChanged?.Invoke(false);
            return false;
        }
    }

    public Task DisconnectAsync()
    {
        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
        catch { }

        _isConnected = false;
        _roomId = "";
        OnConnectionStateChanged?.Invoke(false);
        OnStatusChanged?.Invoke("تم قطع الاتصال بالبث");
        return Task.CompletedTask;
    }

    /// <summary>
    /// استقبال تعليق محلياً أو يدوياً (مثلاً من تحكم الباث أو المودريتور)
    /// </summary>
    public void DispatchComment(string userName, string text)
    {
        int? choice = ParseChoiceFromComment(text);
        var comment = new TikTokLiveComment
        {
            UserName = userName.StartsWith("@") ? userName : $"@{userName}",
            DisplayName = userName.TrimStart('@'),
            CommentText = text,
            Timestamp = DateTime.Now,
            ChoiceIndex = choice
        };

        OnCommentReceived?.Invoke(comment);
    }

    public static int? ParseChoiceFromComment(string comment)
    {
        if (string.IsNullOrWhiteSpace(comment)) return null;

        string trimmed = comment.Trim();

        // مطابقة الأرقام الإنجليزية والعربية
        if (trimmed == "1" || trimmed == "١" || trimmed.Contains("الخيار 1") || trimmed.Contains("الأول") || trimmed.StartsWith("#1")) return 0;
        if (trimmed == "2" || trimmed == "٢" || trimmed.Contains("الخيار 2") || trimmed.Contains("الثاني") || trimmed.StartsWith("#2")) return 1;
        if (trimmed == "3" || trimmed == "٣" || trimmed.Contains("الخيار 3") || trimmed.Contains("الثالث") || trimmed.StartsWith("#3")) return 2;
        if (trimmed == "4" || trimmed == "٤" || trimmed.Contains("الخيار 4") || trimmed.Contains("الرابع") || trimmed.StartsWith("#4")) return 3;

        // مطابقة سريعة إذا بدأ برقم
        char first = trimmed[0];
        if (first == '1' || first == '١') return 0;
        if (first == '2' || first == '٢') return 1;
        if (first == '3' || first == '٣') return 2;
        if (first == '4' || first == '٤') return 3;

        return null;
    }

    private async Task RunSimulationLoopAsync(CancellationToken token)
    {
        var random = new Random();

        while (!token.IsCancellationRequested && _isConnected)
        {
            try
            {
                // إرسال تعليقات محاكاة بين 400ms و 1500ms
                await Task.Delay(random.Next(400, 1500), token);

                string randomUser = SimulatedUsers[random.Next(SimulatedUsers.Length)];
                // في 70% من الحالات يرسل رقماً صحيحاً أو عشوائياً بين 1 و 4
                int choiceNum = random.Next(1, 5);
                string text = choiceNum.ToString();

                DispatchComment(randomUser, text);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch { }
        }
    }

    private async Task RunWebcastPollingLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _isConnected)
        {
            try
            {
                await Task.Delay(3000, token);

                // فحص دوري إذا لم يكن لدينا Room ID
                if (string.IsNullOrEmpty(_roomId) && !string.IsNullOrEmpty(_currentHost))
                {
                    string url = $"https://www.tiktok.com/@{_currentHost}/live";
                    var res = await _httpClient.GetAsync(url, token);
                    if (res.IsSuccessStatusCode)
                    {
                        string html = await res.Content.ReadAsStringAsync(token);
                        var match = Regex.Match(html, @"(?:room_id|roomId)""?\s*[:=]\s*""?(\d+)""?");
                        if (match.Success)
                        {
                            _roomId = match.Groups[1].Value;
                            OnStatusChanged?.Invoke($"🟢 تم اكتشاف البث المباشر (غرفة: {_roomId})");
                        }
                    }
                }
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch { }
        }
    }
}
