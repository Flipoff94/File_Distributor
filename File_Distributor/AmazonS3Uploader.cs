using System;
using System.ComponentModel;
using System.IO;
using Amazon.S3;
using Amazon.S3.Model;

namespace File_Distributor
{
  public class AmazonS3Uploader : BackgroundWorker
  {
    private string bucketName = "arn:aws:s3:::ppc-opsi";
    private string keyName = "*.pdf";
    private string filePath = @"C:\DEV\Flip\Mail_FIle_Distributor\TestFolder\Source";

    public void UploadFile()
    {
      var client = new AmazonS3Client(Amazon.RegionEndpoint.USEast1);

      try
      {
        PutObjectRequest putRequest = new PutObjectRequest
        {
          BucketName = bucketName,
          Key = keyName,
          FilePath = filePath,
          ContentType = "text/plain"
        };

        PutObjectResponse response = client.PutObject(putRequest);
      }
      catch (AmazonS3Exception amazonS3Exception)
      {
        if (amazonS3Exception.ErrorCode != null &&
            (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId")
            ||
            amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
        {
          throw new Exception("Check the provided AWS Credentials.");
        }
        else
        {
          throw new Exception("Error occurred: " + amazonS3Exception.Message);
        }
      }
    }

    #region Public Events

    /// <summary>
    /// Event that fired to stop the service.
    /// </summary>
    public event EventHandler<ErrorEventArgs> StopService;

    #endregion


  }
}