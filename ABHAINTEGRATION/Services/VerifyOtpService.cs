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
        string encryptedOtp = "CGDTfW3lJ0HBFp5DoMDoVQ7bxGzMMe6FUNbqqrad8PNcMupB5D0oLJCFuL977hoPHk6coEPAjo/e7rnvg0/B7swBVczZUSgnTwHlfi/Tm4mCC5DEoxwPAQTRoGL1WfZYinFvYeGI15/SqxeKiVSh2JlU9VGIoCUDnsmlDjd4Rcuo5TK+og/8PWWr1Ri3aDwjEkZUq61PaQa09d/LtukdQqdxkxXo9GuPcykLGTS7qyGzP7kP8ppwq+KvjOY/msy0MRo34gQtt0AoCRdGfksfp4Hd30u1AyGVgJ56cANhQ7RFLvUjtVHaSJ2h7izh7GQQSRJBELbu2Qehf20vsiXNJazoXgO/VJ86gm576gxAoF61wjItf8DZmoEpQ3NAlJ5Abydx4+9VSdzZGUMaASEfEZ9ISVgzHviqmkH9VneSM4ySSa+s0eNtBcHWlaejYTgrI8h/QDyz09U39PxY6YqW/i/GSsqGPHlKcC4rTl/Glz/J1gftY+eh/qTqjH8z9T1kJNTufSttztZ2V36z2sXJv8GHRsHYdMBaxUugiky4QpTi/JY1yP3tJrsc/f5qzemlBkrCp758HVjNhdon/2jvDCSf+3tt7xynylqJfcTfKEi1ORomHA/vFO+y6lB4mlFSZaOMKG/gWXlpy/vaVqkLRhPGOEo2fTmB5pKECgmVbcw=";

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
