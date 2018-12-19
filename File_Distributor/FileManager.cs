using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data.SqlClient;
using System.Configuration;

namespace File_Distributor
{
  public class FileManager
  {
    public void FIleWatcher()
    {

      //Enable Config file
      var appSettings = ConfigurationManager.AppSettings;
      var source = appSettings["sourceFilePath"] ?? null;
      var target = appSettings["destFilePath"] ?? null;


      while (true)
      {
        using (var folderWatcher = new FileSystemWatcher(source))
        {

          folderWatcher.Filter = "*.*";

          var change = folderWatcher.WaitForChanged(WatcherChangeTypes.Created, 100 * 60);

          if (!change.TimedOut)
          {
            Console.WriteLine("File detected: " + change.Name);
            Console.WriteLine("Copy to: " + target);
            File.Copy(Path.Combine(source, change.Name), Path.Combine(target, "Copied_" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + change.Name));
          }
        }
      }
    }
  }
}

