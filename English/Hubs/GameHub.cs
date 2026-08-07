using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace English.Hubs;

public class GameHub
{
    private HubConnection? _hubConnection;

    // --- الأحداث الخاصة بالتحديات ---
    public event Action<string, string>? OnChallengeReceived;
    // تم تغيير الاسم هنا لتجنب التعارض مع الحدث السابق
    public event Action<string, string, string>? OnChallengeWithWordReceived;
    public event Action<string, bool, string>? OnChallengeResponseReceived;
    public event Action<string>? OnChallengeCanceled;

    // --- الأحداث الخاصة بالأصدقاء ---
    public event Action<string>? OnFriendRequestAccepted;
    public event Action<string>? OnFriendRequestReceived;

    // --- أحداث متابعة حالة الاتصال ---
    public event Action<string>? OnUserConnected;
    public event Action<string>? OnUserDisconnected;

    // --- أحداث حالة الاتصال المحلية ---
    public event Action? OnReconnecting;
    public event Action? OnReconnected;
    public event Action<Exception?>? OnClosed;

    // --- 🟢 أحداث المبارزة واللعب (Duel Events) ---
    public event Action<string, string>? OnDuelQuestionReceived;
    public event Action<string, string>? OnDuelAnswerReceived;
    public event Action<string, string, string>? OnSecretWordReceived;
    public event Action<string, string>? OnDuelWinnerReceived;

    // 🟢 إضافة حدث الانسحاب
    public event Action<string>? OnDuelWithdrawalReceived;

    public HubConnection? HubConnection => _hubConnection;

    public async Task ConnectAsync(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return;

        string url = Service.ApiUrl;

        if (_hubConnection != null)
        {
            if (_hubConnection.State == HubConnectionState.Connected)
                return;

            try
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
            }
            catch { }
            _hubConnection = null;
        }

        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{url}gamehub?username={userName}")
            .WithAutomaticReconnect()
            .Build();

        // 1. إعادة تسجيل المستخدم تلقائياً
        _hubConnection.Reconnected += async (connectionId) =>
        {
            try
            {
                await _hubConnection.InvokeAsync("RegisterUser", userName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Reconnection Registration Error: {ex.Message}");
            }

            // إعلام المشتركين أن الاتصال أعيد
            OnReconnected?.Invoke();
        };

        // إعلام المشتركين عند بدء محاولة إعادة الاتصال
        _hubConnection.Reconnecting += (ex) =>
        {
            OnReconnecting?.Invoke();
            return Task.CompletedTask;
        };

        // إعلام المشتركين عند غلق الاتصال
        _hubConnection.Closed += (ex) =>
        {
            OnClosed?.Invoke(ex);
            return Task.CompletedTask;
        };

        // 2. الاستماع لدخول/خروج المستخدمين
        _hubConnection.On<string>("UserConnected", (connectedUserName) =>
            OnUserConnected?.Invoke(connectedUserName));

        _hubConnection.On<string>("UserDisconnected", (disconnectedUserName) =>
            OnUserDisconnected?.Invoke(disconnectedUserName));

        // 3. الاستماع للتحديات والردود
        _hubConnection.On<string, string>("ReceiveChallenge", (sender, category) =>
            OnChallengeReceived?.Invoke(sender, category));

        // ربط الحدث الجديد ذو الـ 3 معاملات باسم منفصل لمنع التعارض
        _hubConnection.On<string, string, string>("ReceiveChallengeWithWord", (sender, category, word) =>
            OnChallengeWithWordReceived?.Invoke(sender, category, word));

        _hubConnection.On<string, bool, string>("ChallengeResponseReceived", (responder, isAccepted, category) =>
            OnChallengeResponseReceived?.Invoke(responder, isAccepted, category));

        _hubConnection.On<string>("ChallengeCanceled", (senderName) =>
            OnChallengeCanceled?.Invoke(senderName));

        // 4. الاستماع لطلبات الصداقة
        _hubConnection.On<string>("ReceiveFriendRequest", (senderName) =>
            OnFriendRequestReceived?.Invoke(senderName));

        _hubConnection.On<string>("FriendRequestAccepted", (acceptorName) =>
            OnFriendRequestAccepted?.Invoke(acceptorName));

        // --- 🟢 5. الاستماع لأحداث ومراسلات المبارزة داخل غرفة اللعب ---
        _hubConnection.On<string, string>("ReceiveDuelQuestion", (sender, q) =>
            OnDuelQuestionReceived?.Invoke(sender, q));

        _hubConnection.On<string, string>("ReceiveDuelAnswer", (responder, a) =>
            OnDuelAnswerReceived?.Invoke(responder, a));

        _hubConnection.On<string, string, string>("ReceiveSecretWord", (target, w, a) =>
            OnSecretWordReceived?.Invoke(target, w, a));

        _hubConnection.On<string, string>("ReceiveDuelWinner", (winner, word) =>
            OnDuelWinnerReceived?.Invoke(winner, word));

        // 🟢 الاستماع لحدث الانسحاب القادم من السيرفر
        _hubConnection.On<string>("ReceiveDuelWithdrawal", (withdrawingUser) =>
            OnDuelWithdrawalReceived?.Invoke(withdrawingUser));

        try
        {
            await _hubConnection.StartAsync();
            await _hubConnection.InvokeAsync("RegisterUser", userName);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SignalR Connection Error: {ex.Message}");
        }
    }

