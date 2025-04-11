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
        string encryptedOtp = "EMCrsVOVBwVpJqn6hV1hpCK3XB/1ZS/T4ohubkgV9mTiZt6NCHSoDJ10NiSwkTfThvcSpXwoGaipmM80G/sMPof98r+ZfDOdhN3O0es/FR3zaF7V7HGkCY9ajWXb8HkXIr00oZ1XB0Qu47XAIPKKONkkLBqYzVREJ3Ljar0Q+YndDFOQqOtvGgd1g7j8MPBJd+bLVvW6FFcGKD29KOl0lxczXFjO4F931IjEV9mq+RRSnYNCmnKyGvgELLBAvYJMNgyshKaRyXZRkZVgh1PTBQ/zuRk8guoHielq9kX64RY2XDcBw3AcN3/4j8Q9O1dvkRs3DYe9e3WI54dJXDdewX6ng9OP3XlCnNT5e6IJIFVwPZMAaHOQ1qAwu7inKW+b/IKLZ2IxefwwdYgakhfkDDDFuLMK4WisJC8zwo3GO4BwT97dYLzb8Y3QDQKmIdkL7PKWz0591pLNAwxlRDe+o+ZYAnvS91KyHvkvQ1ojGGAVx41LWMmxzvVd65zGmGOekmUBdkIlc9xHK3A19dJfPQK7+0tP6fRDu4dx9one6977yYxm+tBFtO7FR66umRwjPcGrnUCpY+6q3FVC2VUowGiuuvsWx+gAf4s6hX+E0Sf9UQc6/ToB7joX6ngZTo9dbnRRCQb91Mw5X0UmXhAGQ8DHjUPojDZDwGvRMoryVGw=";

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
