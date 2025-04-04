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
        string encryptedOtp = "LejmVDalU29wcBb/DRZD5PpJcgHXASpzHqPjMm2Xq2llJHZdmLUxuH8lG07D9V7q70v6j0b7i5JJCOLw3v+xFzF1XLPBJWjjIE38BMkYs9uvevjaPGZ55USsl7p0l8M0fbbLi4o2BAAScGNTbiPUjiIcAcsDDbeqrxMPbtkyDaYQl881Xz3+biMCUnNgQT79JURKFs+Lzam2Nz7uVo/HrNafO8w9T1Sp52zTLPQwUy+g4JUe09lT/Evz/ZXxhVMXljA+Rcl0PgmEGiGxjj2XcaIxPQbVVj3Zk/ieVRIskqTc9q1bkrdv3/Rb3BBny105PsEUWuODWlMPlbjc6GdzkaBd4UIrRJXjThBvjt6ZFax0nwafF6qLKlhrMX6Smj4WQUiCxPhVYlecFI2Vbj6WSFkK0iM3fxD3Q+15Gz8KUP38RIxWgbSF+lP/sCj5CVnuf/CmFLxgE5c763ntYNlIx8jU1nRQXsUyirkldcI7NcmmMBocmtYIXYdc7uVNPSmXf2xZ4WSSRBiFu6KlA7BZZF3tpzSXBfJlyo4rTqfgd88O60Oq1mB96p+7vqPlmd/y3MgEUjDM6on0M07MmbDrjLMVm6TKsS0f1Ad20JAGNS8RuZfmlBJp5uM7uas+wW/Qft/CMnI7or1TrWLDYMX9T+IN4Gg5Ef+imIRuapPkQuU=";

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
