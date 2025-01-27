using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BunnyCDN.Net.Storage;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Security.Encryption;

namespace Volo.Abp.BlobStoring.Bunny;

public class DefaultBunnyClientFactory : IBunnyClientFactory, ITransientDependency
{
    private readonly IDistributedCache<StorageZone> _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IStringEncryptionService _stringEncryptionService;

    private const string CacheKeyPrefix = "BunnyStorageZone:";
    private static TimeSpan CacheDuration = TimeSpan.FromHours(12);

    public DefaultBunnyClientFactory(
        IHttpClientFactory httpClient,
        IDistributedCache<StorageZone> cache,
        IStringEncryptionService stringEncryptionService)
    {
        _cache = cache;
        _httpClientFactory = httpClient;
        _stringEncryptionService = stringEncryptionService;
    }

    public virtual async Task<BunnyCDNStorage> CreateAsync(string accessKey, string containerName, string region = "de")
    {
        var cacheKey = $"{CacheKeyPrefix}{containerName}";
        var storageZoneInfo = await _cache.GetOrAddAsync(
            cacheKey,
            async () => {
                var result = await GetStorageZoneAsync(accessKey, containerName);
                if (result == null)
                {
                    throw new AbpException($"Storage zone '{containerName}' not found");
                }

                // Encrypt the sensitive password before caching
                result.Password = _stringEncryptionService.Encrypt(result.Password!)!;
                return result;
            },
            () => new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = DateTimeOffset.Now.Add(CacheDuration)
            }
        );

        if (storageZoneInfo == null)
        {
            throw new AbpException($"Could not retrieve storage zone information for container '{containerName}'");
        }

        // Decrypt the password before using it
        var decryptedPassword = _stringEncryptionService.Decrypt(storageZoneInfo.Password);

        return new BunnyCDNStorage(containerName, decryptedPassword, region);
    }

    public virtual async Task EnsureStorageZoneExistsAsync(
        string accessKey,
        string containerName,
        string region = "de",
        bool createIfNotExists = false)
    {
        var storageZone = await GetStorageZoneAsync(accessKey, containerName);

        if (storageZone == null)
        {
            if (!createIfNotExists)
            {
                throw new AbpException(
                    $"Storage zone '{containerName}' does not exist. " +
                    "Set createIfNotExists to true to create it automatically.");
            }

            await CreateStorageZoneAsync(accessKey, containerName, region);

            // Clear the cache to force a refresh of the storage zone info
            var cacheKey = $"{CacheKeyPrefix}{containerName}";
            await _cache.RemoveAsync(cacheKey);
        }
    }

    protected virtual async Task<StorageZone> CreateStorageZoneAsync(
        string accessKey,
        string containerName,
        string region)
    {
        using var _client = _httpClientFactory.CreateClient("BunnyApiClient");
        _client.DefaultRequestHeaders.Add("AccessKey", accessKey);

        var payload = new Dictionary<string, object>
        {
            { "Name", containerName },
            { "Region", region },
            { "ZoneTier", 0 }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync(
            "https://api.bunny.net/storagezone",
            content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new AbpException(
                $"Failed to create storage zone '{containerName}'. " +
                $"Status: {response.StatusCode}, Error: {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var createdZone = JsonSerializer.Deserialize<StorageZone>(responseContent);

        if (createdZone == null)
        {
            throw new AbpException(
                $"Failed to deserialize the created storage zone response for '{containerName}'");
        }

        return createdZone;
    }

    protected virtual async Task<StorageZone?> GetStorageZoneAsync(string accessKey, string containerName)
    {
        using var _client = _httpClientFactory.CreateClient("BunnyApiClient");
        _client.DefaultRequestHeaders.Add("AccessKey", accessKey);
        var response = await _client.GetAsync("https://api.bunny.net/storagezone");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var zones = JsonSerializer.Deserialize<StorageZone[]>(content);

        return Array.Find(zones!, z =>
            z.Name.Equals(containerName, StringComparison.OrdinalIgnoreCase) && !z.Deleted);
    }

    [Serializable]
    public class StorageZone
    {
        public int Id { get; set; }
        public string Password { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Region { get; set; }
        public bool Deleted { get; set; }
    }
}
public class BunnyApiException : Exception
{
    public BunnyApiException(string message) : base(message) { }
    public BunnyApiException(string message, Exception innerException) : base(message, innerException) { }
}
