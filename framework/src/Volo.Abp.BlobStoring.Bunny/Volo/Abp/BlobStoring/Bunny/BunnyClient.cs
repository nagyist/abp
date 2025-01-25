using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using BunnyCDN.Net.Storage;
using BunnyCDN.Net.Storage.Models;

namespace Volo.Abp.BlobStoring.Bunny;

public class BunnyClient : IDisposable
{
    private readonly string _region;
    private readonly string _storageZoneName;
    private readonly HttpClient _httpClient;

    private static BunnyCDNStorage? BunnyCDNStorage = null;

    public BunnyClient(string accessKey, string storageZoneName, string region = "de", HttpMessageHandler? handler = null)
    {
        if (string.IsNullOrWhiteSpace(accessKey))
        {
            throw new ArgumentException("Account access key must not be null or empty.", nameof(accessKey));
        }
        _region = region;
        _storageZoneName = storageZoneName;
        _httpClient = handler != null ? new HttpClient(handler) : new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(120);
        _httpClient.DefaultRequestHeaders.Add("AccessKey", accessKey);
    }

    public async Task<bool> DoesStorageZoneExistAsync(string zoneName)
    {
        if (string.IsNullOrWhiteSpace(zoneName))
        {
            throw new ArgumentException("Zone name must not be null or empty.", nameof(zoneName));
        }

        try
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri("https://api.bunny.net/storagezone?page=0&perPage=1000&includeDeleted=false"),
                Headers = { { "accept", "application/json" } }
            };

            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var storageZones = JsonSerializer.Deserialize<List<StorageZone>>(responseBody);

            return storageZones?.Any(zone => zone.Name!.Equals(zoneName, StringComparison.OrdinalIgnoreCase)) == true;
        }
        catch (HttpRequestException ex)
        {
            throw new BunnyApiException("Error occurred while checking storage zone existence.", ex);
        }
        catch (JsonException ex)
        {
            throw new BunnyApiException("Failed to parse the response from Bunny API.", ex);
        }
    }

    // Ensure the storage zone exists, creating it if not found, and return the zone
    public async Task<StorageZone> EnsureStorageZoneExistsAsync(string zoneName, string? originUrl = "")
    {
        try
        {
            if (!await DoesStorageZoneExistAsync(zoneName))
            {
                return await CreateStorageZoneAsync(zoneName, originUrl);
            }

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri("https://api.bunny.net/storagezone?page=0&perPage=1000&includeDeleted=false"),
                Headers = { { "accept", "application/json" } }
            };

            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var storageZones = JsonSerializer.Deserialize<List<StorageZone>>(responseBody);

            return storageZones?.FirstOrDefault(zone => zone.Name!.Equals(zoneName, StringComparison.OrdinalIgnoreCase))
                ?? throw new BunnyApiException($"Storage zone '{zoneName}' not found even though it should exist.");
        }
        catch (BunnyApiException ex)
        {
            throw new BunnyApiException($"Failed to ensure the existence of storage zone '{zoneName}'.", ex);
        }
    }
    
    public async Task UploadAsync(Stream stream, string path) => await (await GetBunnyCDNStorage()).UploadAsync(stream, path);
    
    public async Task<bool> DeleteObjectAsync(string path) => await (await GetBunnyCDNStorage()).DeleteObjectAsync(path);
    
    public async Task<List<StorageObject>> GetStorageObjectsAsync(string path) => await (await GetBunnyCDNStorage()).GetStorageObjectsAsync(path);
   
    public async Task<Stream> DownloadObjectAsStreamAsync(string path) => await (await GetBunnyCDNStorage()).DownloadObjectAsStreamAsync(path);
    
    public async Task DeleteStorageZoneAsync(string zoneName)
    {
        if (string.IsNullOrWhiteSpace(zoneName))
        {
            throw new ArgumentException("Zone name must not be null or empty.", nameof(zoneName));
        }
        var storageZone = (await GetStorageZoneAsync());

        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Delete,
            RequestUri = new Uri($"https://api.bunny.net/storagezone/{storageZone.Id}")
        };
        using (var response = await _httpClient.SendAsync(request))
        {
            response.EnsureSuccessStatusCode();
        }
    }
    private async Task<StorageZone> GetStorageZoneAsync()
    {
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri("https://api.bunny.net/storagezone")
        };

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var zones = JsonSerializer.Deserialize<List<StorageZone>>(content);

        var targetZone = zones?.FirstOrDefault(z =>
            z.Name.Equals(_storageZoneName, StringComparison.OrdinalIgnoreCase));

        return targetZone
            ?? throw new AbpException(
                $"Storage zone '{_storageZoneName}' is not found");
    }
    private async Task<StorageZone> CreateStorageZoneAsync(string zoneName, string? originUrl = "", int zoneTier = 0)
    {
        if (string.IsNullOrWhiteSpace(zoneName))
        {
            throw new ArgumentException("Zone name must not be null or empty.", nameof(zoneName));
        }

        try
        {
            var payload = new Dictionary<string, object>
            {
                { "ZoneTier", zoneTier },
                { "Name", zoneName },
                { "Region", _region }
            };

            // Add OriginUrl only if it is not empty
            if (!string.IsNullOrEmpty(originUrl))
            {
                payload.Add("OriginUrl", originUrl!);
            }

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://api.bunny.net/storagezone"),
                Headers = { { "accept", "application/json" } },
                Content = new StringContent(JsonSerializer.Serialize(payload))
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
                }
            };

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                throw new BunnyApiException($"Failed to create storage zone. Response: {responseBody}");
            }

            var createdZoneJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<StorageZone>(createdZoneJson)
                ?? throw new BunnyApiException("Failed to deserialize the created storage zone response.");
        }
        catch (HttpRequestException ex)
        {
            throw new BunnyApiException("Error occurred while creating storage zone.", ex);
        }
        catch (JsonException ex)
        {
            throw new BunnyApiException("Failed to parse the response for the created storage zone.", ex);
        }
    }
    private async Task<BunnyCDNStorage> GetBunnyCDNStorage()
    {
        if (BunnyCDNStorage != null) { 
            return BunnyCDNStorage;
        }
        var storageZonePassword = (await GetStorageZoneAsync()).Password;
        BunnyCDNStorage = new BunnyCDNStorage(_storageZoneName, storageZonePassword, _region);

        return BunnyCDNStorage;
    }

    public void Dispose() => _httpClient?.Dispose();

    public class StorageZone
    {
        public int Id { get; set; }
        public string Password { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Region { get; set; } = null!;
        public bool Deleted { get; set; }
    }
}

public class BunnyApiException : Exception
{
    public BunnyApiException(string message) : base(message) { }

    public BunnyApiException(string message, Exception innerException) : base(message, innerException) { }
}
