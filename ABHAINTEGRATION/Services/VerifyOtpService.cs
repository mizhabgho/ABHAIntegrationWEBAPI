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
        // Check if JWT token is already cached
        if (_cache.TryGetValue("jwtToken", out string cachedToken))
        {
            Console.WriteLine("✅ Using cached JWT token.");
            return cachedToken;
        }

        var client = _httpClientFactory.CreateClient();

        // Get access token
        var accessToken = await _tokenService.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(accessToken))
            throw new Exception("Failed to retrieve access token.");

        // Get txnId from cache
        if (!_cache.TryGetValue("txnId", out string txnId))
            throw new Exception("Transaction ID not found in cache.");

        // Hardcoded encrypted OTP (replace with dynamic if needed)
        string encryptedOtp = "ke0/w613cVmCKuVNsCHIICoRZ2F//Sj+bd3XygmJp2x2XiHXe/k81DtUi6K6OlCZvW1xT6QEaG4kTNmuzNrfOYfEQUcJYMzV5ez20RC9T7dgE5nR3oy0r9C+5quLZnW7yrkQcZh4GwsHYKb6lNDxBVhsWgFZdaGgncMTjv9Aucwxkz1FaZvaVmR/HuOMbUpj/VU8QqRvDanbTtlS1gC09QmRFApdzrb7BJsBTABrmxnUcm1Bl+F/PsFJARUue+6QFIrd31+L3wrT1/o/umDyZxPzhKBMEiofAabeqjIhLUkMk4yy2oz2lwfRdYBt0pQ612rGzGcWZXTHxnqCFrZWJOJiEDDpHmZtjX4G5nQtQ4mjWhqp2Ip8CayrOtQzOuoCyGAngGm4lemRQZovuuYmt2VwSf8Fp/IOp0YT9i5GmlxForpRduNSbkKxjxNh87uWAOPdO0PqN9zuW/6eDz/o5Myggpyr3Ce+7eD0BP3ml7L22XlgALqt+KLQCaGEBRz1woI0Xl/8BPE6K5C7ceWt6Y9AQhq8t0s9qsMcdoV9ENthcDHEk2n5O44oK0YPQPxFE7nYbWH5KvWQR+mvRE+9Ljg9W7dz3jQyXuPB3RoGmSlDQ1RD6DbNhGjzRlj635ljThMGpkLjixRi9U32xdmNSHlYzUe3FKso49Q9YqCeoKM=";

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
        Console.WriteLine("📨 Sending OTP verification request...");
        var response = await client.SendAsync(request);
        var responseData = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"📥 Response Status: {response.StatusCode}");
        Console.WriteLine($"📄 Response Body: {responseData}");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("❌ OTP verification failed.");
            return responseData;
        }

        try
        {
            var jsonData = JsonSerializer.Deserialize<JsonElement>(responseData);
            var jwtToken = jsonData.GetProperty("token").GetString();

            // ✅ Cache the JWT token
            _cache.Set("jwtToken", jwtToken, TimeSpan.FromMinutes(55));
            Console.WriteLine("✅ JWT Token retrieved and cached.");
            return jwtToken;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Failed to parse JWT token. Error: {ex.Message}");
            return responseData;
        }
    }
}
