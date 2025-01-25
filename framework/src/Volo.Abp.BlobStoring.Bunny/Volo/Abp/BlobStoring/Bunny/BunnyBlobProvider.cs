using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BunnyCDN.Net.Storage;
using Volo.Abp.DependencyInjection;

namespace Volo.Abp.BlobStoring.Bunny;

public class BunnyBlobProvider : BlobProviderBase, ITransientDependency
{
    protected IBunnyBlobNameCalculator BunnyBlobNameCalculator { get; }
    protected IBlobNormalizeNamingService BlobNormalizeNamingService { get; }

    public BunnyBlobProvider(
        IBunnyBlobNameCalculator bunnyBlobNameCalculator,
        IBlobNormalizeNamingService blobNormalizeNamingService)
    {
        BunnyBlobNameCalculator = bunnyBlobNameCalculator;
        BlobNormalizeNamingService = blobNormalizeNamingService;
    }

    public override async Task SaveAsync(BlobProviderSaveArgs args)
    {
        var configuration = args.Configuration.GetBunnyConfiguration();
        var containerName = GetContainerName(args);
        var blobName = BunnyBlobNameCalculator.Calculate(args);

        using var bunnyClient = GetBunnyClient(args);
        
        await ValidateContainerExistsAsync(bunnyClient, containerName, configuration);

        if (!args.OverrideExisting && await BlobExistsAsync(bunnyClient, containerName, blobName))
        {
            throw new BlobAlreadyExistsException(
                $"BLOB '{args.BlobName}' already exists in container '{containerName}'. " +
                $"Set {nameof(args.OverrideExisting)} to true to overwrite.");
        }

        using var memoryStream = new MemoryStream();
        await args.BlobStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        await bunnyClient.UploadAsync(memoryStream, $"{containerName}/{blobName}");
    }

    public override async Task<bool> DeleteAsync(BlobProviderDeleteArgs args)
    {
        var blobName = BunnyBlobNameCalculator.Calculate(args);
        var configuration = args.Configuration.GetBunnyConfiguration();
        var containerName = GetContainerName(args);
        using var bunnyClient = GetBunnyClient(args);
        if (!await BlobExistsAsync(bunnyClient, containerName, blobName))
        {
            return false;
        }

        try
        {
            await bunnyClient.DeleteObjectAsync($"{containerName}/{blobName}");
            return true;
        }
        catch (BunnyCDNStorageException ex) when (ex.Message.Contains("404"))
        {
            return false;
        }

    }

    public override async Task<bool> ExistsAsync(BlobProviderExistsArgs args)
    {
        var blobName = BunnyBlobNameCalculator.Calculate(args);
        var containerName = GetContainerName(args);
        var configuration = args.Configuration.GetBunnyConfiguration();
        using var bunnyClient = GetBunnyClient(args);

        return await BlobExistsAsync(bunnyClient, containerName, blobName);
    }

    public override async Task<Stream?> GetOrNullAsync(BlobProviderGetArgs args)
    {
        var blobName = BunnyBlobNameCalculator.Calculate(args);
        var containerName = GetContainerName(args);
        var configuration = args.Configuration.GetBunnyConfiguration();
        using var bunnyClient = GetBunnyClient(args);

        if (!await BlobExistsAsync(bunnyClient, containerName, blobName))
        {
            return null;
        }

        try
        {
            return await bunnyClient.DownloadObjectAsStreamAsync($"{containerName}/{blobName}");
        }
        catch (WebException ex) when ((HttpStatusCode)ex.Status == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    protected virtual async Task<bool> BlobExistsAsync(BunnyClient bunnyClient, string containerName, string blobName)
    {
        try
        {
            // Combine container name and blob name to create the full path
            var fullBlobPath = $"/{containerName}/{blobName}";

            // Extract the directory path
            var directoryPath = Path.GetDirectoryName(fullBlobPath)?.Replace('\\', '/') + "/";

            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new Exception("Invalid directory path generated from blob name.");
            }

            var objects = await bunnyClient.GetStorageObjectsAsync(directoryPath);

            // Check if the specific blob exists in the returned objects
            return objects?.Any(o => o.FullPath == fullBlobPath) == true;
        }
        catch (BunnyCDNStorageException ex) when (ex.Message.Contains("404"))
        {
            return false;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error while checking blob existence: {ex.Message}", ex);
        }
    }
    protected virtual BunnyClient GetBunnyClient(BlobProviderArgs args)
    {
        var containerName = GetContainerName(args);
        var configuration = args.Configuration.GetBunnyConfiguration();
        if (configuration.Region.IsNullOrEmpty())
        {
            return new BunnyClient(configuration.AccessKey, containerName);
        }
        return new BunnyClient(configuration.AccessKey, containerName, configuration.Region);
    }
    protected virtual string GetContainerName(BlobProviderArgs args)
    {
        var configuration = args.Configuration.GetBunnyConfiguration();
        return configuration.ContainerName.IsNullOrWhiteSpace()
            ? args.ContainerName
            : BlobNormalizeNamingService.NormalizeContainerName(args.Configuration, configuration.ContainerName!);
    }
    protected virtual async Task ValidateContainerExistsAsync(
       BunnyClient client,
       string containerName,
       BunnyBlobProviderConfiguration configuration)
    {
        if (configuration.CreateContainerIfNotExists)
        {
            try
            {
                await client.EnsureStorageZoneExistsAsync(containerName);
            }
            catch (BunnyApiException ex)
            {
                throw new AbpException(
                    $"Failed to ensure Bunny storage zone '{containerName}' exists: {ex.Message}",
                    ex);
            }
        }
    }
}