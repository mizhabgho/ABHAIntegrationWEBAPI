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
        string encryptedOtp = "WhGfv1H/I64/pj4b+Ry4H1IXl+SJKLgKAzSbdx1WJhOwc0KKG2UAmPA7XgTVWYjuTynWslVzL2QbfaD0IWPU1BnJwYXNin+yGw+20QipEFxKAbXb+oO7Fz/J6YRkyeF0qeuXef0uTNPYOv27ZiZqfcuZvTK7Flex3CoN4lArY5w8yBfX8sOq4XjkUcArj2SzG31eItqVFB7V6OWTrvvzWPqeWvqnYRLzWG9v7/oPnfkCMXqlsioqhyn/HQ2bIs8ChMEJHH3aBnNW7ptjA1/t8HhFhe9oibIYIRanHTzVKhOrjCyuUnfNh+HQvxx9Z2PcOWLFq6AgnH9JQW4RM2DvM2KO4vam26glc3MGyBYcGxNxgPm9ikIkYmkr3yihGTPiljl6tcU5EI0eY84ju8j3ADVOvCyAVSS8VE0HtDdvH5T7iGQrpUmkt9adj3RrtWKb1xVoXifFIAv32Z8nwjHEsm+rEZcIBJivF0bwzXQEFG7MiCgt8b8uCGS/dqHwqJDFW13yOoHBbEbMJ79ICtnGbgPVjAVGyCdNhxwys6MLS0JY/aahxLQBo+pDJ4HNMhkDz3YeC/448+OSueH+VXOASLPKYhl/CnDCJl/RULxyxI/IpggKnw9DtmjHQ2+M/aj/e7dgLLmjGYVWB+UKKysEs6hY8o01/qASSN0SclY5oSA=";

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
