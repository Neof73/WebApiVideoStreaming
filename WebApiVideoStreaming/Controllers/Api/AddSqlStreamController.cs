using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace WebApiVideoStreaming.Controllers.Api
{
    public class AddSqlStreamController : ApiController
    {
        private static string connectionString = ConfigurationManager.ConnectionStrings["Streaming"].ConnectionString.ToString();

        // Application transferring a large BLOB to SQL Server in .Net 4.5  
        public static async Task StreamBLOBToServer(string name, Stream file)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand("INSERT INTO [Streams] (textdata, bindata) VALUES (@name, @bindata)", conn))
                {
                    // Add a parameter which uses the FileStream we just opened  
                    // Size is set to -1 to indicate "MAX"  
                    cmd.Parameters.Add("@name", SqlDbType.NVarChar, 100).Value = name;
                    cmd.Parameters.Add("@bindata", SqlDbType.Binary, -1).Value = file;

                    // Send the data to the server asynchronously  
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
    }
}
