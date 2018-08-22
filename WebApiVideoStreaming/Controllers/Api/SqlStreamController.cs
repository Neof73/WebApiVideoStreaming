using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Xml;

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
                                //using (FileStream file = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                                //{
                                data = reader.GetSqlBinary(0).Value;
                                //SaveVideoFile(Guid.NewGuid().ToString(), "" , data );
                                return data;
                                
                            }
                        }
                    }
                }
            }
            return data;
        }

        private static void SaveVideoFile
          (string clientPath, string serverPath, byte[] serverTxn)
        {
            const int BlockSize = 1024 * 512;

            using (FileStream source = new FileStream(clientPath, FileMode.Open, FileAccess.Read))
            {
                using (SqlFileStream dest = new SqlFileStream(serverPath, serverTxn, FileAccess.Write))
                {
                    byte[] buffer = new byte[BlockSize];
                    int bytesRead;
                    while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        dest.Write(buffer, 0, bytesRead);
                        dest.Flush();
                    }
                    dest.Close();
                }
                source.Close();
            }
        }


        // Application transferring a large Text File from SQL Server in .NET 4.5  
        private static async Task PrintTextValues()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (SqlCommand command = new SqlCommand("SELECT [id], [textdata] FROM [Streams]", connection))
                {

                    // The reader needs to be executed with the SequentialAccess behavior to enable network streaming  
                    // Otherwise ReadAsync will buffer the entire text document into memory which can cause scalability issues or even OutOfMemoryExceptions  
                    using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess))
                    {
                        while (await reader.ReadAsync())
                        {
                            Console.Write("{0}: ", reader.GetInt32(0));

                            if (await reader.IsDBNullAsync(1))
                            {
                                Console.Write("(NULL)");
                            }
                            else
                            {
                                char[] buffer = new char[4096];
                                int charsRead = 0;
                                using (TextReader data = reader.GetTextReader(1))
                                {
                                    do
                                    {
                                        // Grab each chunk of text and write it to the console  
                                        // If you are writing to a TextWriter you should use WriteAsync or WriteLineAsync  
                                        charsRead = await data.ReadAsync(buffer, 0, buffer.Length);
                                        Console.Write(buffer, 0, charsRead);
                                    } while (charsRead > 0);
                                }
                            }

                            Console.WriteLine();
                        }
                    }
                }
            }
        }

        // Application transferring a large Xml Document from SQL Server in .NET 4.5  
        private static async Task PrintXmlValues()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (SqlCommand command = new SqlCommand("SELECT [id], [xmldata] FROM [Streams]", connection))
                {

                    // The reader needs to be executed with the SequentialAccess behavior to enable network streaming  
                    // Otherwise ReadAsync will buffer the entire Xml Document into memory which can cause scalability issues or even OutOfMemoryExceptions  
                    using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess))
                    {
                        while (await reader.ReadAsync())
                        {
                            Console.WriteLine("{0}: ", reader.GetInt32(0));

                            if (await reader.IsDBNullAsync(1))
                            {
                                Console.WriteLine("\t(NULL)");
                            }
                            else
                            {
                                using (XmlReader xmlReader = reader.GetXmlReader(1))
                                {
                                    int depth = 1;
                                    // NOTE: The XmlReader returned by GetXmlReader does NOT support async operations  
                                    // See the example below (PrintXmlValuesViaNVarChar) for how to get an XmlReader with asynchronous capabilities  
                                    while (xmlReader.Read())
                                    {
                                        switch (xmlReader.NodeType)
                                        {
                                            case XmlNodeType.Element:
                                                Console.WriteLine("{0}<{1}>", new string('\t', depth), xmlReader.Name);
                                                depth++;
                                                break;
                                            case XmlNodeType.Text:
                                                Console.WriteLine("{0}{1}", new string('\t', depth), xmlReader.Value);
                                                break;
                                            case XmlNodeType.EndElement:
                                                depth--;
                                                Console.WriteLine("{0}</{1}>", new string('\t', depth), xmlReader.Name);
                                                break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // Application transferring a large Xml Document from SQL Server in .NET 4.5  
        // This goes via NVarChar and TextReader to enable asynchronous reading  
        private static async Task PrintXmlValuesViaNVarChar()
        {
            XmlReaderSettings xmlSettings = new XmlReaderSettings()
            {
                // Async must be explicitly enabled in the XmlReaderSettings otherwise the XmlReader will throw exceptions when async methods are called  
                Async = true,
                // Since we will immediately wrap the TextReader we are creating in an XmlReader, we will permit the XmlReader to take care of closing\disposing it  
                CloseInput = true,
                // If the Xml you are reading is not a valid document (as per http://msdn.microsoft.com/library/6bts1x50.aspx) you will need to set the conformance level to Fragment  
                ConformanceLevel = ConformanceLevel.Fragment
            };

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Cast the XML into NVarChar to enable GetTextReader - trying to use GetTextReader on an XML type will throw an exception  
                using (SqlCommand command = new SqlCommand("SELECT [id], CAST([xmldata] AS NVARCHAR(MAX)) FROM [Streams]", connection))
                {

                    // The reader needs to be executed with the SequentialAccess behavior to enable network streaming  
                    // Otherwise ReadAsync will buffer the entire Xml Document into memory which can cause scalability issues or even OutOfMemoryExceptions  
                    using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess))
                    {
                        while (await reader.ReadAsync())
                        {
                            Console.WriteLine("{0}:", reader.GetInt32(0));

                            if (await reader.IsDBNullAsync(1))
                            {
                                Console.WriteLine("\t(NULL)");
                            }
                            else
                            {
                                // Grab the row as a TextReader, then create an XmlReader on top of it  
                                // We are not keeping a reference to the TextReader since the XmlReader is created with the "CloseInput" setting (so it will close the TextReader when needed)  
                                using (XmlReader xmlReader = XmlReader.Create(reader.GetTextReader(1), xmlSettings))
                                {
                                    int depth = 1;
                                    // The XmlReader above now supports asynchronous operations, so we can use ReadAsync here  
                                    while (await xmlReader.ReadAsync())
                                    {
                                        switch (xmlReader.NodeType)
                                        {
                                            case XmlNodeType.Element:
                                                Console.WriteLine("{0}<{1}>", new string('\t', depth), xmlReader.Name);
                                                depth++;
                                                break;
                                            case XmlNodeType.Text:
                                                // Depending on what your data looks like, you should either use Value or GetValueAsync  
                                                // Value has less overhead (since it doesn't create a Task), but it may also block if additional data is required  
                                                Console.WriteLine("{0}{1}", new string('\t', depth), await xmlReader.GetValueAsync());
                                                break;
                                            case XmlNodeType.EndElement:
                                                depth--;
                                                Console.WriteLine("{0}</{1}>", new string('\t', depth), xmlReader.Name);
                                                break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public static async Task<Video> GetFileStream(string name, byte[] buffer, long from)
        {
            SqlConnection sqlConnection = new SqlConnection(connectionStringFS);

            SqlCommand sqlCommand = new SqlCommand { Connection = sqlConnection };

            try
            {
                await sqlConnection.OpenAsync();

                //The first task is to retrieve the file path
                //of the SQL FILESTREAM BLOB that we want to
                //access in the application.

                sqlCommand.CommandText =
                    "SELECT Stream.PathName()"
                    + " FROM [Videos]"
                    + " WHERE [Name] = '" + name + "'";

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

                SqlTransaction transaction = sqlConnection.BeginTransaction("mainTranaction");
                sqlCommand.Transaction = transaction;

                sqlCommand.CommandText =
                    "SELECT GET_FILESTREAM_TRANSACTION_CONTEXT()";

                Object obj = await sqlCommand.ExecuteScalarAsync();
                byte[] txContext = (byte[])obj;

                //The next step is to obtain a handle that
                //can be passed to the Win32 FILE APIs.

                SqlFileStream sqlFileStream = new SqlFileStream(filePath, txContext, FileAccess.Read);

                int numBytes = 0;

                sqlFileStream.Seek(from, SeekOrigin.Begin);
                long totalBytes = sqlFileStream.Length;

                numBytes = await sqlFileStream.ReadAsync(buffer, 0, buffer.Length);

                if (numBytes != 0)
                    Console.WriteLine("Total buffer read: " + numBytes);

                //Because reading and writing are finished, FILESTREAM 
                //must be closed. This closes the c# FileStream class, 
                //but does not necessarily close the the underlying 
                //FILESTREAM handle. 
                sqlFileStream.Close();

                //The final step is to commit or roll back the read and write
                //operations that were performed on the FILESTREAM BLOB.

                sqlCommand.Transaction.Commit();
                return new Video(){ VideoBytes = buffer, TotalLength = totalBytes };
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                sqlConnection.Close();
            }
            return null;
        }
    }
}
