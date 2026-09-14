using System.Collections.Concurrent;
using System.Data;

namespace FoundationalCSharp.Server.Hubs;

public class GameHub : Hub
{
    private static readonly ConcurrentDictionary<string, string> _connectedUsers = new(StringComparer.OrdinalIgnoreCase);

    public override async Task OnConnectedAsync()
    {
        string userName = Context.GetHttpContext()?.Request.Query["username"]!;

        if (!string.IsNullOrEmpty(userName))
        {
            _connectedUsers[userName] = Context.ConnectionId;
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userName = _connectedUsers.FirstOrDefault(x => x.Value == Context.ConnectionId);
        if (!string.IsNullOrEmpty(userName.Key))
        {
            _connectedUsers.TryRemove(userName.Key, out _);

            // 🟢 حفظ وقت آخر ظهور في قاعدة البيانات عند انقطاع الاتصال
            try
            {
                DB.Exec($"UPDATE UsersTbl SET LastSeen = GETDATE() WHERE Username = N'{userName.Key}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database Error on LastSeen Update: {ex.Message}");
            }

            // إبلاغ جميع المستخدمين الآخرين بأن هذا الشخص قطع الاتصال 🔴
            await Clients.Others.SendAsync("UserDisconnected", userName.Key);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // 1. جلب قائمة المستخدمين المتصلين
    public List<string> GetOnlineUsers()
    {
        string currentUser = _connectedUsers.FirstOrDefault(x => x.Value == Context.ConnectionId).Key ?? "";
        return [.. _connectedUsers.Keys.Where(user => !string.Equals(user, currentUser, StringComparison.OrdinalIgnoreCase))];
    }

    // 2. إرسال طلب الصداقة
    public async Task SendFriendRequest(string targetUser)
    {
        string senderUser = _connectedUsers.FirstOrDefault(x => x.Value == Context.ConnectionId).Key ?? "مستخدم";

        try
        {
            DB.Exec($"INSERT INTO Friendships (UserId, FriendId, Status) VALUES (N'{senderUser}', N'{targetUser}', 'Pending');");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database Error on SendFriendRequest: {ex.Message}");
        }

        if (_connectedUsers.TryGetValue(targetUser, out var targetConnectionId))
        {
            await Clients.Client(targetConnectionId).SendAsync("ReceiveFriendRequest", senderUser);
        }
    }

    // 3. إرسال التحدي
    public async Task SendChallenge(string targetUser, string category, string word = "")
    {
        try
        {
            string senderUser = _connectedUsers.FirstOrDefault(x => x.Value == Context.ConnectionId).Key ?? "صديق";

            if (_connectedUsers.TryGetValue(targetUser, out var targetConnectionId))
            {
                if (string.IsNullOrEmpty(word))
                {
                    await Clients.Client(targetConnectionId).SendAsync("ReceiveChallenge", senderUser, category);
                }
                else
                {
                    await Clients.Client(targetConnectionId).SendAsync("ReceiveChallengeWithWord", senderUser, category, word);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SendChallenge: {ex.Message}");
        }
    }

    // 4. الرد على التحدي
    public async Task RespondToChallenge(string targetUser, bool isAccepted, string category)
    {
        string responderUser = _connectedUsers.FirstOrDefault(x => x.Value == Context.ConnectionId).Key ?? "صديق";

        if (_connectedUsers.TryGetValue(targetUser, out var targetConnectionId))
        {
            await Clients.Client(targetConnectionId).SendAsync("ChallengeResponseReceived", responderUser, isAccepted, category);
        }
    }

    // 🟢 5. إلغاء التحدي
    public async Task CancelChallenge(string targetUser)
    {
        string senderUser = _connectedUsers.FirstOrDefault(x => x.Value == Context.ConnectionId).Key ?? "صديق";

        if (_connectedUsers.TryGetValue(targetUser, out var targetConnectionId))
        {
            // إرسال إشعار للطرف الآخر بأن المرسل قام بإلغاء الطلب
            await Clients.Client(targetConnectionId).SendAsync("ChallengeCanceled", senderUser);
        }
    }

    // 6. قبول طلب الصداقة
    public async Task AcceptFriendRequest(string senderUser)
    {
        string acceptorUser = _connectedUsers.FirstOrDefault(x => x.Value == Context.ConnectionId).Key ?? "مستخدم";

        if (_connectedUsers.TryGetValue(senderUser, out var senderConnectionId))
        {
            await Clients.Client(senderConnectionId).SendAsync("FriendRequestAccepted", acceptorUser);
        }

        try
        {
            string sqlCommand = $@"
            UPDATE Friendships SET Status = 'Accepted' WHERE UserId = N'{senderUser}' AND FriendId = N'{acceptorUser}';
            INSERT INTO Friendships (UserId, FriendId, Status) VALUES (N'{acceptorUser}', N'{senderUser}', 'Accepted');";

            DB.Exec(sqlCommand);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database Error on AcceptFriendRequest: {ex.Message}");
        }
    }

    // 7. جلب قائمة الطلبات المعلقة
    public List<string> GetPendingFriendRequests()
    {
        string currentUser = _connectedUsers.FirstOrDefault(x => x.Value == Context.ConnectionId).Key ?? "";

        if (string.IsNullOrEmpty(currentUser)) return new List<string>();

        try
        {
            string sqlCommand = $"SELECT UserId FROM Friendships WHERE FriendId = '{currentUser}' AND Status = 'Pending'";
            string?[] pendingArray = DB.QueryAsArray(sqlCommand, "UserId");

            return pendingArray.Where(name => !string.IsNullOrEmpty(name)).ToList()!;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database Error on GetPendingFriendRequests: {ex.Message}");
            return new List<string>();
        }
    }

    // 8. جلب قائمة الأصدقاء المقبولين
    public List<string> GetFriends()
    {
        string currentUser = _connectedUsers.FirstOrDefault(x => x.Value == Context.ConnectionId).Key ?? "";

        if (string.IsNullOrEmpty(currentUser)) return new List<string>();

        try
        {
            string sqlCommand = $"SELECT FriendId FROM Friendships WHERE UserId = N'{currentUser}' AND Status = 'Accepted'";
            string?[] friendsArray = DB.QueryAsArray(sqlCommand, "FriendId");

            return friendsArray.Where(name => !string.IsNullOrEmpty(name)).ToList()!;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database Error on GetFriends: {ex.Message}");
            return new List<string>();
        }
    }

    // 9. جلب قائمة الطلبات المعلقة التي أرسلها المستخدم الحالي
    public List<string> GetSentPendingRequests()
    {
        string currentUser = _connectedUsers.FirstOrDefault(x => x.Value == Context.ConnectionId).Key ?? "";

        if (string.IsNullOrEmpty(currentUser)) return new List<string>();

        try
        {
            string sqlCommand = $"SELECT FriendId FROM Friendships WHERE UserId = '{currentUser}' AND Status = 'Pending'";
            string?[] sentArray = DB.QueryAsArray(sqlCommand, "FriendId");

            return sentArray.Where(name => !string.IsNullOrEmpty(name)).ToList()!;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database Error on GetSentPendingRequests: {ex.Message}");
            return new List<string>();
        }
    }

    // 10. جلب جميع الأصدقاء للمستخدم المتصل حالياً
    public List<string> GetAllFriends()
    {
        string currentUser = _connectedUsers.FirstOrDefault(x => x.Value == Context.ConnectionId).Key ?? "";

        if (string.IsNullOrEmpty(currentUser)) return new List<string>();

        try
        {
            string sqlCommand = $"SELECT FriendId FROM Friendships WHERE UserId = N'{currentUser}' AND Status = 'Accepted'";
            string?[] friendsArray = DB.QueryAsArray(sqlCommand, "FriendId");

            return friendsArray.Where(name => !string.IsNullOrEmpty(name)).ToList()!;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database Error on GetAllFriends: {ex.Message}");
            return new List<string>();
        }
    }

    // 11. عند تسجيل المستخدم أو إعادة اتصاله
    public async Task RegisterUser(string userName)
    {
        if (!string.IsNullOrEmpty(userName))
        {
            _connectedUsers[userName] = Context.ConnectionId;

            // إبلاغ جميع المستخدمين الآخرين بأن هذا الشخص أصبح متصلاً الآن 🟢
            await Clients.Others.SendAsync("UserConnected", userName);
        }
    }

    public async Task JoinDuelRoom(string roomName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomName);
    }

    public async Task SendDuelQuestion(string roomName, string senderName, string question)
    {
        await Clients.OthersInGroup(roomName).SendAsync("ReceiveDuelQuestion", senderName, question);
    }

    public async Task SendDuelAnswer(string roomName, string responderName, string answer)
    {
        await Clients.OthersInGroup(roomName).SendAsync("ReceiveDuelAnswer", responderName, answer);
    }

    public async Task SyncSecretWordForOpponent(string roomName, string targetUser, string secretWord, string arabicMeaning)
    {
        await Clients.OthersInGroup(roomName).SendAsync("ReceiveSecretWord", targetUser, secretWord, arabicMeaning);
    }

    public async Task DeclareDuelWinner(string roomName, string winnerName, string correctWord)
    {
        await Clients.OthersInGroup(roomName).SendAsync("ReceiveDuelWinner", winnerName, correctWord);
    }

    public async Task SendDuelWithdrawal(string roomName, string withdrawingUser)
    {
        await Clients.OthersInGroup(roomName).SendAsync("ReceiveDuelWithdrawal", withdrawingUser);
    }

    // 🟢 التعديل الأول: إرسال الرسالة مع الاعتماد على كلاس DB واسم المستخدم الفعلي
    // 🟢 1. إرسال الرسالة مع حفظ حالة التوصيل (IsDelivered) بناءً على اتصال الطرف الآخر
    public async Task SendDirectMessage(string receiverUser, string content)
    {
        string senderUser = _connectedUsers.FirstOrDefault(x => x.Value == Context.ConnectionId).Key ?? "";
        if (string.IsNullOrEmpty(senderUser)) return;

        bool isReceiverOnline = _connectedUsers.TryGetValue(receiverUser, out var targetConnectionId);
        int deliveredVal = isReceiverOnline ? 1 : 0; // إذا كان متصلاً تصبح مستلمة تلقائياً (صحين رمادي)

        try
        {
            string safeContent = content.Replace("'", "''");
            // حفظ الرسالة في القاعدة مع تحديد حالة الاستلام
            string sql = $@"
                INSERT INTO Messages (SenderId, ReceiverId, Content, Timestamp, IsDelivered, IsRead, DeliveredAt) 
                OUTPUT INSERTED.Id
                VALUES (N'{senderUser}', N'{receiverUser}', N'{safeContent}', GETDATE(), {deliveredVal}, 0, {(isReceiverOnline ? "GETDATE()" : "NULL")});";

            DataTable dt = DB.Query(sql);
            int messageId = dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0][0]) : 0;

            // إذا كان الطرف الآخر متصلاً، أرسل له الرسالة مباشرة مع معرفها
            if (isReceiverOnline && targetConnectionId != null)
            {
                await Clients.Client(targetConnectionId).SendAsync("ReceiveDirectMessage", messageId, senderUser, content);
            }

            // إرسال تحديث للحالة للمرسل نفسه ليظهر له (صحين رمادي) إذا استلمها الطرف الآخر
            if (isReceiverOnline)
            {
                await Clients.Caller.SendAsync("MessageDelivered", messageId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database Error on SendDirectMessage: {ex.Message}");
        }
    }

    // 🟢 2. جلب سجل المحادثة مع إرجاع معرف الرسالة وحالتها
    public List<ChatMessageDto> GetChatHistory(string targetUser)
    {
        string currentUser = _connectedUsers.FirstOrDefault(x => x.Value == Context.ConnectionId).Key ?? "";
        var messages = new List<ChatMessageDto>();

        if (string.IsNullOrEmpty(currentUser)) return messages;

        try
        {
            string sql = $@"
                SELECT Id, SenderId, ReceiverId, Content, Timestamp, IsDelivered, IsRead 
                FROM Messages 
                WHERE (SenderId = N'{currentUser}' AND ReceiverId = N'{targetUser}')
                   OR (SenderId = N'{targetUser}' AND ReceiverId = N'{currentUser}')
                ORDER BY Timestamp ASC";

            DataTable dt = DB.Query(sql);

            foreach (DataRow row in dt.Rows)
            {
                messages.Add(new ChatMessageDto
                {
                    Id = Convert.ToInt32(row["Id"]),
                    Sender = row["SenderId"]?.ToString() ?? "",
                    Receiver = row["ReceiverId"]?.ToString() ?? "",
                    Content = row["Content"]?.ToString() ?? "",
                    Timestamp = Convert.ToDateTime(row["Timestamp"]),
                    IsDelivered = Convert.ToBoolean(row["IsDelivered"]),
                    IsRead = Convert.ToBoolean(row["IsRead"])
                });
            }

            // 🟢 بمجرد جلب السيرفر للرسائل وفتح المستخدم للمحادثة، نحدد أن الرسائل الواردة أصبحت مقروءة!
            string updateSql = $@"
                UPDATE Messages 
                SET IsRead = 1, ReadAt = GETDATE(), IsDelivered = 1, DeliveredAt = ISNULL(DeliveredAt, GETDATE())
                WHERE SenderId = N'{targetUser}' AND ReceiverId = N'{currentUser}' AND IsRead = 0;
            ";
            DB.Exec(updateSql);

            // إبلاغ الطرف الآخر (المرسل) بأن رسائله تمت قراءتها (لتتحول إلى صحين أزرق لديه)
            if (_connectedUsers.TryGetValue(targetUser, out var senderConnectionId))
            {
                Clients.Client(senderConnectionId).SendAsync("MessagesReadBy", currentUser);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database Error on GetChatHistory: {ex.Message}");
        }

        return messages;
    }

    // 🟢 دالة لتحديث الرسائل إلى مقروءة فوراً عندما يراها المستخدم وهو داخل المحادثة
    public async Task MarkMessagesAsRead(string senderUser)
    {
        string currentUser = _connectedUsers.FirstOrDefault(x => x.Value == Context.ConnectionId).Key ?? "";
        if (string.IsNullOrEmpty(currentUser)) return;

        try
        {
            string updateSql = $@"
                UPDATE Messages 
                SET IsRead = 1, ReadAt = GETDATE(), IsDelivered = 1, DeliveredAt = ISNULL(DeliveredAt, GETDATE())
                WHERE SenderId = N'{senderUser}' AND ReceiverId = N'{currentUser}' AND IsRead = 0;
            ";
            DB.Exec(updateSql);

            // إبلاغ المرسل فوراً لتتحول الصحين إلى اللون الأزرق لديه
            if (_connectedUsers.TryGetValue(senderUser, out var senderConnectionId))
            {
                await Clients.Client(senderConnectionId).SendAsync("MessagesReadBy", currentUser);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database Error on MarkMessagesAsRead: {ex.Message}");
        }
    }

    // 1. أضف هذه الدالة لجلب آخر ظهور للمستخدم
    public DateTime? GetLastSeen(string username)
    {
        try
        {
            string sql = $"SELECT LastSeen FROM UsersTbl WHERE UserName = N'{username}'";
            DataTable dt = DB.Query(sql);
            if (dt.Rows.Count > 0 && dt.Rows[0]["LastSeen"] != DBNull.Value)
            {
                return Convert.ToDateTime(dt.Rows[0]["LastSeen"]);
            }
        }
        catch { }
        return null;
    }
}