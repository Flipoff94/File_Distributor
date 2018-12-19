using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Runtime.InteropServices;
using NLog;
using System.IO;
using System.Configuration;

namespace File_Distributor
{
  public partial class FileDistributor : ServiceBase
  {

    #region Constructor
    public FileDistributor()
    {
      InitializeComponent();
      logger = LogManager.GetCurrentClassLogger(); 
    }
    #endregion Constructor

    #region Public Event

    /// <summary>
    /// Stop the service
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public void StopService(object sender, ErrorEventArgs e)
    {
      if (!stopRequested)
      {
        Exception ex = e.GetException();

        logger.DebugException("Stop requested by worker.", ex);

        this.EventLog.WriteEntry("Error occurred running service.\n" + ex.Message, EventLogEntryType.Error);
        ExitCode = 1066;
        Stop();
      }
    }

    #endregion Public Event

    #region Protected Methods

    protected override void OnStart(string[] args)
    {
      try
      {
        logger.Debug("Service Started.");
        try
        {
          logger.Debug("Exe Version: " +
            System.Diagnostics.FileVersionInfo.GetVersionInfo(
            System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion);
          logger.Debug("Automatic Build Version: " +
            System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());
          logger.Debug("User: " + Environment.UserName);
          logger.Debug("Machine name: " + Environment.MachineName);
        }
        catch (Exception)
        { logger.Debug("Failed Logging."); }
        String executingDir = AppDomain.CurrentDomain.BaseDirectory;
        DirectoryInfo executingDirInfo = new DirectoryInfo(executingDir);

        String defaultSettingsFileLocation = executingDirInfo.Parent.Parent.FullName + "\\Settings.config";
        logger.Debug("Creating Back Ground Worker.");

        //Enable Config file
        var appSettings = ConfigurationManager.AppSettings;


        //Amazon Upload Method calling here
        uploader = new AmazonS3Uploader();
        uploader.StopService += new EventHandler<ErrorEventArgs>(StopService);
        uploader.RunWorkerAsync();



        while (true)
        {
          //Mailer Call
          string fileName = appSettings[""] ?? null; //check what to do here
          string sourcePath = appSettings["sourceFilePath"] ?? null;  //ATTENTION >>>>>>>>>> Maybe change to new file loction for when file was copied to new location...... 
          string recipient = appSettings["mailerRecipients"] ?? null;
          string emailSubject = appSettings["mailerEmailSubject"] ?? null;
          string emailBody = appSettings["mailerEmailBody" + DateTime.Now.ToLongDateString()] ?? null;

          string sourceFile = System.IO.Path.Combine(sourcePath, fileName);
          mailer = new Mailer();
          mailer.StartStuff();
          var savedPath = sourceFile;
          System.Net.Mail.Attachment attachFileTxt = new System.Net.Mail.Attachment(savedPath);
          mailer.SendEmailORD(recipient, emailSubject, emailBody, new System.Net.Mail.Attachment[1] { attachFileTxt });
        }

      }
      catch (Exception E)
      {
        logger.FatalException("Service cannot start.", E);
      }
    }

    protected override void OnStop()
    {
      try
      {
        stopRequested = true;

        this.RequestAdditionalTime(5000);
        logger.Debug("Stopping service...");

        if (uploader != null)
        {
          // Unwire the listening to the stopping of the service.
          uploader.WorkerSupportsCancellation = true;
          uploader.StopService -= new EventHandler<ErrorEventArgs>(StopService);

          uploader.CancelAsync();
        }
        logger.Debug("Service stopped.");
      }
      catch (Exception E)
      {
        logger.FatalException("Cannot stop service.", E);
      }
    }

    #endregion Protected Methods



    #region Private Variables

    /// <summary>
    /// NLog logger.
    /// </summary>
    private static Logger logger;

    /// <summary>
    /// boolean variable to check if the stop service was requested or not.
    /// </summary>
    private bool stopRequested;

    /// <summary>
    /// The Amazon service worker.
    /// </summary>
    //private OSOutboundServiceWorker worker;
    private AmazonS3Uploader uploader;

    /// <summary>
    /// The Mailer service worker.
    /// </summary>
    private Mailer mailer;
    #endregion Private Variables
  }
}
