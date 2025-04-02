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

    public AbhaVerificationService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        TokenService tokenService,
        CertificateService certificateService)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _tokenService = tokenService;
        _certificateService = certificateService;
    }

    public async Task<string> SendOtpAsync()
    {
        var client = _httpClientFactory.CreateClient();

        // ✅ Fetch dynamic access token
        var accessToken = await _tokenService.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new Exception("❌ Failed to retrieve access token.");
        }

        // ✅ Encrypt mobile number (Ensure this is correct)
        string encryptedMobileNumber = "himjWuf50tp+E18w4Xvkq16zZi9Jk/NRk6n+k4cLeLlyg+STlWBravVAeSELPI9SP2ehvRuaB8OLVPjGkr0vQh/X7EUNIIQnrs/riZE/ilfKeCsV9wJZkaZrtdvQtqC1hDb9E58MSoZ97I4S9Hxm6sD3bJvL+zvu2l+RIO7NeCF/aewO/m6bklyFoN3pMWjhH0F6Fa3TXkUOvEV/hypKnzAcAJ/UCmZtlFIkumeduGi4W+oXQ0cLVTxN27ZVfrQAtGAfIeTGDswDoETizL/wAIgQdZGCRaa04BCEAJrC3a3bqWKTphKdp/iGt751Iq3jlR7nbDi5nMp4RtUikixzCkcDFskQ6cFe2Zr6qkGmAZqhaM4OPJVxlsSx0jzPqCbCFloRLmAQa1NUAevwtX1h00w8OyO/lExUnNPTiqVSobOqgOLtUA93cDYnPEPgNemP7Wwrk+7Vffoi6b11tH9fBsxmAsdp3RqCXx9fhgca5mJycCytQ/EYibF52AftPVTFyLJJFLqDxSZEbW7E4ObQ92IhEKcJaoioSscjrEfoRjtZXkpY56Q0Jn3A4CI1XsRT9a9Rx97Fqa3ZShnoPIxdbw1hGRWvEQ4MIsQTEjwilx3VLuUlGBQ0PLhX036myPVUqlWsj/MNsfeQZK1EQ2Mcy+59KRJ0iukt2beQ9ArNfbg=";
        Console.WriteLine($"🔐 Encrypted Mobile Number: {encryptedMobileNumber}");

        // ✅ Create OTP request payload
        var requestBody = new
        {
            scope = new[] { "abha-login", "mobile-verify" },
            loginHint = "mobile",
            loginId = encryptedMobileNumber,
            otpSystem = "abdm"
        };

        // ✅ Serialize with camelCase JSON naming
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        var requestBodyJson = JsonSerializer.Serialize(requestBody, jsonOptions);
        var requestContent = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");

        // ✅ Prepare HTTP request
        var request = new HttpRequestMessage(HttpMethod.Post, "https://abhasbx.abdm.gov.in/abha/api/v3/profile/login/request/otp")
        {
            Content = requestContent
        };
        request.Headers.Add("REQUEST-ID", Guid.NewGuid().ToString());
        request.Headers.Add("TIMESTAMP", DateTime.UtcNow.ToString("o"));
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        // ✅ Send request
        Console.WriteLine("🔄 Sending OTP request...");
        var response = await client.SendAsync(request);

        // ✅ Read response
        var responseData = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"🔍 Response Status: {response.StatusCode}");

        // ✅ Check response
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("❌ OTP request failed.");
            return responseData;
        }

        try
        {
            // ✅ Parse JSON response
            var jsonData = JsonSerializer.Deserialize<JsonElement>(responseData);

            // ✅ Extract txnId & message
            var txnId = jsonData.GetProperty("txnId").GetString();
            var message = jsonData.GetProperty("message").GetString();

            // ✅ Print formatted JSON log
            var logObject = new
            {
                txnId,
                message
            };

            string logJson = JsonSerializer.Serialize(logObject, jsonOptions);
            Console.WriteLine($"✅ OTP Response:\n{logJson}");

            return txnId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to parse transaction ID. Error: {ex.Message}");
            return responseData;
        }
    }
}
