using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace WebApiVideoStreaming.Support
{
    public class FileStreamer
    {
        private static string connectionStringFS => ConfigurationManager.ConnectionStrings["StreamingFS"].ConnectionString;
        public FileInfo FileInfo { get; set; }
        public long Start { get; set; }
        public long End { get; set; }
        public long TotalLength { get; set; }
        public string Name { get; set; } 

        public async Task WriteToStream(Stream outputStream, HttpContent content, TransportContext context)
        {
            try
            {
                var buffer = new byte[65536];
                using (var video = FileInfo.OpenRead())
                {
                    if (End == -1)
                    {
                        End = video.Length;
                    }
                    var position = Start;
                    var bytesLeft = End - Start + 1;
                    video.Position = Start;
                    while (position <= End)
                    {
                        // what should i do here?
                        var bytesRead = video.Read(buffer, 0, (int)Math.Min(bytesLeft, buffer.Length));
                        await outputStream.WriteAsync(buffer, 0, bytesRead);
                        position += bytesRead;
                        bytesLeft = End - position + 1;
                    }
                }
            }
            catch (Exception ex)
            {
                // fail silently
            }
            finally
            {
                outputStream.Close();
            }
        }

        public async Task WriteSqlToStream(Stream outputStream, HttpContent content, TransportContext context)
        {
            var blocksize = 1024 * 512;
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
                        sqlCommand.Parameters.AddWithValue("name", Name);

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
                        TotalLength = 0;
                        using (SqlFileStream sqlFileStream = new SqlFileStream(filePath, txContext, FileAccess.Read))
                        {

                            int numBytes = 0;

                            sqlFileStream.Position = Start;
                            TotalLength = sqlFileStream.Length;
                            var count = TotalLength > blocksize ? blocksize : TotalLength;

                            //buffer = new byte[blocksize];
                            numBytes = await sqlFileStream.ReadAsync(buffer, 0, (int)count);

                            if (numBytes != 0)
                                Console.WriteLine("Total buffer read: " + numBytes);

                            //Because reading and writing are finished, FILESTREAM 
                            //must be closed. This closes the c# FileStream class, 
                            //but does not necessarily close the the underlying 
                            //FILESTREAM handle. 
                            await outputStream.WriteAsync(buffer, 0, numBytes);
                            TotalLength -= numBytes;
                        }

                        //The final step is to commit or roll back the read and write
                        //operations that were performed on the FILESTREAM BLOB.

                        sqlCommand.Transaction.Commit();
                        outputStream.Close();
                        //return new Video() { VideoBytes = buffer, TotalLength = totalBytes, Start = from };
                    }
                    catch (System.Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                        throw new System.Exception(ex.Message);
                    }
                }
            }
        }


        public async Task GetLength(string name)
        {
            Name = name;
            using (SqlConnection sqlConnection = new SqlConnection(connectionStringFS))
            {
                using (SqlCommand sqlCommand = new SqlCommand { Connection = sqlConnection })
                {
                    try
                    {
                        await sqlConnection.OpenAsync();
                        sqlCommand.CommandText = "SELECT Stream.PathName() FROM [Videos] WHERE [Name] = @name";
                        sqlCommand.Parameters.AddWithValue("name", Name);
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
                        SqlTransaction transaction = sqlConnection.BeginTransaction("mainTransaction");
                        sqlCommand.Transaction = transaction;
                        sqlCommand.CommandText = "SELECT GET_FILESTREAM_TRANSACTION_CONTEXT()";
                        Object obj = await sqlCommand.ExecuteScalarAsync();
                        byte[] txContext = (byte[])obj;
                        using (SqlFileStream sqlFileStream = new SqlFileStream(filePath, txContext, FileAccess.Read))
                        {
                            TotalLength = sqlFileStream.Length;
                        }
                        sqlCommand.Transaction.Commit();
                    }
                    catch (System.Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                        throw new System.Exception(ex.Message);
                    }
                }
            }
        }


        public async Task<Stream> GetStream()
        {
            var blocksize = 1024 * 512;
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
                        sqlCommand.Parameters.AddWithValue("name", Name);

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
                        TotalLength = 0;
                        SqlFileStream sqlFileStream = new SqlFileStream(filePath, txContext, FileAccess.Read);
                        //The final step is to commit or roll back the read and write
                        //operations that were performed on the FILESTREAM BLOB.        
                        sqlFileStream.Close();
                        sqlCommand.Transaction.Commit();
                        return sqlFileStream;
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