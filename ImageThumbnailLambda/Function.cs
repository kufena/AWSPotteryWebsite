using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Amazon.SimpleSystemsManagement;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ImageThumbnailLambda;

public class Function

{
    string TargetBucketParameterName = "/potterywebsitesolution/thumbnailfunction/targetbucket";

    IAmazonS3 S3Client { get; set; }
    String TargetBucket = String.Empty;

    /// <summary>
    /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
    /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
    /// region the Lambda function is executed in.
    /// </summary>
    public Function()
    {
        S3Client = new AmazonS3Client();
    }

    private async Task fetchTargetBucketParameter()
    {
        using (var client = new AmazonSimpleSystemsManagementClient())
        {
            var response = await client.GetParameterAsync(new Amazon.SimpleSystemsManagement.Model.GetParameterRequest { Name = TargetBucketParameterName });
            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                TargetBucket = response.Parameter.Value;
            }
        }
    }

    /// <summary>
    /// Constructs an instance with a preconfigured S3 client. This can be used for testing the outside of the Lambda environment.
    /// </summary>
    /// <param name="s3Client"></param>
    public Function(IAmazonS3 s3Client, String targetBucket)
    {
        this.S3Client = s3Client;
        this.TargetBucket = targetBucket;
    }
    
    /// <summary>
    /// This method is called for every Lambda invocation. This method takes in an S3 event object and can be used 
    /// to respond to S3 notifications.
    /// </summary>
    /// <param name="evnt"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task<string?> FunctionHandler(S3Event evnt, ILambdaContext context)
    {

        if (TargetBucket == String.Empty)
        {
            await fetchTargetBucketParameter();
        }
        context.Logger.LogInformation($"Target bucket for our lambda is {TargetBucket}");

        var s3Event = evnt.Records?[0].S3;
        if(s3Event == null)
        {
            return null;
        }

        try
        {
            var response = await this.S3Client.GetObjectMetadataAsync(s3Event.Bucket.Name, s3Event.Object.Key);
            string contentType = response.Headers.ContentType;

            context.Logger.LogInformation($"Content type of object is {contentType}");

            if (contentType.StartsWith("image"))
            {
                //var req = new GetObjectRequest() { BucketName = s3Event.Bucket.Name, Key = s3Event.Object.Key };

                var obj = await this.S3Client.GetObjectAsync(s3Event.Bucket.Name, s3Event.Object.Key);

                if (obj == null)
                {
                    context.Logger.LogInformation("ok we've got a null from get object async.");
                    return null;
                }

                context.Logger.LogInformation($"Code is {obj.HttpStatusCode}");

                if (obj.HttpStatusCode != System.Net.HttpStatusCode.OK)
                {
                    context.Logger.LogError($"Not an OK repsone when retrieving {s3Event.Bucket.Name}/{s3Event.Object.Key}");
                    return null;
                }

                using (var instr = obj.ResponseStream)
                {
                    if (instr == null)
                    {
                        context.Logger.LogError("Null object response");
                        return null;
                    }


                    Image img = Image.Load(instr);
                    Image thumb = img.Clone(x => x.Resize(128, 128));

                    // copy original
                    MemoryStream ims = new MemoryStream();
                    img.Save(ims, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
                    var imgsave = new PutObjectRequest
                    {
                        BucketName = TargetBucket,
                        Key = s3Event.Object.Key,
                        InputStream = ims,
                        ContentType = response.Headers.ContentType
                    };
                    var imgsaveresp = await this.S3Client.PutObjectAsync(imgsave);

                    // store thumb
                    MemoryStream tms = new MemoryStream();
                    thumb.Save(tms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
                    var thumbsave = new PutObjectRequest
                    {
                        BucketName = TargetBucket,
                        Key = "thumb." + s3Event.Object.Key,
                        InputStream = tms,
                        ContentType = response.Headers.ContentType
                    };
                    var thumbsaveresp = await this.S3Client.PutObjectAsync(thumbsave);


                }
            }
            else
            {

                var copyResponse = await S3Client.CopyObjectAsync(new CopyObjectRequest
                {
                    SourceBucket = s3Event.Bucket.Name,
                    SourceKey = s3Event.Object.Key,
                    DestinationBucket = TargetBucket,
                    DestinationKey = s3Event.Object.Key
                });
                context.Logger.LogInformation($"Copied non-image file {copyResponse.HttpStatusCode}");
            }            

            return response.Headers.ContentType;
        }
        catch(Exception e)
        {
            context.Logger.LogInformation($"Error getting object {s3Event.Object.Key} from bucket {s3Event.Bucket.Name}. Make sure they exist and your bucket is in the same region as this function.");
            context.Logger.LogInformation(e.Message);
            context.Logger.LogInformation(e.StackTrace);
            throw;
        }
    }
}