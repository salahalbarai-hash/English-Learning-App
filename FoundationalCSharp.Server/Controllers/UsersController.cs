using System;
using System.Data;
using Microsoft.AspNetCore.Mvc;
using Shared.Models;

namespace FoundationalCSharp.Server.Controllers
{
    [ApiController]
    [Route("Users")]
    public class UsersController : ControllerBase
    {
        [HttpGet("GetUserById")]
        public ActionResult<ApiResult<User>> GetUserById([FromQuery] long id)
        {
            try
            {
                var dt = DB.Query($"SELECT * FROM UsersTbl WHERE ID = {id}");
                if (dt.Rows.Count == 0)
                {
                    return new ApiResult<User> { Success = false, Message = "User not found" };
                }

        [HttpGet("GetUserByUserName")]
        public ActionResult<ApiResult<User>> GetUserByUserName([FromQuery] string userName)
        {
            try
            {
                var dt = DB.Query($"SELECT * FROM UsersTbl WHERE UserName = N'{userName}'");
                if (dt.Rows.Count == 0)
                {
                    return new ApiResult<User> { Success = false, Message = "User not found" };
                }

                var row = dt.Rows[0];

                var user = new User
                {
                    ID = Convert.ToInt64(row["ID"]),
                    UserName = Convert.ToString(row["UserName"]),
                    Password = Convert.ToString(row["Password"]),
                    PhoneNumber = Convert.ToString(row["PhoneNumber"]),
                    MemorizedWords = row.Table.Columns.Contains("MemorizedWords") && row["MemorizedWords"] != DBNull.Value ? Convert.ToInt32(row["MemorizedWords"]) : 0,
                    Coins = row.Table.Columns.Contains("Coins") && row["Coins"] != DBNull.Value ? Convert.ToInt64(row["Coins"]) : 0,
                    FriendsChallengeCount = row.Table.Columns.Contains("FriendsChallengeCount") && row["FriendsChallengeCount"] != DBNull.Value ? Convert.ToInt64(row["FriendsChallengeCount"]) : 0
                };

                return new ApiResult<User> { Success = true, Data = user };
            }
            catch (Exception ex)
            {
                return new ApiResult<User> { Success = false, Message = ex.Message };
            }
        }

        [HttpGet("GetFriendsWithIds")]
        public ActionResult<User[]> GetFriendsWithIds([FromQuery] string userName)
        {
            try
            {
                // Friendships table stores FriendId as the friend's userName
                var rows = DB.QueryAsArray($"SELECT u.ID, u.UserName FROM Friendships f JOIN UsersTbl u ON u.UserName = f.FriendId WHERE f.UserId = N'{userName}' AND f.Status = 'Accepted'");
                var list = new List<User>();
                foreach (var row in rows)
                {
                    if (row.Length >= 2 && !string.IsNullOrWhiteSpace(row[0]) && !string.IsNullOrWhiteSpace(row[1]))
                    {
                        if (long.TryParse(row[0], out long id))
                        {
                            list.Add(new User { ID = id, UserName = row[1] });
                        }
                    }
                }
                return list.ToArray();
            }
            catch
            {
                return new User[0];
            }
        }

                var row = dt.Rows[0];

                var user = new User
                {
                    ID = Convert.ToInt64(row["ID"]),
                    UserName = Convert.ToString(row["UserName"]),
                    Password = Convert.ToString(row["Password"]),
                    PhoneNumber = Convert.ToString(row["PhoneNumber"]),
                    MemorizedWords = row.Table.Columns.Contains("MemorizedWords") && row["MemorizedWords"] != DBNull.Value ? Convert.ToInt32(row["MemorizedWords"]) : 0,
                    Coins = row.Table.Columns.Contains("Coins") && row["Coins"] != DBNull.Value ? Convert.ToInt64(row["Coins"]) : 0,
                    FriendsChallengeCount = row.Table.Columns.Contains("FriendsChallengeCount") && row["FriendsChallengeCount"] != DBNull.Value ? Convert.ToInt64(row["FriendsChallengeCount"]) : 0
                };

                return new ApiResult<User> { Success = true, Data = user };
            }
            catch (Exception ex)
            {
                return new ApiResult<User> { Success = false, Message = ex.Message };
            }
        }
    }

}
