// ...existing code...
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

    public int GetMemorizedWordsCount(string userName)
    {
        if (string.IsNullOrEmpty(userName)) return 0;
        try
        {
            string sqlCommand = $"SELECT MemorizedWords FROM UsersTbl WHERE Username = N'{userName}'";
            DataTable dt = DB.Query(sqlCommand);
            if (dt != null && dt.Rows.Count > 0 && dt.Rows[0]["MemorizedWords"] != DBNull.Value)
            {
                return Convert.ToInt32(dt.Rows[0]["MemorizedWords"]);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database Error on GetMemorizedWordsCount: {ex.Message}");
        }
        return 0;
    }

    // 11. عند تسجيل المستخدم أو إعادة اتصاله
    public async Task RegisterUser(string userName)
// ...existing code...