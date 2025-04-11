using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;

public class VerifyUserService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly TokenService _tokenService;

    public VerifyUserService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        TokenService tokenService)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
    }

    public async Task<string> VerifyUserAsync()
    {
        // Hardcoded ABHA number (temporary; replace with a VALID ABHA number registered in ABDM sandbox)
        const string ABHANumber = "91-4722-6124-3340"; // From VerifyOtpAsync accounts
        // Alternative: Try preferredAbhaAddress if ABHA number fails
        //const string preferredAbhaAddress = "mizhab_720032003@sbx"; // From VerifyOtpAsync accounts

        // Validate ABHA number format
        if (!Regex.IsMatch(ABHANumber, @"^\d{2}-\d{4}-\d{4}-\d{4}$"))
        {
            var error = $"Invalid ABHA number format: {ABHANumber}. Expected: XX-XXXX-XXXX-XXXX";
            Console.WriteLine($"❌ {error}");
            throw new ArgumentException(error);
        }

        var client = _httpClientFactory.CreateClient();

        // Get access token from cache or TokenService
        if (!_cache.TryGetValue("accessToken", out string accessToken) || string.IsNullOrEmpty(accessToken))
        {
            accessToken = await _tokenService.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                var error = "Failed to retrieve access token.";
                Console.WriteLine($"❌ {error}");
                throw new Exception(error);
            }
            _cache.Set("accessToken", accessToken, TimeSpan.FromMinutes(55));
        }

        // Get JWT token from cache
        if (!_cache.TryGetValue("jwtToken", out string jwtToken) || string.IsNullOrEmpty(jwtToken))
        {
            var error = "JWT token not found in cache. Please verify OTP first.";
            Console.WriteLine($"❌ {error}");
            throw new InvalidOperationException(error);
        }

        // Get txnId from cache
        if (!_cache.TryGetValue("txnId", out string txnId) || string.IsNullOrEmpty(txnId))
        {
            var error = "Transaction ID not found in cache. Please initiate OTP request first.";
            Console.WriteLine($"❌ {error}");
            throw new InvalidOperationException(error);
        }

        // Prepare request payload
        var requestBody = new
        {
            ABHANumber, // Try ABHA number first
            //ABHANumber = preferredAbhaAddress, // Uncomment to try preferredAbhaAddress
            txnId
        };

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var requestBodyJson = JsonSerializer.Serialize(requestBody, jsonOptions);
        var requestContent = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");

        // Create HTTP request
        var request = new HttpRequestMessage(HttpMethod.Post, "https://abhasbx.abdm.gov.in/abha/api/v3/profile/login/verify/user")
        {
            Content = requestContent
        };
        request.Headers.Add("T-token", $"Bearer {jwtToken}");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Headers.Add("REQUEST-ID", Guid.NewGuid().ToString());
        request.Headers.Add("TIMESTAMP", DateTime.UtcNow.ToString("o"));

        // Send request
        Console.WriteLine("📨 Sending user verification request...");
        Console.WriteLine($"📄 Request Payload: {requestBodyJson}");
        var response = await client.SendAsync(request);
        var responseData = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"📥 Response Status: {response.StatusCode}");
        Console.WriteLine($"📄 Response Body: {responseData}");

        if (!response.IsSuccessStatusCode)
        {
            var error = $"User verification failed with status {response.StatusCode}: {responseData}";
            Console.WriteLine($"❌ {error}");
            throw new HttpRequestException(error);
        }

        try
        {
            var jsonData = JsonSerializer.Deserialize<JsonElement>(responseData);

            // Validate token and refreshToken
            if (!jsonData.TryGetProperty("token", out var tokenElement) || !jsonData.TryGetProperty("refreshToken", out var refreshTokenElement))
            {
                var error = "Response missing token or refreshToken.";
                Console.WriteLine($"❌ {error}");
                throw new Exception(error);
            }

            var token = tokenElement.GetString();
            var refreshToken = refreshTokenElement.GetString();

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(refreshToken))
            {
                var error = "Received empty token or refreshToken.";
                Console.WriteLine($"❌ {error}");
                throw new Exception(error);
            }

            // Cache the tokens
            _cache.Set("jwtToken", token, TimeSpan.FromMinutes(55));
            _cache.Set("refreshToken", refreshToken, TimeSpan.FromDays(7));

            // Log success
            var logObject = new
            {
                Token = token,
                RefreshToken = refreshToken,
                ABHANumber = ABHANumber
            };
            string logJson = JsonSerializer.Serialize(logObject, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine($"✅ User verification successful:\n{logJson}");

            return token;
        }
        catch (JsonException ex)
        {
            var error = $"Failed to parse verify user response: {ex.Message}";
            Console.WriteLine($"⚠️ {error}");
            throw new Exception($"Response parsing failed: {responseData}", ex);
        }
        catch (Exception ex)
        {
            var error = $"User verification processing failed: {ex.Message}";
            Console.WriteLine($"⚠️ {error}");
            throw;
        }
    }
}