using Microsoft.AspNetCore.Mvc;

namespace FoundationalCSharp.Server.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class StudentsController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetStudents()
        {
            string query = "SELECT UserName, MemorizedWords, Coins " +
                           "FROM UsersTbl " +
                           "WHERE Coins > 0 OR MemorizedWords > 10 " +
                           "ORDER BY Coins DESC, MemorizedWords DESC";
            Leader[] leaders = DB.QueryAsLeaders(query);
            return Ok(leaders);
        }

        [HttpPut("UpdateMemorizedWords")]
        public IActionResult UpdateMemorizedWord([FromBody] User user)
        {
            DB.Exec($"UPDATE UsersTbl SET MemorizedWords = {user.MemorizedWords} WHERE ID = {user.ID}");
            return Ok("1");
        }

        [HttpPut("FriendsChallengeCount")]
        public IActionResult UpdateFriendsChallengeCount([FromBody] User user)
        {
            DB.Exec($"UPDATE UsersTbl SET FriendsChallengeCount = {user.FriendsChallengeCount} WHERE ID = {user.ID}");
            return Ok("1");
        }

        [HttpPost("GetStudent")]
        public IActionResult GetStudent([FromBody] Student user)
        {
            User? u = DB.GetUser($"SELECT * FROM UsersTbl WHERE UserName = N'{user.ArabicName}' AND Password = N'{user.Password}'");
            if (u is null)
                return NotFound("لم يتم العثور على المستخدم");
            return Ok(u);
        }
    }
}