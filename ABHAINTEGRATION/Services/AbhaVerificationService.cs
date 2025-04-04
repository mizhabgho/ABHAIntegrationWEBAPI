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

    public AbhaVerificationService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        TokenService tokenService)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _tokenService = tokenService;
    }

    public async Task<string> SendOtpAsync()
    {
        var client = _httpClientFactory.CreateClient();

        // Fetch dynamic access token
        var accessToken = await _tokenService.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new Exception("Failed to retrieve access token.");
        }

        // Encrypt mobile number (Ensure this is correct)
        string encryptedMobileNumber = "ZYWrSvV4VKQesbK15Sg2hcEoAK7zaJLmfQCaUmuxnHa59gAnynwE+MyOTgOwCij78ehgdnHe223tjqweATVvlnCF3TqtbWUD7F7AGft1Yzr5T0mKKDYKmSErqxcXEq7SVpzAZYsHTf5ADA4b6/iid2tRZP+TU3Qff0DnL2NDa29fkjW4SMD3kZBsUWnucWbZ7PIPX5LRXAkWSRgx9OROROXZyl75jBT5iSUHQzJCnxtlW2Dq42Y2+9Za+rZapGNst+lI8soDQWRFGaNKEbMwrylW1Pmjwt7X8omAlLaTlYV90Vpkgv9qwSD7QxLxHxrhzGAMkbZp5vSQT3xNayLfJpABogPK+eYvCkUKdMnhYmvewJuJGfadFA0QhHsdpBN1/QZsHo871ick+XlCwrTdXk9A2SMx7FNZvymQ90ga3bjek2CLBXvGJf1ZjNjhVei0/1EKOFjyEE44Jt/0BcJomEjNSYZQGSyL8kfSYOcoDpfLtgH+RiCfNMITV5SUmeVkq9wPKKB2LwiRcix4SzoHpEc7pUSN+3WJeshtuHqOh01HTmazlQFBAzeqpSAlV4ZVKdR6mU79N0+DOgNrmsw6cvPXNSBqjvhKAnu3uX0Yk4PKSKSVyvHvXIVjmHbxaj7x10mUEQ1KAfIHdD/cE0IcTaYFIRt6KvX/FGeikOAyuVY="; // Replace with actual encryption logic
        Console.WriteLine($"Encrypted Mobile Number: {encryptedMobileNumber}");

        // Create OTP request payload
        var requestBody = new
        {
            scope = new[] { "abha-login", "mobile-verify" },
            loginHint = "mobile",
            loginId = encryptedMobileNumber,
            otpSystem = "abdm"
        };

        // Serialize with camelCase JSON naming
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var requestBodyJson = JsonSerializer.Serialize(requestBody, jsonOptions);
        var requestContent = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");

        // Prepare HTTP request
        var request = new HttpRequestMessage(HttpMethod.Post, "https://abhasbx.abdm.gov.in/abha/api/v3/profile/login/request/otp")
        {
            Content = requestContent
        };
        request.Headers.Add("REQUEST-ID", Guid.NewGuid().ToString());
        request.Headers.Add("TIMESTAMP", DateTime.UtcNow.ToString("o"));
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        // Send request
        Console.WriteLine("Sending OTP request...");
        var response = await client.SendAsync(request);

        // Read response
        var responseData = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Response Status: {response.StatusCode}");
        Console.WriteLine($"Response Body: {responseData}");

        // Check response
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("OTP request failed.");
            return responseData;
        }

        try
        {
            // Parse JSON response
            var jsonData = JsonSerializer.Deserialize<JsonElement>(responseData);

            // Extract txnId & message
            var txnId = jsonData.GetProperty("txnId").GetString();
            var message = jsonData.GetProperty("message").GetString();

            // Store txnId in cache with a suitable expiration time
            _cache.Set("txnId", txnId, TimeSpan.FromMinutes(5));

            // Print formatted JSON log
            var logObject = new
            {
                txnId,
                message
            };

            string logJson = JsonSerializer.Serialize(logObject, jsonOptions);
            Console.WriteLine($"OTP Response:\n{logJson}");

            return txnId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to parse transaction ID. Error: {ex.Message}");
            return responseData;
        }
    }

    // Method to retrieve txnId from cache
    public string GetTxnId()
    {
        if (_cache.TryGetValue("txnId", out string txnId))
        {
            return txnId;
        }
        else
        {
            throw new Exception("Transaction ID not found in cache.");
        }
    }
}
