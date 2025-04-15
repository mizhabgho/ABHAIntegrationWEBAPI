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
        string encryptedOtp = "XmyR00EAZAnPgHKM6X2V2F8Hj4qSmcYyaZ62gR6pM85aYRGv/C5BME4G20nXsbbz7QdoPQWdcRC+tdycNLqUSZzJSZV0KR/ZkzP+IzYwoNBLBC4nhaWn7VudQkS0Pss9z/CXonCZ1mDb3gLIYa+l2KeBE+jpZXJLBWEz0kx6xE3MpYYRW36htO8J2UUNvnqKoIa3XShrJKc5s47FjyC70PdlkD89Ol9eisuhFU7e/zExTBDQsFGM03Xh936qTNF/CFxcYXntRqNFQZt57Mc8o9qTMPdHZ+v6htkzxMRYXekkKYjo6rtuDTGx+Mo8uEHOSyw/aT3EJk5pbO75PSJU7EXtYMK2wG/cLFK7792xdY/1Unmyde1mCSs2bT/5JKb+mRi691xX6zEDwmHbP5ue8WKtOtk2UOcKVdOBEPPRxRvEPHl00Lvq7UKB920r6tc94UoFEW/iGK2DZRj/Koc9N2cpa4WGq6aocZdI5JIZ75emR5JnpYHfzbWbyXeZiQII14A+8ek6aJCI6Gi5MUkLX7v6SFUZ/IRV2mPQ3eHZAS+eKfw/EV5xgMLSg9rq61vLgs+WsLBzxT4osrKqyVlBzBwqnb9yLhZObMxoD8aC0ieiSilsjV3w6iIfXg+cHel8FK1VhnGWOq263CKPoK3JNOi1Z/PewT8gEGPJCk76O+A=";

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
