using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;

namespace BulletinBoard.Api.Services;

/// <summary>
/// Issues short-lived SAS URLs so browsers upload/read listing images directly,
/// keeping large payloads off the Function App.
/// In Azure: user-delegation SAS via Managed Identity.
/// Locally (Azurite): account-key SAS via a connection string.
/// </summary>
public class BlobService
{
    private readonly BlobServiceClient _service;
    private readonly BlobContainerClient _container;
    private readonly bool _useUserDelegation;
    public static readonly string[] AllowedContentTypes =
        { "image/jpeg", "image/png", "image/webp", "image/gif" };
    public const long MaxBytes = 10 * 1024 * 1024; // 10 MB

    public BlobService(IConfiguration config)
    {
        var containerName = config["Blob:ContainerName"] ?? "listing-images";
        var localConn = config["Blob:LocalConnectionString"];

        if (!string.IsNullOrEmpty(localConn))
        {
            // Local development against Azurite.
            _service = new BlobServiceClient(localConn);
            _container = _service.GetBlobContainerClient(containerName);
            _container.CreateIfNotExists();
            _useUserDelegation = false;
        }
        else
        {
            var serviceUri = new Uri(config["Blob:ServiceUri"]!);
            var clientId = config["Blob:ManagedIdentityClientId"];
            var credential = string.IsNullOrEmpty(clientId)
                ? new DefaultAzureCredential()
                : new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = clientId });
            _service = new BlobServiceClient(serviceUri, credential);
            _container = _service.GetBlobContainerClient(containerName);
            _useUserDelegation = true;
        }
    }

    public string NewBlobName(Guid ownerId, string extension) =>
        $"{ownerId}/{Guid.NewGuid():N}{extension}";

    /// <summary>A write-only SAS the browser uses to PUT the image.</summary>
    public async Task<string> CreateUploadUrlAsync(string blobName, TimeSpan validFor)
    {
        var blob = _container.GetBlobClient(blobName);
        return await CreateSasAsync(blob, BlobSasPermissions.Write | BlobSasPermissions.Create, validFor);
    }

    /// <summary>A read-only SAS used to render an image.</summary>
    public async Task<string> CreateReadUrlAsync(string blobName, TimeSpan validFor)
    {
        var blob = _container.GetBlobClient(blobName);
        return await CreateSasAsync(blob, BlobSasPermissions.Read, validFor);
    }

    public async Task DeleteAsync(string blobName) =>
        await _container.DeleteBlobIfExistsAsync(blobName);

    private async Task<string> CreateSasAsync(BlobClient blob, BlobSasPermissions permissions, TimeSpan validFor)
    {
        var builder = new BlobSasBuilder
        {
            BlobContainerName = _container.Name,
            BlobName = blob.Name,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresOn = DateTimeOffset.UtcNow.Add(validFor)
        };
        builder.SetPermissions(permissions);

        if (_useUserDelegation)
        {
            var key = await _service.GetUserDelegationKeyAsync(
                DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.Add(validFor));
            var sas = builder.ToSasQueryParameters(key, _service.AccountName).ToString();
            return $"{blob.Uri}?{sas}";
        }

        // Account-key SAS (Azurite / local).
        return blob.GenerateSasUri(builder).ToString();
    }
}
