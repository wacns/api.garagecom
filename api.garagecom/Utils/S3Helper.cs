using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace api.garagecom.Utils;

public abstract class S3Helper
{
    private static readonly string AccessKey = Environment.GetEnvironmentVariable("S3_ACCESS_KEY")!;
    private static readonly string SecretKey = Environment.GetEnvironmentVariable("S3_SECRET_KEY")!;
    private static readonly string BucketName = Environment.GetEnvironmentVariable("S3_BUCKET_NAME")!;
    private static readonly string ServiceUrl = Environment.GetEnvironmentVariable("S3_SERVICE_URL")!;

    private static IAmazonS3 CreateClient()
    {
        var credentials = new BasicAWSCredentials(AccessKey, SecretKey);

        var s3Client = new AmazonS3Client(credentials, new AmazonS3Config
        {
            ServiceURL = ServiceUrl,
            ForcePathStyle = true,
            UseHttp = false,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        });
        return s3Client;
    }

    public static async Task<bool> UploadAttachmentAsync(IFormFile attachment, string attachmentId, string path)
    {
        using var client = CreateClient();
        try
        {
            using var memoryStream = new MemoryStream();
            await attachment.CopyToAsync(memoryStream);
            var extension = Path.GetExtension(attachment.FileName);
            var request = new PutObjectRequest
            {
                InputStream = memoryStream,
                BucketName = BucketName,
                ContentType = "binary/octet-stream",
                Key = path + attachmentId + extension,
                DisablePayloadSigning = true
            };
            return (await client.PutObjectAsync(request)).HttpStatusCode == HttpStatusCode.OK ? true : false;
        }
        catch (AmazonS3Exception ex)
        {
            Console.WriteLine($"Error encountered on server. Message:'{ex.Message}' when uploading object");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unknown encountered on server. Message:'{ex.Message}' when uploading object");
        }

        return false;
    }

    public static Task<byte[]> DownloadAttachmentAsync(string key, string path)
    {
        using (var client = CreateClient())
        {
            try
            {
                var request = new GetObjectRequest
                {
                    BucketName = BucketName,
                    Key = path + key
                };
                using var response = client.GetObjectAsync(request).Result;
                using var stream = response.ResponseStream;
                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                return Task.FromResult(memoryStream.ToArray());
            }
            catch (AmazonS3Exception ex)
            {
                Console.WriteLine($"Error encountered on server. Message:'{ex.Message}' when downloading object");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unknown encountered on server. Message:'{ex.Message}' when downloading object");
            }
        }

        return Task.FromResult(Array.Empty<byte>());
    }
}