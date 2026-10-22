using System.Collections.Concurrent;
using System.Text.Json;
using English.Models;

namespace English.Services;

public class ChatSummary
{
    public string? LastMessage { get; set; }
    public bool IsMine { get; set; }
    public MessageStatus Status { get; set; }
    public DateTime? LastMessageTime { get; set; }
    public int UnreadCount { get; set; }
}

public static class ChatStorageService
{
    private static readonly ConcurrentDictionary<string, ChatSummary> _summaryCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new(StringComparer.OrdinalIgnoreCase);

    public static event Action<string, int, string>? OnGlobalMessageReceived;
    public static event Action<string>? OnChatSummaryChanged;

    private static SemaphoreSlim GetLock(string friendName)
    {
        return _fileLocks.GetOrAdd(friendName, _ => new SemaphoreSlim(1, 1));
    }

    public static string GetFilePath(string friendName)
    {
        return Path.Combine(FileSystem.AppDataDirectory, $"chat_{friendName}.json");
    }

    public static List<int> GetBlacklist()
    {
        try
        {
            string json = Preferences.Get("DeletedForMe_List", "[]");
            return JsonSerializer.Deserialize<List<int>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void AddToBlacklist(IEnumerable<int> ids)
    {
        try
        {
            var list = GetBlacklist();
            bool changed = false;
            foreach (var id in ids)
            {
                if (id > 0 && !list.Contains(id))
                {
                    list.Add(id);
                    changed = true;
                }
            }
            if (changed)
            {
                Preferences.Set("DeletedForMe_List", JsonSerializer.Serialize(list));
            }
        }
        catch { }
    }

    public static ChatSummary GetChatSummary(string friendName)
    {
        if (string.IsNullOrWhiteSpace(friendName))
            return new ChatSummary();

        if (_summaryCache.TryGetValue(friendName, out var cached))
            return cached;

        var summary = LoadSummaryFromFile(friendName);
        _summaryCache[friendName] = summary;
        return summary;
    }

    private static ChatSummary LoadSummaryFromFile(string friendName)
    {
        var summary = new ChatSummary();
        string path = GetFilePath(friendName);
        if (!File.Exists(path))
            return summary;

        try
        {
            string json = File.ReadAllText(path);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var blacklist = GetBlacklist();
                var all = JsonSerializer.Deserialize<List<ChatBubbleModel>>(json) ?? [];
                var valid = all.Where(m => !blacklist.Contains(m.Id)).ToList();

                var last = valid.LastOrDefault();
                if (last != null)
                {
                    summary.LastMessage = last.Content;
                    summary.IsMine = last.IsMine;
                    summary.Status = last.Status;
                    summary.LastMessageTime = last.Timestamp;
                }

                summary.UnreadCount = valid.Count(m => !m.IsMine && m.Status != MessageStatus.Read);
            }
        }
        catch { }

        return summary;
    }

    public static async Task<List<ChatBubbleModel>> LoadMessagesAsync(string friendName)
    {
        var sem = GetLock(friendName);
        await sem.WaitAsync();
        try
        {
            string path = GetFilePath(friendName);
            if (!File.Exists(path))
                return [];

            string json = await File.ReadAllTextAsync(path);
            if (string.IsNullOrWhiteSpace(json))
                return [];

            var blacklist = GetBlacklist();
            var all = JsonSerializer.Deserialize<List<ChatBubbleModel>>(json) ?? [];
            var valid = all.Where(m => !blacklist.Contains(m.Id)).ToList();

            UpdateSummaryCache(friendName, valid);
            return valid;
        }
        catch
        {
            return [];
        }
        finally
        {
            sem.Release();
        }
    }

    public static async Task SaveMessagesAsync(string friendName, List<ChatBubbleModel> messages)
    {
        var sem = GetLock(friendName);
        await sem.WaitAsync();
        try
        {
            var blacklist = GetBlacklist();
            var valid = messages.Where(m => !blacklist.Contains(m.Id)).ToList();

            string path = GetFilePath(friendName);
            string json = JsonSerializer.Serialize(valid);
            await File.WriteAllTextAsync(path, json);

            UpdateSummaryCache(friendName, valid);
            OnChatSummaryChanged?.Invoke(friendName);
        }
        catch { }
        finally
        {
            sem.Release();
        }
    }

    public static async Task AppendIncomingMessageAsync(string sender, int messageId, string content)
    {
        if (string.IsNullOrWhiteSpace(sender)) return;

        var sem = GetLock(sender);
        await sem.WaitAsync();
        try
        {
            var blacklist = GetBlacklist();
            if (blacklist.Contains(messageId)) return;

            string path = GetFilePath(sender);
            List<ChatBubbleModel> list = [];
            if (File.Exists(path))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(path);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        list = JsonSerializer.Deserialize<List<ChatBubbleModel>>(json) ?? [];
                    }
                }
                catch { }
            }

