using Microsoft.AspNetCore.Mvc;

namespace FoundationalCSharp.Server.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class HtmlStudentsController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetHtmlStudents()
        {
            string query = "SELECT Name ArabicName, Degree " +
                           "FROM HtmlStudentsTbl " +
                           "WHERE Degree > 0 " +
                           "ORDER BY Degree DESC";
            Student[] leaders = DB.QueryAsHtmlStudents(query);
            return Ok(leaders);
        }

        [HttpPost("GetStudent")]
        public IActionResult GetStudent([FromBody] Student student)
        {
            Student? s = DB.GetHtmlStudent($"SELECT * FROM HtmlStudentsTbl WHERE Name = N'{student.ArabicName}' AND Password = N'{student.Password}'");

            if (s is null)
                return NotFound("لم يتم العثور على المستخدم");

            return Ok(s);
        }

        [HttpPost("AddStudent")]
        public IActionResult AddStudent(Student student)
        {
            string? id = DB.AddHtmlStudent(student);
            return Ok(id);
        }

        [HttpPut("UpdateHtmlStudent")]
        public IActionResult UpdateHtmlStudent([FromBody] Student student)
        {
            try
            {
                DB.UpdateHtmlStudent(student);
                return Ok();
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpPut("UpdateDegree")]
        public IActionResult UpdateDegree([FromBody] Student student)
        {
            DB.Exec($"UPDATE HtmlStudentsTbl SET Degree = {student.Degree} WHERE ID = {student.ID}");
            return Ok("1");
        }
    }
}
