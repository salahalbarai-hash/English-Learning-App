using System.Data;
namespace FoundationalCSharp.Server
{
    public class DB
    {
        private static string GetConnectionString()
        {
            return new ConnectionStrings().ConnectionString;
        }

        public static void UpdateUserCoins(long id, long coins)
        {
            using SqlConnection conn = new(GetConnectionString());
            using SqlCommand sqlCommand = new("UPDATE UsersTbl SET Coins = @Coins WHERE ID = @ID", conn);

            sqlCommand.Parameters.Add("@ID", SqlDbType.BigInt).Value = id;
            sqlCommand.Parameters.Add("@Coins", SqlDbType.BigInt).Value = coins;

            conn.Open();
            sqlCommand.ExecuteNonQuery();
        }

        public static DataTable Query(string command)
        {
            using DataTable dt = new();
            using SqlConnection conn = new(GetConnectionString());
            using SqlDataAdapter da = new(command, conn);

            da.Fill(dt);
            return dt;
        }

        public static string?[][] QueryAsArray(string command)
        {
            using DataTable dt = new();
            using SqlConnection conn = new(GetConnectionString());
            using SqlDataAdapter da = new(command, conn);

            da.Fill(dt);
            return [.. dt.AsEnumerable().Select(row => row.ItemArray.Select(val => val?.ToString()).ToArray())];
        }

        public static string?[] QueryAsArray(string command, string columnName)
        {
            using DataTable dt = new();
            using SqlConnection conn = new(GetConnectionString());
            using SqlDataAdapter da = new(command, conn);

            da.Fill(dt);
            return dt.AsEnumerable().Select(row => row.Field<string>(columnName)).ToArray();
        }

        public static User[] QueryAsUsers(string command)
        {
            var users = new List<User>();

            using SqlConnection conn = new(GetConnectionString());
            using SqlCommand cmd = new(command, conn);

            conn.Open();
            using SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                users.Add(new User
                {
                    UserName = Convert.ToString(reader["UserName"]),
                    Password = Convert.ToString(reader["Password"]),
                    PhoneNumber = Convert.ToString(reader["PhoneNumber"])
                });
            }

            return [.. users];
        }

        public static Leader[] QueryAsLeaders(string command)
        {
            var leaders = new List<Leader>();

            using SqlConnection conn = new(GetConnectionString());
            using SqlCommand cmd = new(command, conn);

            conn.Open();
            using SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                leaders.Add(new Leader
                {
                    UserName = Convert.ToString(reader["UserName"]) ?? "",
                    MemorizedWords = Convert.ToInt32(reader["MemorizedWords"])
                });
            }