            // منع تكرار نفس الرسالة
            if (messageId > 0 && list.Any(m => m.Id == messageId))
                return;

            var newMsg = new ChatBubbleModel
            {
                Id = messageId,
                Content = content,
                Timestamp = DateTime.Now,
                IsMine = false,
                Status = MessageStatus.Delivered // مستلمة ولكن لم تقرأ بعد
            };

            list.Add(newMsg);

            var valid = list.Where(m => !blacklist.Contains(m.Id)).ToList();
            string saveJson = JsonSerializer.Serialize(valid);
            await File.WriteAllTextAsync(path, saveJson);

            UpdateSummaryCache(sender, valid);
        }
        catch { }
        finally
        {
            sem.Release();
        }

        OnGlobalMessageReceived?.Invoke(sender, messageId, content);
        OnChatSummaryChanged?.Invoke(sender);
    }

    public static async Task MarkMessagesAsReadAsync(string friendName)
    {
        if (string.IsNullOrWhiteSpace(friendName)) return;

        var sem = GetLock(friendName);
        await sem.WaitAsync();
        try
        {
            string path = GetFilePath(friendName);
            if (!File.Exists(path)) return;

            string json = await File.ReadAllTextAsync(path);
            if (string.IsNullOrWhiteSpace(json)) return;

            var list = JsonSerializer.Deserialize<List<ChatBubbleModel>>(json) ?? [];
            bool changed = false;

            foreach (var m in list)
            {
                if (!m.IsMine && m.Status != MessageStatus.Read)
                {
                    m.Status = MessageStatus.Read;
                    changed = true;
                }
            }

            if (changed)
            {
                string saveJson = JsonSerializer.Serialize(list);
                await File.WriteAllTextAsync(path, saveJson);
                UpdateSummaryCache(friendName, list);
            }
        }
        catch { }
        finally
        {
            sem.Release();
        }

        OnChatSummaryChanged?.Invoke(friendName);
    }

    public static async Task MarkMySentMessagesAsReadAsync(string friendName)
    {
        if (string.IsNullOrWhiteSpace(friendName)) return;

        var sem = GetLock(friendName);
        await sem.WaitAsync();
        try
        {
            string path = GetFilePath(friendName);
            if (!File.Exists(path)) return;

            string json = await File.ReadAllTextAsync(path);
            if (string.IsNullOrWhiteSpace(json)) return;

            var list = JsonSerializer.Deserialize<List<ChatBubbleModel>>(json) ?? [];
            bool changed = false;

            foreach (var m in list)
            {
                if (m.IsMine && m.Status != MessageStatus.Read)
                {
                    m.Status = MessageStatus.Read;
                    changed = true;
                }
            }

            if (changed)
            {
                string saveJson = JsonSerializer.Serialize(list);
                await File.WriteAllTextAsync(path, saveJson);
                UpdateSummaryCache(friendName, list);
            }
        }
        catch { }
        finally
        {
            sem.Release();
        }

        OnChatSummaryChanged?.Invoke(friendName);
    }

    public static async Task RemoveMessageByIdAsync(int messageId)
    {
        if (messageId <= 0) return;

        AddToBlacklist([messageId]);

        // نقوم بمسحها من أي كاش موجود
        foreach (var key in _summaryCache.Keys.ToList())
        {
            _summaryCache.TryRemove(key, out _);
            OnChatSummaryChanged?.Invoke(key);
        }

        await Task.CompletedTask;
    }

    private static void UpdateSummaryCache(string friendName, List<ChatBubbleModel> valid)
    {
        var summary = new ChatSummary();
        var last = valid.LastOrDefault();
        if (last != null)
        {
            summary.LastMessage = last.Content;
            summary.IsMine = last.IsMine;
            summary.Status = last.Status;
            summary.LastMessageTime = last.Timestamp;
        }
        summary.UnreadCount = valid.Count(m => !m.IsMine && m.Status != MessageStatus.Read);
        _summaryCache[friendName] = summary;
    }
}
