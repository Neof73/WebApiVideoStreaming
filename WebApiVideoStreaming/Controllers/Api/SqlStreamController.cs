using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.IO;
using System.Threading.Tasks;
using System.Web.Http;

using WebApiVideoStreaming.Models;

namespace WebApiVideoStreaming.Controllers.Api
{
    public class SqlStreamController : ApiController
    {
        private static string connectionString => ConfigurationManager.ConnectionStrings["Streaming"].ConnectionString;
        private static string connectionStringFS => ConfigurationManager.ConnectionStrings["StreamingFS"].ConnectionString;


        public static async Task<List<String>> GetVideoList()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                List<String> list = new List<string>();
                await connection.OpenAsync();
                using (SqlCommand command = new SqlCommand("SELECT [textdata] FROM [Streams] ", connection))
                {
                    // The reader needs to be executed with the SequentialAccess behavior to enable network streaming  
                    // Otherwise ReadAsync will buffer the entire BLOB into memory which can cause scalability issues or even OutOfMemoryExceptions  
                    using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess)) 
                    {
                        while (await reader.ReadAsync()) 
                        {
                            if (!(await reader.IsDBNullAsync(0))) 
                            {
                                string data = reader.GetString(0);
                                list.Add(data);
                            }
                        }
                    }
                }
                return list;
            }
        }

        // Application retrieving a large BLOB from SQL Server in .NET 4.5 using the new asynchronous capability  
        public static async Task<byte[]> GetBinaryValue(string name)
        {
            //string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "binarydata.bin");

            byte[] data = null;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (SqlCommand command = new SqlCommand("SELECT [bindata] FROM [Streams] WHERE [textdata]=@name", connection))
                {
                    command.Parameters.AddWithValue("name", name);

                    // The reader needs to be executed with the SequentialAccess behavior to enable network streaming  
                    // Otherwise ReadAsync will buffer the entire BLOB into memory which can cause scalability issues or even OutOfMemoryExceptions  
                    using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess))
                    {
                        if (await reader.ReadAsync())
                        {
                            if (!(await reader.IsDBNullAsync(0)))
                            {
                                data = reader.GetSqlBinary(0).Value;
                                return data;
                                
                            }
                        }
                    }
                }
            }
            return data;
        }

        public static async Task<Video> GetFileStream(string name, long blocksize, long from)
        {
            using (SqlConnection sqlConnection = new SqlConnection(connectionStringFS))
            {
                using (SqlCommand sqlCommand = new SqlCommand { Connection = sqlConnection })
                {
                    try
                    {
                        await sqlConnection.OpenAsync();

                        //The first task is to retrieve the file path
                        //of the SQL FILESTREAM BLOB that we want to
                        //access in the application.

                        sqlCommand.CommandText = "SELECT Stream.PathName() FROM [Videos] WHERE [Name] = @name";
                        sqlCommand.Parameters.AddWithValue("name", name);

                        String filePath = null;

                        Object pathObj = await sqlCommand.ExecuteScalarAsync();
                        if (DBNull.Value != pathObj)
                            filePath = (string)pathObj;
                        else
                        {
                            throw new System.Exception(
                                "Stream.PathName() failed"
                                + " to read the path name "
                                + " for the Stream column.");
                        }

                        //The next task is to obtain a transaction
                        //context. All FILESTREAM BLOB operations
                        //occur within a transaction context to
                        //maintain data consistency.

                        //All SQL FILESTREAM BLOB access must occur in 
                        //a transaction. MARS-enabled connections
                        //have specific rules for batch scoped transactions,
                        //which the Transact-SQL BEGIN TRANSACTION statement
                        //violates. To avoid this issue, client applications 
                        //should use appropriate API facilities for transaction management, 
                        //management, such as the SqlTransaction class.

                        SqlTransaction transaction = sqlConnection.BeginTransaction("mainTransaction");
                        sqlCommand.Transaction = transaction;

                        sqlCommand.CommandText = "SELECT GET_FILESTREAM_TRANSACTION_CONTEXT()";

                        Object obj = await sqlCommand.ExecuteScalarAsync();
                        byte[] txContext = (byte[])obj;

                        //The next step is to obtain a handle that
                        //can be passed to the Win32 FILE APIs.
                        byte[] buffer = new byte[blocksize];
                        long totalBytes = 0;
                        using (SqlFileStream sqlFileStream = new SqlFileStream(filePath, txContext, FileAccess.Read))
                        {

                            int numBytes = 0;

                            sqlFileStream.Seek(from, SeekOrigin.Begin);
                            totalBytes = sqlFileStream.Length;

                            buffer = new byte[blocksize];
                            numBytes = await sqlFileStream.ReadAsync(buffer, 0, buffer.Length);

                            if (numBytes != 0)
                                Console.WriteLine("Total buffer read: " + numBytes);

                            //Because reading and writing are finished, FILESTREAM 
                            //must be closed. This closes the c# FileStream class, 
                            //but does not necessarily close the the underlying 
                            //FILESTREAM handle. 
                        }

                        //The final step is to commit or roll back the read and write
                        //operations that were performed on the FILESTREAM BLOB.

                        sqlCommand.Transaction.Commit();
                        return new Video() { VideoBytes = buffer, TotalLength = totalBytes };
                    }
                    catch (System.Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                        throw new System.Exception(ex.Message);
                    }
                }
            }
        }
    }
}
