using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
namespace English.Services
{
    public static class Service
    {
        private static int _adCounter = 0;
        public static bool IsAdShowing { get; private set; }

        public static string ApiUrl =>
#if DEBUG
            "http://192.168.8.139:5005/";
#else
            Preferences.Get("ApiUrl", "");
#endif

        // ========================
        // Users
        // ========================

        public static async Task<Leader[]?> GetTopTen()
        {
            var client = CreateClient();
            var request = new RestRequest("Users/TopTen/", Method.Get);

            var response = await client.ExecuteAsync(request);
            if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
                return null;

            return JsonConvert.DeserializeObject<Leader[]>(response.Content);
        }

        public static async Task<ApiResult<User>> GetUserByUserName(string userName)
        {
            var client = CreateClient();
            var request = new RestRequest("Users/GetUserByUserName", Method.Get);
            request.AddQueryParameter("userName", userName);

            var response = await client.ExecuteAsync(request);

            if (string.IsNullOrEmpty(response.Content))
            {
                return new ApiResult<User> { Success = false, Message = "تعذر الاتصال بالسيرفر" };
            }

            var result = JsonConvert.DeserializeObject<ApiResult<User>>(response.Content);

            return result ?? new ApiResult<User>
            {
                Success = false,
                Message = "حدث خطأ غير معروف"
            };
        }

        public static async Task<string[]> GetFriendsAsync(string userName)
        {
            var client = CreateClient();
            var request = new RestRequest("Users/GetFriends", Method.Get);

            // تمرير اسم المستخدم الحالي كـ Query Parameter للـ API
            request.AddQueryParameter("userName", userName);

            try
            {
                var response = await client.ExecuteAsync(request);
                var content = response.Content;

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(content))
                    return [];

                // إلغاء التسلسل إلى مصفوفة نصوص تمثل أسماء الأصدقاء
                var result = JsonConvert.DeserializeObject<string[]>(content);
                return result ?? [];
            }
            catch
            {
                return [];
            }
        }

