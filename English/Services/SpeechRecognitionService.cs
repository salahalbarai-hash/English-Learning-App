using CommunityToolkit.Maui.Media;
using System.Globalization;

namespace English.Services;

public class SpeechRecognitionService
{
    private static CancellationTokenSource? _cts;
    private static System.Timers.Timer? _safetyTimer;

    public static async Task<(bool Success, string Text, string ErrorMessage)> ListenAndEvaluateAsync(
        string targetEnglishWord, 
        Action<bool>? onListeningStateChanged = null)
    {
        try
        {
            var isGranted = await SpeechToText.Default.RequestPermissions(CancellationToken.None);
            if (!isGranted)
            {
                return (false, "", "الرجاء منح إذن الميكروفون للتطبيق لاستخدامه في تقييم النطق.");
            }

            CancelListening();

            _cts = new CancellationTokenSource();

            // مؤقت أمان ينفذ الإلغاء حصراً على الـ MainThread لمنع كراش أندرويد
            StopSafetyTimer();
            _safetyTimer = new System.Timers.Timer(9000) { AutoReset = false };
            _safetyTimer.Elapsed += (s, e) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        if (_cts != null && !_cts.IsCancellationRequested)
                        {
                            _cts.Cancel();
                        }
                    }
                    catch { }
                });
            };
            _safetyTimer.Start();

            onListeningStateChanged?.Invoke(true);

            SpeechToTextResult? result = null;

            // إجبار تشغيل واستدعاء SpeechRecognizer على MainThread لأن أندرويد يمنع تشغيله وإلغاءه خارج الـ Main Looper
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    var culture = CultureInfo.GetCultureInfo("en-US");
                    result = await SpeechToText.Default.ListenAsync(culture, new Progress<string>(), _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // انتهاء المهلة أو الإلغاء بشكل طبيعي
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SpeechRecognition] Listen error: {ex.Message}");
                }
            });

            // إيقاف مؤقت الأمان فور انتهاء الاستماع بنجاح حتى لا يعمل بالخلفية مطلقاً
            StopSafetyTimer();

            onListeningStateChanged?.Invoke(false);

            if (result == null || !result.IsSuccessful)
            {
                string err = result?.Exception?.Message ?? "";
                if (string.IsNullOrWhiteSpace(err) || err.Contains("cancel", StringComparison.OrdinalIgnoreCase))
                {
                    return (false, "", "لم يتم التقاط أي صوت، حاول مرة ثانية.");
                }
                return (false, "", "لم نتمكن من سماع صوتك بوضوح، حاول ثانية.");
            }

            string spokenText = result.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(spokenText))
            {
                return (false, "", "لم يتم التقاط أي صوت.");
            }

            bool isMatch = IsPronunciationMatch(spokenText, targetEnglishWord);
            return (isMatch, spokenText, "");
        }
        catch (OperationCanceledException)
        {
            StopSafetyTimer();
            onListeningStateChanged?.Invoke(false);
            return (false, "", "انتهى وقت الاستماع دون التقاط صوت.");
        }
        catch (Exception ex)
        {
            StopSafetyTimer();
            onListeningStateChanged?.Invoke(false);
            return (false, "", $"تعذر بدء الاستماع: {ex.Message}");
        }
        finally
        {
            StopSafetyTimer();
            onListeningStateChanged?.Invoke(false);
            CleanupCts();
        }
    }

    private static void StopSafetyTimer()
    {
        try
        {
            _safetyTimer?.Stop();
            _safetyTimer?.Dispose();
            _safetyTimer = null;
        }
        catch { }
    }

    private static void CleanupCts()
    {
        try
        {
            _cts?.Dispose();
        }
        catch { }
        finally
        {
            _cts = null;
        }
    }

    public static void CancelListening()
    {
        StopSafetyTimer();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                }
            }
            catch { }
            finally
            {
                CleanupCts();
            }
        });
    }

    private static bool IsPronunciationMatch(string spoken, string target)
    {
        if (string.IsNullOrWhiteSpace(spoken) || string.IsNullOrWhiteSpace(target))
            return false;

        string cleanSpoken = CleanWord(spoken);
        string cleanTarget = CleanWord(target);

        // 1. تطابق تام
        if (string.Equals(cleanSpoken, cleanTarget, StringComparison.OrdinalIgnoreCase))
            return true;

        // 2. إذا نطق المستخدم جملة تحتوي على الكلمة
        var words = cleanSpoken.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(w => string.Equals(w, cleanTarget, StringComparison.OrdinalIgnoreCase)))
            return true;

        // 3. معالجة الكلمات المتطابقة صوتياً (Homophones الشائعة)
        if (AreHomophones(cleanSpoken, cleanTarget))
            return true;

        // 4. تشابه تقريبي بحذف المسافات
        if (cleanSpoken.Replace(" ", "").Equals(cleanTarget.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static string CleanWord(string input)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in input)
        {
            if (char.IsLetterOrDigit(c) || c == ' ')
                sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString().Trim();
    }

    private static bool AreHomophones(string spoken, string target)
    {
        (string, string)[] homophones =
        [
            ("steal", "steel"),
            ("right", "write"),
            ("hear", "here"),
            ("sea", "see"),
            ("sun", "son"),
            ("meet", "meat"),
            ("buy", "by"),
            ("bye", "by"),
            ("for", "four"),
            ("to", "two"),
            ("too", "to"),
            ("new", "knew"),
            ("no", "know"),
            ("one", "won"),
            ("hour", "our"),
            ("flower", "flour"),
            ("wait", "weight"),
            ("piece", "peace"),
            ("break", "brake")
        ];

        foreach (var (w1, w2) in homophones)
        {
            if ((spoken.Contains(w1) && target.Equals(w2, StringComparison.OrdinalIgnoreCase)) ||
                (spoken.Contains(w2) && target.Equals(w1, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}
