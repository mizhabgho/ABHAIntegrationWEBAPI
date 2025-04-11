using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

public class VerifyUserService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly VerifyOtpService _verifyOtpService;

    public VerifyUserService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        VerifyOtpService verifyOtpService)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _verifyOtpService = verifyOtpService;
    }

    public async Task<string> VerifyUserAsync()
    {
        var client = _httpClientFactory.CreateClient();

        // 🔑 Get JWT token from cache
        if (!_cache.TryGetValue("jwtToken", out string jwtToken) || string.IsNullOrEmpty(jwtToken))
            throw new Exception("JWT token not found in cache. Make sure VerifyOtpService has run.");

        // 🆔 Get txnId and abhaNumber from cache
        if (!_cache.TryGetValue("txnId", out string txnId))
            throw new Exception("Transaction ID not found in cache.");

        var ABHANumber = "91-4722-6124-3340";

        // 🧾 Prepare request payload
        var requestBody = new
        {
            ABHANumber,
            txnId = txnId
        };

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var requestBodyJson = JsonSerializer.Serialize(requestBody, jsonOptions);
        var requestContent = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");

        // 📬 Create HTTP request
        var request = new HttpRequestMessage(HttpMethod.Post, "https://abhasbx.abdm.gov.in/abha/api/v3/profile/login/verify/user")
        {
            Content = requestContent
        };
        Console.WriteLine($"📄 jwtToken: {jwtToken}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);
        request.Headers.Add("REQUEST-ID", Guid.NewGuid().ToString());
        request.Headers.Add("TIMESTAMP", DateTime.UtcNow.ToString("o"));

        // 🚀 Send request
        Console.WriteLine("📨 Sending user verification request...");
        var response = await client.SendAsync(request);
        var responseData = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"📥 Response Status: {response.StatusCode}");
        Console.WriteLine($"📄 Response Body: {responseData}");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("❌ User verification failed.");
            return responseData;
        }

        try
        {
            var jsonData = JsonSerializer.Deserialize<JsonElement>(responseData);
            var token = jsonData.GetProperty("token").GetString();
            var refreshToken = jsonData.GetProperty("refreshToken").GetString();

            // 🔒 Cache the tokens
            _cache.Set("jwtToken", token, TimeSpan.FromMinutes(55));
            _cache.Set("R-jwtToken", refreshToken, TimeSpan.FromMinutes(55));

            Console.WriteLine("✅ Tokens retrieved and cached.");
            return token;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Failed to parse tokens. Error: {ex.Message}");
            return responseData;
        }
    }
}
