using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

public class AbhaVerificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly TokenService _tokenService;
    private readonly CertificateService _certificateService;

    public AbhaVerificationService(IHttpClientFactory httpClientFactory, IMemoryCache cache, TokenService tokenService, CertificateService certificateService)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _tokenService = tokenService;
        _certificateService = certificateService;
    }

    public async Task<string> SendOtpAsync()
    {
        var client = _httpClientFactory.CreateClient();
        var accessToken = await _tokenService.GetAccessTokenAsync();

        if (string.IsNullOrEmpty(accessToken))
        {
            throw new Exception("❌ Failed to retrieve access token.");
        }

        string encryptedMobileNumber = "HARD_CODED_ENCRYPTED_VALUE"; // Replace with actual encrypted value

        Console.WriteLine($"🔐 Encrypted Mobile Number: {encryptedMobileNumber}");

        var requestBody = new
        {
            scope = new[] { "abha-login", "mobile-verify" },
            loginHint = "mobile",
            loginId = encryptedMobileNumber,
            otpSystem = "abdm"
        };

        var requestContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, "https://abhasbx.abdm.gov.in/api/v3/auth/otp")
        {
            Content = requestContent
        };
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Headers.Add("REQUEST-ID", Guid.NewGuid().ToString());
        request.Headers.Add("TIMESTAMP", DateTime.UtcNow.ToString("o"));

        Console.WriteLine("🔄 Sending OTP request...");
        var response = await client.SendAsync(request);

        var responseData = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"🔍 Response Status: {response.StatusCode}");
        Console.WriteLine($"📄 Response Body: {responseData}");

        return responseData;
    }
}
