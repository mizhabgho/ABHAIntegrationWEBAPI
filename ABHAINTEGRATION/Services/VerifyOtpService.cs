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
        string encryptedOtp = "ckI6sRuKDf+H/K4O6283EYJHZ/cCS3p9gQ0a4uIt2SAx4/oc0i5Tm/JEqQ8yx27zhZy1to+z3NY8llTIkDhGnp4at/9vfMI0nhika3axNMvfaA73aONXPrNQ91VY+Cr0QzZdCty9HKW4y0KzyPWv+dZA2/r61eesQet2iFzrDPEPvcRWQKpTa1UlkMvrCAzmvO6pWnaN5ykvqe3RdWLymAAKaCagsjqy4vjxk4pLK0u4Qs9KkrRplqKs8qietVmJPmsETeDYJW+U6OFh7H6B4etSziiUAdwHhXzEuivvtEUGmO6UclXN96sGvpvw2mUUEC2dNb6VGDCBqdWojJ0heiOu4tTx1/PvanbAMm6REJsFCgFWc3CIEn1vYEWZ9sO0nhPjuNlsJ5cIy+OKW3hL3PH2qFumINea8bL+2YwTgyj/AUOiFh4uwGhuoYmO35VEHHSX9wjoJhftHn6VBKI+XGsFx4PtBU9KxcX4EqY7thzaxVQaPhdDqeEcpAjxxQUceirVUbrqAynMqUYhpqFQMX9DlmNU3G3jM6odDk8hujtx5nk2vnrAAi9GhKWIB8s247bzODmKvM1mUSz+Le3mJm3KuB2prCRMisF9nJOO6KGOsYDeI0+OUSVdtyEAfKyritji6yCGwFtPsBLGi7Kppe2tLdxdBzO06vC7dOtZIDA=";

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
