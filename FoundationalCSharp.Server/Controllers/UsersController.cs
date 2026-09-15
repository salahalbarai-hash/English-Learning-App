using Microsoft.AspNetCore.Mvc;
using System.Data;

namespace FoundationalCSharp.Server.Controllers
{
    [ApiController]
    [Route("/Users")]
    public class UsersController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetUsers()
        {
            string query = "SELECT UserName, YER " +
                           "FROM UsersTbl " +
                           "WHERE TRY_CAST(YER AS INT) > 0 " +
                           "ORDER BY TRY_CAST(YER AS INT)";
            DataTable users = DB.Query(query);
            return Ok(users);
        }

        [HttpGet("GetGeminiKey")]
        public ActionResult<string?> GetAvailableGeminiKey()
        {
            DataTable dt = DB.Query("SELECT * FROM GeminiKeys ORDER BY WindowStart");
            List<GeminiKeyModel> keys = [];

            foreach (DataRow row in dt.Rows)
            {
                keys.Add(new GeminiKeyModel
                {
                    Id = Convert.ToInt32(row["Id"]),
                    ApiKey = row["ApiKey"].ToString() ?? "",
                    IsActive = Convert.ToBoolean(row["IsActive"]),
                    RequestCount = Convert.ToInt32(row["RequestCount"]),
                    WindowStart = Convert.ToDateTime(row["WindowStart"])
                });
            }

            string updateSql = "", selectedKey = "";

            DateTime now = DateTime.Now;

            foreach (var key in keys)
            {
                bool changed = false;

                // إذا مرت دقيقة يتم تصفير العداد
                TimeSpan elapsed = now - key.WindowStart;

                if (elapsed >= TimeSpan.FromMinutes(1))
                {
                    key.RequestCount = 0;
                    changed = true;
                }

                // اختيار أقدم مفتاح فعال ومتاح
                if (selectedKey == "" && key.IsActive && key.RequestCount < 5)
                {
                    key.RequestCount++;

                    // تحديث وقت آخر استخدام حتى لا يعود له مباشرة
                    key.WindowStart = now;

                    selectedKey = key.ApiKey;
                    changed = true;
                }

                // إضافة التعديل فقط إذا تغير المفتاح
                if (changed)
                {
                    updateSql += $@"UPDATE GeminiKeys SET RequestCount = {key.RequestCount},
                                  WindowStart = '{key.WindowStart:yyyy-MM-ddTHH:mm:ss}'
                              WHERE Id = {key.Id};";
                }
            }

            if (updateSql.Length > 0)
            {
                DB.Exec(updateSql);
            }

            return selectedKey;
        }
        [HttpPost("AddStudent")]
        public IActionResult AddStudent(Student student)
        {
            string? id = DB.AddStudent(student);
            return Ok(id);
        }
        [HttpPost("GetStudent")]
        public IActionResult GetStudent(string arabicName, string password)
        {
            Student? student = DB.GetStudent($"SELECT * FROM Students WHERE arabicName = '{arabicName}' AND Password = '{password}'");
            if (student is null)
                return NotFound("لم يتم العثور على المستخدم");
            return Ok(student);
        }
        [HttpGet("TopTen")]
        public IActionResult GetTopTen()
        {
            string query = "SELECT TOP (10) UserName, TimeFinalExam " +
                           "FROM UsersTbl " +
                           "WHERE TimeFinalExam IS NOT NULL AND TimeFinalExam > '00:00:01' " +
                           "ORDER BY TimeFinalExam";
            Leader[] leaders = DB.QueryAsLeaders(query);
            return Ok(leaders);
        }