        public static async Task<User[]?> GetFriendsWithIdsAsync(string userName)
        {
            var client = CreateClient();
            var request = new RestRequest("Users/GetFriendsWithIds", Method.Get);
            request.AddQueryParameter("userName", userName);

            try
            {
                var response = await client.ExecuteAsync(request);
                var content = response.Content;

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(content))
                    return null;

                var result = JsonConvert.DeserializeObject<User[]>(content);
                return result ?? null;
            }
            catch
            {
                return null;
            }
        }

        public static async Task<Leader[]> GetStudents()
        {
            var client = CreateClient();
            var request = new RestRequest("Students", Method.Get);
            try
            {
                var response = await client.ExecuteAsync(request);
                var content = response.Content;
                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(content))
                    return [];
                var result = JsonConvert.DeserializeObject<Leader[]>(content);
                return result ?? [];
            }
            catch
            {
                return [];
            }
        }

        public static async Task<string> GetApiKey()
        {
            var client = new RestClient(new RestClientOptions(ApiUrl));
            var request = new RestRequest("Users/GetGeminiKey", Method.Get);
            var response = await client.ExecuteAsync(request);

            if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
            {
                return "";
            }

            return JsonConvert.DeserializeObject<string>(response.Content) ?? "";
        }

        public static async Task<string> AskAIAsync(string prompt)
        {
            try
            {
                string apiKey = await GetApiKey();
                if (string.IsNullOrEmpty(apiKey))
                    return string.Empty;

                var client = new RestClient();
                var request = new RestRequest($"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent?key={apiKey}", Method.Post);
                request.AddHeader("Content-Type", "application/json");

                var body = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    }
                };

                request.AddJsonBody(body);

                var response = await client.ExecuteAsync(request);
                if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
                    return string.Empty;

                var json = JObject.Parse(response.Content);
                var text = json["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

                return text?.Trim() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static async Task<ApiResult<User>> GetUser(User user)
        {
            var client = CreateClient();
            var request = new RestRequest("Users/GetUser/", Method.Post);
            request.AddJsonBody(user);

            var response = await client.ExecuteAsync(request);

            if (string.IsNullOrEmpty(response.Content))
            {
                return new ApiResult<User> { Success = false, Message = "تعذر الاتصال بالسيرفر" };
            }

            var result = JsonConvert.DeserializeObject<ApiResult<User>>(response.Content);

            return result ?? new ApiResult<User>
            {
                Success = false,
                Message = "حدث خطأ غير معروف"
            };
        }

        public static async Task<ApiResult<User>> GetUserById(long id)
        {
            var client = CreateClient();
            var request = new RestRequest("Users/GetUserById", Method.Get);
            request.AddQueryParameter("id", id.ToString());

            var response = await client.ExecuteAsync(request);

            if (string.IsNullOrEmpty(response.Content))
            {
                return new ApiResult<User> { Success = false, Message = "تعذر الاتصال بالسيرفر" };
            }

            var result = JsonConvert.DeserializeObject<ApiResult<User>>(response.Content);

            return result ?? new ApiResult<User>
            {
                Success = false,
                Message = "حدث خطأ غير معروف"
            };
        }

        public static async Task<string> AddUser(User user)
        {
            var client = new RestClient(new RestClientOptions(ApiUrl));
            var request = new RestRequest("Users", Method.Post);
            request.AddJsonBody(user);

            var response = await client.ExecuteAsync(request);

            return response.StatusCode == HttpStatusCode.OK
                ? $"{response.Content}".Trim('"')
                : string.Empty;
        }

        public static async Task<string> UpdateUser(User user)
        {
            var client = new RestClient(new RestClientOptions(ApiUrl));
            var request = new RestRequest("Users", Method.Put);
            request.AddJsonBody(user);

            var response = await client.ExecuteAsync(request);
            return response.StatusCode == HttpStatusCode.OK ? "1" : string.Empty;
        }

        public static async Task<string> UpdateCoins(User user)
        {
            var client = new RestClient(new RestClientOptions(ApiUrl));
            var request = new RestRequest("Users/UpdateCoins", Method.Put);
            request.AddJsonBody(user);

            var response = await client.ExecuteAsync(request);
            return response.StatusCode == HttpStatusCode.OK && response.Content != null
                ? response.Content.Trim('"')
                : string.Empty;
        }

        public static async Task<string> UpdateTimeFinalExam(TimeFinalExamModel model)
        {
            var client = new RestClient(new RestClientOptions(ApiUrl));
            var request = new RestRequest("Users/UpdateTimeFinalExam", Method.Put);
            request.AddJsonBody(model);

            var response = await client.ExecuteAsync(request);

            return response.StatusCode == HttpStatusCode.OK && response.Content != null
                ? response.Content.Trim('"')
                : string.Empty;
        }
        public static async Task<string> UpdateMemorizedWords(User student)
        {
            var client = CreateClient();
            var request = new RestRequest("Students/UpdateMemorizedWords", Method.Put);
            request.AddJsonBody(student);

            var response = await client.ExecuteAsync(request);

            return response.StatusCode == HttpStatusCode.OK && response.Content != null
                ? response.Content.Trim('"')
                : string.Empty;
        }

        public static async Task<string> UpdateFriendsChallengeCount(User student)
        {
            var client = CreateClient();
            var request = new RestRequest("Students/UpdateFriendsChallengeCount", Method.Put);
            request.AddJsonBody(student);

            var response = await client.ExecuteAsync(request);

            return response.StatusCode == HttpStatusCode.OK && response.Content != null
                ? response.Content.Trim('"')
                : string.Empty;
        }

        // ========================
        // Internet
        // ========================

        public static async Task<bool> HasActiveInternetAsync(int seconds)
        {
            try
            {
                using var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(seconds)
                };
                var response = await client.GetAsync("https://www.google.com/generate_204");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // ========================
        // Lock system
        // ========================

        public static async Task<bool> IsLock(string key)
        {
            var json = await ReadLockFile();
            return json[key]?.Value<bool>() ?? true;
        }

        public static async Task UnLock(string key)
        {
            var json = await ReadLockFile();
            json[key] = false;
            await WriteLockFile(json);
        }

        // ========================
        // Ads
        // ========================

        public static async Task ShowAd(Action action, ActivityIndicator indicator)
        {
            if (IsAdShowing)
                return;

            IsAdShowing = true;
            _adCounter++;

            //if (_adCounter % 2 == 0)
            //    await InterstitialAd.ShowInterstitialAd(action, indicator);
            //else
            //    await RewardedAd.ShowRewardedlAd(action, indicator);

            IsAdShowing = false;
        }

        // ========================
        // Helpers
        // ========================

        private static RestClient? _restClient;
        private static string? _lastApiUrl;

        private static RestClient CreateClient()
        {
            var currentUrl = ApiUrl;
            if (_restClient == null || _lastApiUrl != currentUrl)
            {
                _restClient?.Dispose();
                _restClient = new RestClient(new RestClientOptions(currentUrl));
                _lastApiUrl = currentUrl;
            }
            return _restClient;
        }

        // The CreateRequest helper was removed. Use `new RestRequest(endpoint, method)` directly.

        // Pending coins updates storage key
        private const string PendingCoinsKey = "Pending_Coins_Updates";

        private class PendingCoinsEntry
        {
            public long ID { get; set; }
            public long Coins { get; set; }
        }

        public static void AddPendingCoinsUpdate(long id, long coins)
        {
            try
            {
                string json = Preferences.Get(PendingCoinsKey, "[]");
                var list = System.Text.Json.JsonSerializer.Deserialize<List<PendingCoinsEntry>>(json) ?? new List<PendingCoinsEntry>();
                list.Add(new PendingCoinsEntry { ID = id, Coins = coins });
                Preferences.Set(PendingCoinsKey, System.Text.Json.JsonSerializer.Serialize(list));
            }
            catch { }
        }

        public static async Task TrySyncPendingCoinsAsync()
        {
            try
            {
                if (!await HasActiveInternetAsync(5)) return;

                string json = Preferences.Get(PendingCoinsKey, "[]");
                var list = System.Text.Json.JsonSerializer.Deserialize<List<PendingCoinsEntry>>(json) ?? new List<PendingCoinsEntry>();
                if (list.Count == 0) return;

                var remaining = new List<PendingCoinsEntry>();

                foreach (var entry in list)
                {
                    try
                    {
                        var res = await UpdateCoins(new User { ID = entry.ID, Coins = entry.Coins });
                        if (res != "1")
                        {
                            remaining.Add(entry);
                        }
                    }
                    catch
                    {
                        remaining.Add(entry);
                    }
                }

                Preferences.Set(PendingCoinsKey, System.Text.Json.JsonSerializer.Serialize(remaining));
            }
            catch { }
        }

        public static async Task<int> FetchFriendMemorizedWordsCountAsync(string friendUserName)
        {
            if (string.IsNullOrWhiteSpace(friendUserName)) return 0;

            if (Shell.Current is AppShell appShell && appShell.GameHub != null)
            {
                try
                {
                    int count = await appShell.GameHub.GetMemorizedWordsCountAsync(friendUserName);
                    if (count > 0) return count;
                }
                catch { }
            }

            try
            {
                var students = await GetStudents();
                var friend = students?.FirstOrDefault(s => string.Equals(s.UserName, friendUserName, StringComparison.OrdinalIgnoreCase));
                if (friend != null && friend.MemorizedWords > 0)
                {
                    return friend.MemorizedWords;
                }
            }
            catch { }

            return 0;
        }

        private static async Task<JObject> ReadLockFile()
        {
            var path = Path.Combine(FileSystem.AppDataDirectory, "LockFile.json");

            if (!File.Exists(path))
                await File.WriteAllTextAsync(path, "{}");

            var text = await File.ReadAllTextAsync(path);
            return JObject.Parse(text);
        }

        private static async Task WriteLockFile(JObject json)
        {
            var path = Path.Combine(FileSystem.AppDataDirectory, "LockFile.json");
            await File.WriteAllTextAsync(path, json.ToString());
        }

        public static async Task<ImageSource> LoadImageFromServerAsync(string url)
        {
            using var client = new HttpClient();

            // إضافة Authentication Header
            var credentials = "11294124:60-dayfreetrial";
            var base64Credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(credentials));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);

            // جلب الصورة كـ Byte Array
            var imageBytes = await client.GetByteArrayAsync(url);

            // تحويل البايتات إلى ImageSource
            return ImageSource.FromStream(() => new MemoryStream(imageBytes));
        }
    }
}
