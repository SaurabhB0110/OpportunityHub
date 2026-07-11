using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace OpportunityHub.Services;

/// <summary>
/// Abstraction for S3 file uploads.
/// </summary>
public interface IS3Service
{
    /// <summary>
    /// Uploads the provided file to the specified folder (prefix) in S3 and returns the public URL.
    /// The implementation generates a GUID filename and preserves the original file extension.
    /// </summary>
    /// <param name="file">IFormFile to upload</param>
    /// <param name="folder">Folder/prefix inside the bucket (e.g. "resumes", "company-logos")</param>
    /// <returns>Public S3 URL of the uploaded object</returns>
    Task<string> UploadFileAsync(IFormFile file, string folder);
}