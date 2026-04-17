using System.Text.Json;
using ProfilesApi.DTOs;

namespace ProfilesApi.Services;

public interface IExternalApiService
{
    Task<GenderizeResponse> GetGenderAsync(string name);
    Task<AgifyResponse> GetAgeAsync(string name);
    Task<NationalizeResponse> GetNationalityAsync(string name);
}

public class ExternalApiService(HttpClient httpClient, ILogger<ExternalApiService> logger)
    : IExternalApiService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<GenderizeResponse> GetGenderAsync(string name)
    {
        var response = await FetchAsync($"https://api.genderize.io?name={Uri.EscapeDataString(name)}");
        return Deserialize<GenderizeResponse>(response, "Genderize");
    }

    public async Task<AgifyResponse> GetAgeAsync(string name)
    {
        var response = await FetchAsync($"https://api.agify.io?name={Uri.EscapeDataString(name)}");
        return Deserialize<AgifyResponse>(response, "Agify");
    }

    public async Task<NationalizeResponse> GetNationalityAsync(string name)
    {
        var response = await FetchAsync($"https://api.nationalize.io?name={Uri.EscapeDataString(name)}");
        return Deserialize<NationalizeResponse>(response, "Nationalize");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> FetchAsync(string url)
    {
        try
        {
            var res = await httpClient.GetAsync(url);
            res.EnsureSuccessStatusCode();
            return await res.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HTTP error calling {Url}", url);
            throw;
        }
    }

    private static T Deserialize<T>(string json, string apiName)
    {
        try
        {
            var result = JsonSerializer.Deserialize<T>(json, JsonOpts);
            if (result is null)
                throw new ExternalApiException(apiName);
            return result;
        }
        catch (JsonException ex)
        {
            throw new ExternalApiException(apiName, ex);
        }
    }
}

// ── Custom exception ──────────────────────────────────────────────────────────
public class ExternalApiException(string apiName, Exception? inner = null)
    : Exception($"{apiName} returned an invalid response", inner)
{
    public string ApiName { get; } = apiName;
}
