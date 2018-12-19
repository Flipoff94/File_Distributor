using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Threading;
using NLog;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Collections.ObjectModel;

namespace File_Distributor
{
  class Mailer
  {
    public void InitApp()
    {
      var appSettings = ConfigurationManager.AppSettings;
      string mailServerE = appSettings["MailerSmtp"] ?? null;
      string mailLogin = appSettings["MailerLogin"] ?? null;
      string mailPass = appSettings["MailerPass"] ?? null;

      instanceName = appSettings["InstanceName"] ?? "Unknown Instance";

      extraFormatOptions = new Dictionary<string, string>();
      extraFormatOptions.Add("{{InstanceName}}", instanceName);

      scriptsPath = appSettings["ScriptsPath"] ?? "c:\\OrdScripts";
      if (String.IsNullOrWhiteSpace(scriptsPath))
      {
        scriptsPath = "c:\\OrdScripts";
      }

      emailLimitAtOnce = Convert.ToInt32(appSettings["EmailLimitAtOnce"] ?? "10");
      emailLimitPerRun = Convert.ToInt32(appSettings["EmailLimitPerRun"] ?? "10");

      generatingLimit = Convert.ToInt32(appSettings["generatingLimit"] ?? "4");


      mailServer = mailServerE;
      if (!String.IsNullOrWhiteSpace(mailLogin) && !String.IsNullOrWhiteSpace(mailPass))
      {
        mailerLogin = new NetworkCredential(mailLogin, mailPass);
      }
      else
      {
        mailerLogin = null;
      }

      //MailerEmailAddress
      if (!String.IsNullOrWhiteSpace(appSettings["MailerAddress"]))
      {
        mailerSender = appSettings["MailerAddress"];
      }
      else
      {
        mailerSender = "";
      }

      //MailerDisplayName
      if (!String.IsNullOrWhiteSpace(appSettings["MailerDisplayName"]))
      {
        mailerDisplayName = appSettings["MailerDisplayName"];
      }
      else
      {
        mailerDisplayName = null;
      }

      //MailerSSL
      if (!String.IsNullOrWhiteSpace(appSettings["MailerSSL"]))
      {
        mailerSSL = Convert.ToBoolean(appSettings["MailerSSL"]);
      }
      else
      {
        mailerSSL = false;
      }

      //MailerPort
      if (!String.IsNullOrWhiteSpace(appSettings["MailerPort"]))
      {
        mailerPort = Convert.ToInt32(appSettings["MailerPort"]);
      }
      else
      {
        mailerPort = 25;
      }

      //ReplyTo
      if (!String.IsNullOrWhiteSpace(appSettings["ReplyTo"]))
      {
        replyTo = Convert.ToString(appSettings["ReplyTo"]);
      }
      else
      {
        replyTo = null;
      }

      //ExchangeServiceUrl
      if (!String.IsNullOrWhiteSpace(appSettings["ExchangeServiceUrl"]))
      {
        exchangeServerUrl = Convert.ToString(appSettings["ExchangeServiceUrl"]);
      }
      else
      {
        exchangeServerUrl = null;
      }


      //ExchangePass
      if (!String.IsNullOrWhiteSpace(appSettings["ExchangePass"]))
      {
        exchangePass = Convert.ToString(appSettings["ExchangePass"]);
      }
      else
      {
        exchangePass = null;
      }



      allMailingThreads = new SemaphoreSlim(emailLimitAtOnce);
      allGeneratingThreads = new SemaphoreSlim(generatingLimit);
      allFtpThreads = new SemaphoreSlim(ftpLimit);

      emailLimit = new SemaphoreSlim(emailLimitPerRun);

      exchangeRead = new SemaphoreSlim(4);

      if (!String.IsNullOrWhiteSpace(appSettings["ConfigSet"]))
      {
        configSet = appSettings["ConfigSet"];
      }
      else
      {
        configSet = "";
      }

      if (!String.IsNullOrWhiteSpace(appSettings["MessageTags"]))
      {
        messageTags = appSettings["MessageTags"];
      }
      else
      {
        messageTags = "";
      }

      if (!String.IsNullOrWhiteSpace(appSettings["IncludeMetaInFilename"]))
      {
        includeMeta = Convert.ToBoolean(appSettings["IncludeMetaInFilename"]);
      }
      else
      {
        includeMeta = true;
      }
    }

