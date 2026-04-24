
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Appointment.Application.Abstractions.Archives;

namespace Appointment.Infrastructure.Abstractions.Archives;

internal sealed class BlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _blobContainerClient;

    public BlobStorageService(string connectionString, string containerName)
    {
        BlobServiceClient blobServiceClient = new(connectionString);
        _blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
    }

    public async Task UploadBlobAsync(string blobName, Stream content, string contentType)
    {
        await _blobContainerClient.CreateIfNotExistsAsync();
        var blobClient = _blobContainerClient.GetBlobClient(blobName);
        var blobHttpHeaders = new BlobHttpHeaders { ContentType = contentType };
        await blobClient.UploadAsync(content, new BlobUploadOptions { HttpHeaders = blobHttpHeaders });
    }

    public async Task<Stream?> DownloadBlobAsync(string blobName)
    {
        var blobClient = _blobContainerClient.GetBlobClient(blobName);
        if(await blobClient.ExistsAsync())
        {
            var downloadInfo = await blobClient.DownloadAsync();
            return downloadInfo.Value.Content;
        }
        return null;
    }

    public async Task DeleteBlobAsync(string blobName)
    {
        var blobClient = _blobContainerClient.GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync();
    }

    public async Task<IEnumerable<string>> GetBlobAsync()
    {
        HashSet<string> archivos = [];
        await foreach (var item in _blobContainerClient.GetBlobsAsync())
        {
            archivos.Add(item.Name);
        }
        return archivos;
    }


}