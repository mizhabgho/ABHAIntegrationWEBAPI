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
    private const string AccessTokenKey = "accessToken"; // ✅ Fixed naming

    public TokenService(IHttpClientFactory httpClientFactory, IMemoryCache cache)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        if (_cache.TryGetValue(AccessTokenKey, out string accessToken))
        {
            Console.WriteLine("✅ Using cached access token.");
            return accessToken;
        }

        var client = _httpClientFactory.CreateClient();

        var requestBody = new
        {
            clientId = Environment.GetEnvironmentVariable("clientId"), // ✅ Fixed property names
            clientSecret = Environment.GetEnvironmentVariable("clientSecret"),
            grantType = "client_credentials"
        };

        var requestContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, "https://dev.abdm.gov.in/api/hiecm/gateway/v3/sessions")
        {
            Content = requestContent
        };
        request.Headers.Add("REQUEST-ID", Guid.NewGuid().ToString());
        request.Headers.Add("TIMESTAMP", DateTime.UtcNow.ToString("o"));
        request.Headers.Add("X-CM-ID", "sbx");

        Console.WriteLine("🔄 Sending request to ABHA API...");
        var response = await client.SendAsync(request);

        var responseData = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"🔍 Response Status: {response.StatusCode}");
        Console.WriteLine($"📄 Response Body: {responseData}");

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to retrieve access token. Status: {response.StatusCode}, Response: {responseData}");
        }

        var json = JsonSerializer.Deserialize<JsonElement>(responseData);
        accessToken = json.GetProperty("accessToken").GetString();

        _cache.Set(AccessTokenKey, accessToken, TimeSpan.FromMinutes(55));
        Console.WriteLine("✅ Access token retrieved and cached.");

        return accessToken;
    }
}
