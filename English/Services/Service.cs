using English.Models;
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

        private static readonly string ApiUrl = //Preferences.Get("ApiUrl", "");
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
            var request = CreateRequest("Users/TopTen/", Method.Get);

            var response = await client.ExecuteAsync(request);
            if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
                return null;

            return JsonConvert.DeserializeObject<Leader[]>(response.Content);
        }

        public static async Task<string[]> GetFriendsAsync(string userName)
        {
            var client = new RestClient(new RestClientOptions(ApiUrl));
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

        public static async Task<Leader[]> GetStudents()
        {
            var client = new RestClient(new RestClientOptions(ApiUrl));
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

            var request = CreateRequest("Users/GetGeminiKey",Method.Get);
            var response = await client.ExecuteAsync(request);

            if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
            {
                return "";
            }

            return JsonConvert.DeserializeObject<string>(response.Content) ?? "";
        }

        public static async Task<User> GetUser(User user)
        {
            var client = CreateClient();
            var request = CreateRequest("Users/GetUser/", Method.Post);
            request.AddJsonBody(user);

            var response = await client.ExecuteAsync(request);
            if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
                return new();

            return JsonConvert.DeserializeObject<User>(response.Content) ?? new();
        }        

        public static async Task<string> AddUser(User user)
        {
            var client = CreateClient();
            var request = CreateRequest("Users", Method.Post);
            request.AddJsonBody(user);

            var response = await client.ExecuteAsync(request);

            return response.StatusCode == HttpStatusCode.OK
                ? $"{response.Content}".Trim('"')
                : string.Empty;
        }

        public static async Task<string> UpdateUser(User user)
        {
            var client = CreateClient();
            var request = CreateRequest("Users", Method.Put);
            request.AddJsonBody(user);

            var response = await client.ExecuteAsync(request);
            return response.StatusCode == HttpStatusCode.OK ? "1" : string.Empty;
        }

        public static async Task<string> UpdateTimeFinalExam(TimeFinalExamModel model)
        {
            var client = CreateClient();
            var request = CreateRequest("Users/UpdateTimeFinalExam", Method.Put);
            request.AddJsonBody(model);

            var response = await client.ExecuteAsync(request);

            return response.StatusCode == HttpStatusCode.OK && response.Content != null
                ? response.Content.Trim('"')
                : string.Empty;
        }
        public static async Task<string> UpdateMemorizedWords(User student)
        {
            var client = new RestClient(new RestClientOptions(ApiUrl));
            var request = new RestRequest("Students/UpdateMemorizedWords", Method.Put);
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

        private static RestClient CreateClient()
        {
            return new RestClient(new RestClientOptions(ApiUrl));
        }

        private static RestRequest CreateRequest(string endpoint, Method method)
        {
            var request = new RestRequest(endpoint, method);
           // request.AddHeader("Authorization", $"Basic {Credentials()}");
            return request;
        }

        private static string Credentials()
        {
            var raw = "11294124:60-dayfreetrial";
            return Convert.ToBase64String(Encoding.ASCII.GetBytes(raw));
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
