using OS.DBLogger;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace File_Distributor
{
  public class Save2DB
  {
    #region  Public Classes
    public static void databaseFilePut(string varFilePath)
    {
      try
      {
        byte[] file;
        using (var stream = new FileStream(varFilePath, FileMode.Open, FileAccess.Read))
        {
          using (var reader = new BinaryReader(stream))
          {
            file = reader.ReadBytes((int)stream.Length);
          }
        }
      }
      catch(Exception ex)
      {
        Console.WriteLine("This didn't go as planned: " + ex.StackTrace + Environment.NewLine + ex.Message);
      }
      try
      {
        ////Update Was Status in WebSchPublishedTrip
        string connectionString = @"Data Source = OPSI-LT-128\SQLEXPRESS; Initial Catalog = PPC_Files; Integrated Security = False;";
        string updateQuery = "INSERT INTO PPC_PODs (PODFileByte) Values(@File)" + connectionString;

        try
        {
          using (SqlCommand command = new SqlCommand(updateQuery, new SqlConnection(connectionString)))
          {
            command.Connection.Open();
            command.ExecuteNonQuery();
            command.Connection.Close();
            Console.WriteLine(Environment.NewLine + "File inserted into DB ");
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine(Environment.NewLine + "It didnt really update :( " +
          Environment.NewLine + ex.Message + ex.StackTrace + Environment.NewLine);
          log.Log(UserID, "Error Updating message in DB - PPC_PODs", LogType.Error, "updateQuery");
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine("This didn't go as planned: " + ex.StackTrace + Environment.NewLine + ex.Message);
      }
    }

    #endregion Public Classes

    #region Private Classes
    private static LogController log;
    private static int UserID;
    #endregion Private Classes
  }
}
