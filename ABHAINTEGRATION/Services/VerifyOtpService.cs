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
        string encryptedOtp = "rSOukqoZooQDULACeSGr+VEkCQ3ZNP98+NTyUT44LWV+hoYbM1N+e10shO+DKgAi9CL/RFeWjDskI4xqsgRBTrvNrFrUArR3i3dZ029Rp6dhuHfMZDuEQ6amV9OCYdBETQkD6OTXv68kuLUkXpmc/lZ9ngcAcF4GurfkZk5VZzWXUMD4US3Ftw1QCZS9IKk5eMgrozC+A439Im/9SiNTH3AH7Q5D5U5GyCTM31cM5i/zOQSbPkLZg+RrQkZQfyokI4b7REcNhXUZsUZnCOETkUpZ3QicEfPjY7snofDUi3vxRz0s7mabyrV3tbor9mRGKdS6kOfByCKRq1+vqXnzEiRYH/H66JkwCvSsGZYvJDzKUYWXZdoRkXnWmnCAJ/4XsnRZGKiUcyOLfG1amGbmJlMR/Q28LnoYNUVf6sXlfdcJDikTYX4PXxkrZFBaeTKOeOyBOl+2a2bZdEDFFIDnKW4f0J8+shc7mqQQQddrOxhLEsEREpDhxczmJf1Gj32hlfWj3Mz5boYFr3RByMGtx4x5L6rH/Ym+lloh+/glcoLT1UpGb0ZWBYj0LaOhJSg0is9Ss5Yb4EgBO4YkGYBaGEAfvc8ZRLYj7Xy5/ovcXgEjr4cHPWUFRvI5D24ml8TWi6diuivP1Hsq7sR36mmAjycJGAYUl6lUDqFTors0Dxk=";

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
