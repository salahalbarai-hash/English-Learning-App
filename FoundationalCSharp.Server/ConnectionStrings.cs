using System;
using System.Collections.Generic;
using System.Data;
namespace FoundationalCSharp
{
    public class ConnectionStrings
    {
        public string LocalConnectionString => $"Server=SALAH\\SQLEXPRESS; Database=DB; User Id = sa; Password=qwaszx!@123; Connection Timeout=5;TrustServerCertificate=True;";
        public string ConnectionString => $"Server=db45520.public.databaseasp.net; Database=db45520; User Id=db45520; Password=2Yz%=c4AP3#x; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;  ";
       
        public bool CheckConnection()
        {
            using SqlConnection connection = new(ConnectionString);
            try
            {
                connection.Open();
                return true;
            }
            catch (SqlException)
            {
                return false;
            }
        }
    }
}
