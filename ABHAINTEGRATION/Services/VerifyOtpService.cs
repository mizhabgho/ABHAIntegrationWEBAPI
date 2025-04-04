using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

public class VerifyOtpService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly TokenService _tokenService;

    public VerifyOtpService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        TokenService tokenService)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _tokenService = tokenService;
    }

    public async Task<string> VerifyOtpAsync()
    {
        var client = _httpClientFactory.CreateClient();

        // Get access token
        var accessToken = await _tokenService.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(accessToken))
            throw new Exception("Failed to retrieve access token.");

        // Get txnId from cache
        if (!_cache.TryGetValue("txnId", out string txnId))
            throw new Exception("Transaction ID not found in cache.");

        // Hardcoded encrypted OTP (replace with dynamic if needed)
        string encryptedOtp = "pxl50ewZmqTwOflmIL50zwlaWGJhJ5kJhSa+J32atVJmTrV7u6J7bArtCf51OsxO23nvIPrRTiYIMkh1H3hhDpwEYpW7Sk8QJuTw465DukohQQzDb93mp8npIkN9/LKBmk7sfzUcrybduuOUwM/L/oZR7dtzno0A9Uv9QLjAw2lKmoBL1Ww+dDYP1v0P6Z5DiEGfE9tq3yvVitd9ZuZ4n5FOef5LhuIq+a5o1KqLuja1oQmP9EGAkOIRT3n+2ispOJ4QTb9OBzMcA72RZ9poS4MS12YkJ6Oq2IDfxpdmKI11mqKidCnyos7Gcr9N4pxxQ1NGPzYkPwFxfg2StVgQsSBD4P5X/I5sEei5da5YD6ERXlAd1d80731G39zsqoBzi+PxDN+e4ewvWqqUQNFfg5Ee6zeT92GbKH2wm1yDhT4Gxo1tKdmB9E1HWx9HzOvM49iyykUc1bGEX68d5WJZgGnJbpKwlcdonexL2yml7PLqZWUS89X51SRE4CDtJwa6uZvDh5+LtzA93Zsgk0a/fWFuK9Ycg/yuPehbYSyMq64QAaLCLho28crka0ej5k5INE9BhYbOev6Th00TbRs52rzho6WGACCbazwtQFQCMhmvOUpuoRdnlcn9WERl5fBDrOEI83S3MZxWJqOWs0/00kty25MwXGkSVs07sz56nqM=";

        // Construct request payload
        var requestBody = new
        {
            scope = new[] { "abha-login", "mobile-verify" },
            authData = new
            {
                authMethods = new[] { "otp" },
                otp = new
                {
                    txnId = txnId,
                    otpValue = encryptedOtp
                }
            }
        };

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var requestBodyJson = JsonSerializer.Serialize(requestBody, jsonOptions);
        var requestContent = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");

        // Prepare HTTP request
        var request = new HttpRequestMessage(HttpMethod.Post, "https://abhasbx.abdm.gov.in/abha/api/v3/profile/login/verify")
        {
            Content = requestContent
        };
        request.Headers.Add("REQUEST-ID", Guid.NewGuid().ToString());
        request.Headers.Add("TIMESTAMP", DateTime.UtcNow.ToString("o"));
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        // Send request
        Console.WriteLine("Sending OTP verification request...");
        var response = await client.SendAsync(request);
        var responseData = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Response Status: {response.StatusCode}");
        Console.WriteLine($"Response Body: {responseData}");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("OTP verification failed.");
            return responseData;
        }

        try
        {
            var jsonData = JsonSerializer.Deserialize<JsonElement>(responseData);
            var jwtToken = jsonData.GetProperty("token").GetString();

            Console.WriteLine($"OTP Verified. JWT Token:\n{jwtToken}");
            return jwtToken;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to parse JWT token. Error: {ex.Message}");
            return responseData;
        }
    }
}