            return [.. leaders];
        }

        public static Student[] QueryAsHtmlStudents(string command)
        {
            var students = new List<Student>();

            using SqlConnection conn = new(GetConnectionString());
            using SqlCommand cmd = new(command, conn);

            conn.Open();
            using SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                students.Add(new Student
                {
                    ArabicName = Convert.ToString(reader["ArabicName"]) ?? "",
                    Degree = Convert.ToInt32(reader["Degree"])
                });
            }

            return [.. students];
        }

        public static void Exec(string command)
        {
            using SqlConnection conn = new(GetConnectionString());
            using SqlCommand cmd = new(command, conn);

            conn.Open();
            cmd.ExecuteNonQuery();
        }

        public static void Exec(string?[] commands)
        {
            using SqlConnection conn = new(GetConnectionString());
            conn.Open();

            foreach (var item in commands)
            {
                if (!string.IsNullOrWhiteSpace(item))
                {
                    using SqlCommand cmd = new(item, conn);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static DataTable Execute(string command, Dictionary<string, object?> parameters, CommandType commandType)
        {
            DataTable resultTable = new();
            try
            {
                using SqlConnection conn = new(GetConnectionString());
                using SqlCommand sqlCommand = new(command, conn)
                {
                    CommandType = commandType
                };

                foreach (var param in parameters)
                {
                    sqlCommand.Parameters.Add(new SqlParameter(param.Key, param.Value ?? DBNull.Value));
                }

                using SqlDataAdapter sqlDataAdapter = new(sqlCommand);
                sqlDataAdapter.Fill(resultTable);
            }
            catch
            {
                // يُفضل تسجيل الخطأ هنا (Logging) بدلاً من تركه فارغاً لتسهيل التتبع
            }

            return resultTable;
        }

        public static void UpdateUser(User user)
        {
            using SqlConnection conn = new(GetConnectionString());
            using SqlCommand sqlCommand = new("UpdateUser", conn) { CommandType = CommandType.StoredProcedure };

            sqlCommand.Parameters.Add("@ID", SqlDbType.BigInt).Value = user.ID;
            sqlCommand.Parameters.Add("@UserName", SqlDbType.NVarChar, 100).Value = user.UserName ?? (object)DBNull.Value;
            sqlCommand.Parameters.Add("@Password", SqlDbType.NVarChar, 100).Value = user.Password ?? (object)DBNull.Value;
            sqlCommand.Parameters.Add("@PhoneNumber", SqlDbType.NVarChar, 9).Value = user.PhoneNumber ?? (object)DBNull.Value;

            conn.Open();
            sqlCommand.ExecuteNonQuery();
        }

        public static string? AddUser(User user)
        {
            try
            {
                using SqlConnection conn = new(GetConnectionString());
                using SqlCommand sqlCommand = new("AddUser", conn) { CommandType = CommandType.StoredProcedure };

                sqlCommand.Parameters.Add("@UserName", SqlDbType.NVarChar, 50).Value = user.UserName ?? (object)DBNull.Value;
                sqlCommand.Parameters.Add("@Password", SqlDbType.NVarChar, 50).Value = user.Password ?? (object)DBNull.Value;
                sqlCommand.Parameters.Add("@PhoneNumber", SqlDbType.NVarChar, 9).Value = user.PhoneNumber ?? (object)DBNull.Value;

                conn.Open();
                object? result = sqlCommand.ExecuteScalar();
                return result?.ToString();
            }
            catch (Exception ex)
            {
                throw new Exception("حدث خطأ أثناء تنفيذ الاستعلام.", ex);
            }
        }

        public static string? AddHtmlStudent(Student student)
        {
            try
            {
                using SqlConnection conn = new(GetConnectionString());
                using SqlCommand sqlCommand = new("AddHtmlStudent", conn) { CommandType = CommandType.StoredProcedure };

                sqlCommand.Parameters.Add("@Name", SqlDbType.NVarChar, 50).Value = student.ArabicName ?? (object)DBNull.Value;
                sqlCommand.Parameters.Add("@Password", SqlDbType.NVarChar, 50).Value = student.Password ?? (object)DBNull.Value;
                sqlCommand.Parameters.Add("@PhoneNumber", SqlDbType.NVarChar, 9).Value = student.PhoneNumber ?? (object)DBNull.Value;

                conn.Open();
                object? result = sqlCommand.ExecuteScalar();
                return result?.ToString();
            }
            catch (Exception ex)
            {
                throw new Exception("حدث خطأ أثناء تنفيذ الاستعلام.", ex);
            }
        }

        public static User? GetUser(string command)
        {
            using SqlConnection conn = new(GetConnectionString());
            using SqlCommand cmd = new(command, conn);

            conn.Open();
            using SqlDataReader reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                return new User
                {
                    ID = Convert.ToInt32(reader["ID"]),
                    UserName = Convert.ToString(reader["UserName"]),
                    Password = Convert.ToString(reader["Password"]),
                    PhoneNumber = Convert.ToString(reader["PhoneNumber"]),
                    TimeFinalExam = Convert.ToString(reader["TimeFinalExam"]),
                    YER = Convert.ToString(reader["YER"]),
                    MemorizedWords = Convert.ToInt32(reader["MemorizedWords"]),
                    Coins = Convert.ToInt64(reader["Coins"]),
                    FriendsChallengeCount = Convert.ToInt64(reader["FriendsChallengeCount"]),
                };
            }

            return null;
        }

        public static Student? GetStudent(string command)
        {
            using SqlConnection conn = new(GetConnectionString());
            using SqlCommand cmd = new(command, conn);

            conn.Open();
            using SqlDataReader reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                return new Student
                {
                    ID = Convert.ToInt64(reader["ID"]),
                    ArabicName = Convert.ToString(reader["ArabicName"]),
                    EnglishName = Convert.ToString(reader["EnglishName"]),
                    NickName = Convert.ToString(reader["NickName"]),
                    Password = Convert.ToString(reader["Password"]),
                    Socre = Convert.ToInt64(reader["Socre"]),
                    PhoneNumber = Convert.ToString(reader["PhoneNumber"]),
                    Degree = Convert.ToInt32(reader["Degree"]),
                };
            }

            return null;
        }

        public static Student? GetHtmlStudent(string command)
        {
            using SqlConnection conn = new(GetConnectionString());
            using SqlCommand cmd = new(command, conn);

            conn.Open();
            using SqlDataReader reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                return new Student
                {
                    ID = Convert.ToInt64(reader["ID"]),
                    ArabicName = Convert.ToString(reader["Name"]),
                    Password = Convert.ToString(reader["Password"]),
                    PhoneNumber = Convert.ToString(reader["PhoneNumber"]),
                    Degree = Convert.ToInt32(reader["Degree"]),
                };
            }

            return null;
        }

        public static void UpdateHtmlStudent(Student student)
        {
            using SqlConnection conn = new(GetConnectionString());
            using SqlCommand sqlCommand = new("UpdateHtmlStudent", conn) { CommandType = CommandType.StoredProcedure };

            sqlCommand.Parameters.Add("@ID", SqlDbType.BigInt).Value = student.ID;
            sqlCommand.Parameters.Add("@ArabicName", SqlDbType.NVarChar, 100).Value = student.ArabicName ?? (object)DBNull.Value;
            sqlCommand.Parameters.Add("@Password", SqlDbType.NVarChar, 100).Value = student.Password ?? (object)DBNull.Value;
            sqlCommand.Parameters.Add("@PhoneNumber", SqlDbType.NVarChar, 9).Value = student.PhoneNumber ?? (object)DBNull.Value;

            conn.Open();
            sqlCommand.ExecuteNonQuery();
        }

        public static string? AddStudent(Student student)
        {
            try
            {
                using SqlConnection conn = new(GetConnectionString());
                using SqlCommand sqlCommand = new("AddStudent", conn) { CommandType = CommandType.StoredProcedure };

                sqlCommand.Parameters.Add("@ArabicName", SqlDbType.NVarChar, 100).Value = student.ArabicName ?? (object)DBNull.Value;
                sqlCommand.Parameters.Add("@EnglishName", SqlDbType.NVarChar, 100).Value = student.EnglishName ?? (object)DBNull.Value;
                sqlCommand.Parameters.Add("@NickName", SqlDbType.NVarChar, 100).Value = student.NickName ?? (object)DBNull.Value;
                sqlCommand.Parameters.Add("@Password", SqlDbType.NVarChar, 100).Value = student.Password ?? (object)DBNull.Value;
                sqlCommand.Parameters.Add("@PhoneNumber", SqlDbType.NVarChar, 9).Value = student.PhoneNumber ?? (object)DBNull.Value;

                conn.Open();
                object? result = sqlCommand.ExecuteScalar();
                return result?.ToString();
            }
            catch (Exception ex)
            {
                throw new Exception("حدث خطأ أثناء تنفيذ الاستعلام.", ex);
            }
        }
    }
}