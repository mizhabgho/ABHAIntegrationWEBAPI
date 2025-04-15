using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

public class VerifyUserService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly TokenService _tokenService;
    private const string ApiUrl = "https://abhasbx.abdm.gov.in/abha/api/v3/profile/login/verify/user";

    public VerifyUserService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        TokenService tokenService)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _tokenService = tokenService;
    }

    public async Task<string> VerifyUserAsync()
    {
        const string abhaNumber = "91-4722-6124-3340";
        var (accessToken, jwtToken, txnId) = await ValidateAndGetTokens();

        if (!IsValidAbhaFormat(abhaNumber))
        {
            throw new ArgumentException($"Invalid ABHA number format: {abhaNumber}");
        }

        using var request = CreateRequest(abhaNumber, accessToken, jwtToken, txnId);
        return await ExecuteRequest(request);
    }

    private bool IsValidAbhaFormat(string abhaNumber)
    {
        return Regex.IsMatch(abhaNumber, @"^\d{2}-\d{4}-\d{4}-\d{4}$");
    }

    private async Task<(string accessToken, string jwtToken, string txnId)> ValidateAndGetTokens()
    {
        var accessToken = await GetCachedToken("accessToken", _tokenService.GetAccessTokenAsync);
        var jwtToken = _cache.Get<string>("jwtToken") ?? throw new InvalidOperationException("JWT token missing");
        var txnId = _cache.Get<string>("txnId") ?? throw new InvalidOperationException("Transaction ID missing");
        
        return (accessToken.Trim(), jwtToken.Trim(), txnId.Trim());
    }

    private HttpRequestMessage CreateRequest(string abhaNumber, string accessToken, string jwtToken, string txnId)
    {
        var requestBody = new VerifyUserRequest
        {
            ABHANumber = abhaNumber,
            TxnId = txnId
        };

        var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null, // Disable camelCase conversion
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                }),
                Encoding.UTF8,
                "application/json")
        };

        AddHeaders(request, accessToken, jwtToken);
        LogRequestDetails(request);
        
        return request;
    }

    private void AddHeaders(HttpRequestMessage request, string accessToken, string jwtToken)
    {
        request.Headers.Add("T-token", $"Bearer {jwtToken}");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Headers.Add("REQUEST-ID", Guid.NewGuid().ToString());
        request.Headers.Add("TIMESTAMP", DateTime.UtcNow.ToString("o"));
    }

    private async Task<string> ExecuteRequest(HttpRequestMessage request)
    {
        using var client = _httpClientFactory.CreateClient();
        var response = await client.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();
        // Added response logging
        Console.WriteLine($"📥 Response Status: {response.StatusCode}");
        Console.WriteLine($"📄 Response Body: {responseContent}");
        
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Request failed with status {response.StatusCode}. Response: {responseContent}");
        }

        return ProcessSuccessfulResponse(responseContent);
    }

    private string ProcessSuccessfulResponse(string responseContent)
    {
        using var jsonDoc = JsonDocument.Parse(responseContent);
        var root = jsonDoc.RootElement;

        var token = root.GetProperty("token").GetString() 
            ?? throw new InvalidOperationException("Token missing in response");
        var refreshToken = root.GetProperty("refreshToken").GetString() 
            ?? throw new InvalidOperationException("Refresh token missing in response");

        CacheNewTokens(token, refreshToken);
        return token;
    }

    private void CacheNewTokens(string token, string refreshToken)
    {
        _cache.Set("xToken", token, TimeSpan.FromMinutes(55)); // Added xtoken caching
        Console.WriteLine("✅ X Token retrieved and cached.");
        _cache.Set("jwtToken", token, TimeSpan.FromMinutes(55));
        _cache.Set("refreshToken", refreshToken, TimeSpan.FromDays(7));
    }

    private async Task<string> GetCachedToken(string key, Func<Task<string>> fetchToken)
    {
        return await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(55);
            return await fetchToken();
        }) ?? throw new InvalidOperationException($"Failed to retrieve {key}");
    }

    private void LogRequestDetails(HttpRequestMessage request)
    {
        Console.WriteLine($"Request Headers: {string.Join(", ", request.Headers)}");
        Console.WriteLine($"Request Method: {request.Method}");
        Console.WriteLine($"Request URI: {request.RequestUri}");
        
        if (request.Content is StringContent content)
        {
            var body = content.ReadAsStringAsync().Result;
            Console.WriteLine($"Request Body: {body}");
        }
    }

    private class VerifyUserRequest
    {
        [JsonPropertyName("ABHANumber")]
        public string ABHANumber { get; set; }

        [JsonPropertyName("txnId")]
        public string TxnId { get; set; }
    }
}