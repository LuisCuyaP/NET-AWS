namespace Appointment.Application.Abstractions.Archives;

public interface IBlobStorageService
{
    Task UploadBlobAsync(string blobName, Stream content, string contentType);
    Task<Stream?> DownloadBlobAsync(string blobName);
    Task DeleteBlobAsync(string blobName);
    Task<IEnumerable<string>> GetBlobAsync();
}
