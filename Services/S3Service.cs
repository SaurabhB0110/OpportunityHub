using System;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OpportunityHub.Services;

public class S3Service : IS3Service, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly AwsOptions _options;
    private readonly ILogger<S3Service> _logger;

    public S3Service(IOptions<AwsOptions> options, ILogger<S3Service> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // If credentials are provided use them; otherwise allow SDK to use the default credential chain (instance role, env vars, etc.)
        if (!string.IsNullOrWhiteSpace(_options.AccessKey) && !string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            var credentials = new BasicAWSCredentials(_options.AccessKey, _options.SecretKey);
            var region = RegionEndpoint.GetBySystemName(_options.Region);
            _client = new AmazonS3Client(credentials, region);
        }
        else
        {
            // Rely on the SDK's default credential provider (recommended for production e.g., IAM role)
            var region = RegionEndpoint.GetBySystemName(_options.Region);
            _client = new AmazonS3Client(region);
            _logger.LogInformation("AWS credentials not provided in configuration; using default credential chain.");
        }
    }

    /// <inheritdoc />
    public async Task<string> UploadFileAsync(IFormFile file, string folder)
    {
        if (file == null) throw new ArgumentNullException(nameof(file));
        if (string.IsNullOrWhiteSpace(folder)) folder = "uploads";

        var ext = Path.GetExtension(file.FileName) ?? string.Empty;
        var fileName = $"{Guid.NewGuid()}{ext}";
        var key = $"{folder.Trim().TrimStart('/').TrimEnd('/')}/{fileName}";

        try
        {
            using var stream = file.OpenReadStream();
            var transferUtility = new TransferUtility(_client);
            var request = new TransferUtilityUploadRequest
            {
                InputStream = stream,
                Key = key,
                BucketName = _options.BucketName,
                ContentType = file.ContentType // preserve content type for correct serving
                // NOTE: Do NOT set CannedACL or any ACL here so the upload is compatible with
                // "Bucket owner enforced (ACLs disabled)" object ownership configuration.
            };

            await transferUtility.UploadAsync(request).ConfigureAwait(false);

            // Construct the public URL (virtual-hosted–style). If your objects are private, switch to pre-signed URLs.
            var url = $"https://{_options.BucketName}.s3.{_options.Region}.amazonaws.com/{key}";
            return url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "S3 upload failed for key {Key}", key);
            throw;
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}