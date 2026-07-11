namespace OpportunityHub.Services;

/// <summary>
/// AWS configuration bound from configuration (appsettings.json / env).
/// </summary>
public class AwsOptions
{
    public string Region { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
}