        [HttpGet("GetFriends")]
        public IActionResult GetFriends(string userName)
        {
            try
            {
                // 1. الاستعلام الذي يستخدم الاسم المستعار (AS FriendName) ليتوافق مع QueryAsArray
                string query = $"SELECT CASE WHEN UserId = N'{userName}' THEN FriendId ELSE UserId END AS FriendName " +
                               $"FROM Friendships " +
                               $"WHERE UserId = N'{userName}' OR FriendId = N'{userName}' AND Status = 'Accepted'";

                // 2. استدعاء دالتك الجاهزة مباشرة
                string?[] friendsArray = DB.QueryAsArray(query, "FriendName");

                // 3. تنقية النتائج (إزالة القيم الفارغة، أو تكرار اسم المستخدم نفسه) وتحويلها إلى قائمة
                var friendsList = friendsArray
                    .Where(f => !string.IsNullOrEmpty(f) && f != userName)
                    .Distinct()
                    .ToList();

                return Ok(friendsList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ أثناء جلب الأصدقاء", error = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult AddUser(User user)
        {
            string? id = DB.AddUser(user);
            return Ok(id);
        }
        [HttpPost("GetUser")]
        public IActionResult GetUser([FromBody] User user)
        {
            User? u = DB.GetUser($"SELECT * FROM UsersTbl WHERE UserName = N'{user.UserName}' AND Password = N'{user.Password}'");

            if (u is null)
                return NotFound(new ApiResult<User> { Success = false, Message = "بيانات الدخول غير صحيحة" });

            return Ok(new ApiResult<User> { Success = true, Data = u });
        }

        [HttpGet("GetUserByUserName")]
        public IActionResult GetUserByUserName(string userName)
        {
            User? u = DB.GetUser($"SELECT * FROM UsersTbl WHERE UserName = N'{userName}'");

            if (u is null)
                return NotFound(new ApiResult<User> { Success = false, Message = "لم يتم العثور على المستخدم" });

            // إخفاء كلمة المرور لأسباب أمنية
            u.Password = null;

            return Ok(new ApiResult<User> { Success = true, Data = u });
        }
        [HttpPost("AddPoints")]
        public IActionResult AddPoints([FromBody] AddPointsModel model)
        {
            DB.Exec($"UPDATE UsersTbl SET TimeFinalExam = '{model.Id}' WHERE ID = {model.Number}");
            return Ok();
        }
        [HttpPut]
        public IActionResult UpdateUser(User user)
        {
            try
            {
                DB.UpdateUser(user);
                return Ok();
            }
            catch (Exception)
            {
                throw;
            }
        }
        [HttpPut("UpdateCoins")]
        public IActionResult UpdateCoins([FromBody] User user)
        {
            try
            {
                DB.UpdateUserCoins(user.ID, user.Coins);
                return Ok("1");
            }
            catch (Exception)
            {
                return BadRequest();
            }
        }
        [HttpPut("UpdateTimeFinalExam")]
        public IActionResult UpdateTimeFinalExam(TimeFinalExamModel timeFinalExamModel)
        {
            try
            {
                string command = $"UPDATE UsersTbl SET TimeFinalExam = '{timeFinalExamModel.Time}' WHERE ID = {timeFinalExamModel.Id}";
                DB.Exec(command);
                return Ok();
            }
            catch (Exception)
            {
                throw;
            }
        }
        [HttpDelete("{userName}")]
        public IActionResult DeleteUser(string userName)
        {
            DB.Exec($"DELETE FROM UsersTbl WHERE UserName = '{userName}'");
            return Ok(userName);
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadImage([FromForm] IFormFile image)
        {
            if (image == null || image.Length == 0)
            {
                return BadRequest("No image provided.");
            }

            // مسار الحفظ داخل مجلد المشروع
            var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Images");
            Directory.CreateDirectory(uploadPath); // إنشاء المجلد إذا لم يكن موجودًا
            var filePath = Path.Combine(uploadPath, image.FileName);
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }
                return Ok($"/Images/{image.FileName}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
        //CustomerImg.Source = ImageSource.FromUri(new Uri("https://your-server.com/uploads/your-image.jpg"));
    }
    public class AddPointsModel
    {
        public string? Id { get; set; }
        public double Number { get; set; }
    }
}