    // --- دوال التحديات ---

    // تم دمج الدالتين وتصحيح الخطأ البرمجي في الأقواس المتداخلة
    // ✅ الكود الصحيح لتطبيق الهاتف (Client)
    public async Task SendChallengeAsync(string targetUser, string category, string word = "")
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            string safeWord = string.IsNullOrEmpty(word) ? "" : word;

            await _hubConnection.InvokeAsync("SendChallenge", targetUser, category, safeWord);
        }
    }

    public async Task JoinDuelRoomAsync(string roomName)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("JoinDuelRoom", roomName);
    }

    public async Task SendResponseAsync(string targetUser, bool isAccepted, string category)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("RespondToChallenge", targetUser, isAccepted, category);
    }

    public async Task CancelChallengeAsync(string targetUser)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("CancelChallenge", targetUser);
    }

    // --- دوال الصداقة ---
    public async Task SendFriendRequestAsync(string targetUser)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("SendFriendRequest", targetUser);
    }

    public async Task AcceptFriendRequestAsync(string senderName)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("AcceptFriendRequest", senderName);
    }

    public async Task<List<string>> GetPendingFriendRequestsAsync()
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            try { return await _hubConnection.InvokeAsync<List<string>>("GetPendingFriendRequests"); }
            catch { return new List<string>(); }
        }
        return new List<string>();
    }

    public async Task<List<string>> GetOnlineUsersAsync()
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            try { return await _hubConnection.InvokeAsync<List<string>>("GetOnlineUsers"); }
            catch { return new List<string>(); }
        }
        return new List<string>();
    }

    public async Task<List<string>> GetFriendsAsync()
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            try { return await _hubConnection.InvokeAsync<List<string>>("GetFriends"); }
            catch { return new List<string>(); }
        }
        return new List<string>();
    }

    public async Task<List<string>> GetSentPendingRequestsAsync()
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            try { return await _hubConnection.InvokeAsync<List<string>>("GetSentPendingRequests"); }
            catch { return new List<string>(); }
        }
        return new List<string>();
    }

    public async Task DisconnectAsync()
    {
        if (_hubConnection != null)
            await _hubConnection.StopAsync();
    }

    public async Task<DateTime?> GetLastSeenAsync(string targetUser)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            try { return await _hubConnection.InvokeAsync<DateTime?>("GetLastSeen", targetUser); }
            catch { return null; }
        }
        return null;
    }
}