    public int StartStuff()
    {
      InitApp();
      Collection<System.Threading.Tasks.Task> allTasks = new Collection<System.Threading.Tasks.Task>();
      Collection<System.Threading.Tasks.Task> allGenTasks = new Collection<System.Threading.Tasks.Task>();
      Collection<System.Threading.Tasks.Task> allMailTasks = new Collection<System.Threading.Tasks.Task>();

      try
      {
        logger.Info("Waiting for all generating thread to finish.");
        System.Threading.Tasks.Task.WaitAll(allGenTasks.ToArray());
        logger.Info("All generation finished with no errors.");
      }
      catch (AggregateException ex)
      {
        string errorList = "";
        foreach (Exception e in ex.InnerExceptions)
        {
          errorList += e.Message + Environment.NewLine;
        }
        logger.Info("Some generation finished with errors... " + Environment.NewLine + errorList);
      }

      try
      {
        logger.Info("Waiting for all distribution threads to finish.");
        System.Threading.Tasks.Task.WaitAll(allMailTasks.ToArray());
        logger.Info("All distribution finished with no errors.");
      }
      catch (AggregateException ex)
      {
        bool notCancel = false;
        string errorList = "";
        foreach (var e in ex.InnerExceptions)
        {
          if (!(e is OperationCanceledException))
          {
            notCancel = true;
          }
          errorList += e.Message + Environment.NewLine;
        }
        if (notCancel)
        {
          logger.Info("Some distribution finished with errors..." + Environment.NewLine + errorList);
        }
        else
        {
          logger.Info("All distribution finished with no errors.");
        }

      }
      return 999999;
    }



    public void SendEmailORD(string recipients, string subject, string body, System.Net.Mail.Attachment[] attachment)
    {
      //mailServer is SMTP server address
      if (!String.IsNullOrWhiteSpace(mailServer))
      {
        using (SmtpClient mailerSmtp = new SmtpClient(mailServer))
        {
          //create ascii encoder
          ASCIIEncoding ascii = new ASCIIEncoding();


          mailerSmtp.EnableSsl = mailerSSL; //email over ssl
          mailerSmtp.Port = mailerPort; //server port to use
          if (mailerLogin != null)
          {
            mailerSmtp.Credentials = mailerLogin; // login/password
          }
          MailMessage mail = new MailMessage(); //create new message
          mail.From = new MailAddress(mailerSender, mailerDisplayName); //message sender, if mailerDisplayName is empty or null, then it will show email address instead (default).
          mail.To.Add(recipients); //emails of message recipients
          mail.Body = body; //body of message
          mail.Subject = subject; //subject of message
          mail.IsBodyHtml = true; //use plain text or use HTML for formatting contents of the message

          //additional headers. Must be explicitly converted to ASCII
          if (!String.IsNullOrWhiteSpace(configSet))
          {
            mail.Headers.Add(ascii.GetString(ascii.GetBytes("X-SES-CONFIGURATION-SET")), ascii.GetString(ascii.GetBytes(configSet))); //config set
          }
          if (!String.IsNullOrWhiteSpace(messageTags))
          {
            mail.Headers.Add(ascii.GetString(ascii.GetBytes("X-SES-MESSAGE-TAGS")), ascii.GetString(ascii.GetBytes(messageTags))); //tag list
          }

          //attach attachments
          if (attachment != null)
          {
            foreach (var e in attachment)
            {
              mail.Attachments.Add(e);
            }
          }
          if (!String.IsNullOrWhiteSpace(replyTo))
          {
            mail.ReplyToList.Add(new MailAddress(replyTo));
          }

          mailerSmtp.Send(mail); //send

        }
      }
    }


    #region Privates
    private static Logger logger = LogManager.GetCurrentClassLogger();

    private string dbConnString;

    private string mailerSender;
    private string mailerDisplayName;
    private string mailServer;
    private NetworkCredential mailerLogin;
    private bool mailerSSL;
    private int mailerPort;
    private string replyTo;
    private string exchangeServerUrl;
    private string exchangePass;

    private int emailLimitPerRun;

    private int emailLimitAtOnce;
    private int generatingLimit;
    private int ftpLimit;

    private bool allowScripts;

    private SemaphoreSlim allMailingThreads;
    private SemaphoreSlim allGeneratingThreads;
    private SemaphoreSlim allFtpThreads;

    private SemaphoreSlim emailLimit;
    private SemaphoreSlim exchangeRead;


    private bool sslEnabled;

    private string instanceName;

    private string scriptsPath;

    private string configSet;
    private string messageTags;

    private int bounceRetentionDays = 14;

    Dictionary<string, string> extraFormatOptions;

    private bool includeMeta;
    #endregion Privates
  }
}
