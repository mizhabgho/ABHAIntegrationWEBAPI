using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

public class TokenService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly string _baseUrl;

    /// <summary>
    /// Initializes a new instance of the TokenService.
    /// </summary>
    /// <param name="httpClientFactory">Factory for creating HTTP clients.</param>
    /// <param name="cache">Memory cache for storing tokens.</param>
    public TokenService(IHttpClientFactory httpClientFactory, IMemoryCache cache)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        // Configurable base URL (defaults to ABHA sandbox)
        _baseUrl = Environment.GetEnvironmentVariable("ABHA_API_URL") ?? "https://dev.abdm.gov.in/api/hiecm/gateway";
    }

    /// <summary>
    /// Retrieves an access token from the ABHA API, caching it for reuse.
    /// </summary>
    /// <returns>The access token.</returns>
    /// <exception cref="InvalidOperationException">Thrown if client ID or secret is missing.</exception>
    /// <exception cref="HttpRequestException">Thrown if the API call fails.</exception>
    public async Task<string> GetAccessTokenAsync()
    {
        // Validate environment variables
        var clientId = Environment.GetEnvironmentVariable("clientId");
        var clientSecret = Environment.GetEnvironmentVariable("clientSecret");
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            throw new InvalidOperationException("Client ID or Client Secret is not configured.");
        }

        // Use client-specific cache key to prevent collisions
        var cacheKey = $"accessToken_{clientId}";
        if (_cache.TryGetValue(cacheKey, out string accessToken) && !string.IsNullOrEmpty(accessToken))
        {
            Console.WriteLine("✅ Retrieved access token from cache.");
            return accessToken;
        }

        // Create HTTP client
        var client = _httpClientFactory.CreateClient();
        var requestBody = new
        {
            clientId,
            clientSecret,
            grantType = "client_credentials"
        };

        // Serialize request body
        var requestContent = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json"
        );

        // Build request with ABHA-required headers
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v3/sessions")
        {
            Content = requestContent
        };
        request.Headers.Add("REQUEST-ID", Guid.NewGuid().ToString());
        request.Headers.Add("TIMESTAMP", DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
        request.Headers.Add("X-CM-ID", "sbx");

        try
        {
            Console.WriteLine("🔄 Sending token request to ABHA API...");
            var response = await client.SendAsync(request);

            var responseData = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"🔍 Response Status: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to retrieve access token. Status: {response.StatusCode}");
            }

            // Parse response safely
            var json = JsonSerializer.Deserialize<JsonElement>(responseData);
            if (!json.TryGetProperty("accessToken", out var tokenElement))
            {
                throw new HttpRequestException("Access token not found in API response.");
            }

            accessToken = tokenElement.GetString();
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new HttpRequestException("Access token is empty or invalid.");
            }

            // Cache token for 55 minutes (slightly less than typical 60-min expiry)
            _cache.Set(cacheKey, accessToken, TimeSpan.FromMinutes(55));
            Console.WriteLine("✅ Access token retrieved and cached.");

            return accessToken;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"❌ Error fetching token: {ex.Message}");
            throw;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"❌ Error parsing response: {ex.Message}");
            throw new HttpRequestException("Failed to parse access token response.", ex);
        }
    }
}