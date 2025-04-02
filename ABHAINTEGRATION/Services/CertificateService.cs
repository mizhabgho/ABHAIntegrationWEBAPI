using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

public class CertificateService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly TokenService _tokenService;
    private const string CertificateKey = "publicKey"; // ✅ Consistent naming

    public CertificateService(IHttpClientFactory httpClientFactory, IMemoryCache cache, TokenService tokenService)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _tokenService = tokenService;
    }

    public async Task<string> GetPublicCertificateAsync()
    {
        if (_cache.TryGetValue(CertificateKey, out string certificate))
        {
            Console.WriteLine("✅ Using cached public key.");
            return certificate;
        }

        Console.WriteLine("🔄 Fetching new public key from ABHA API...");
        var accessToken = await _tokenService.GetAccessTokenAsync();

        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://abhasbx.abdm.gov.in/abha/api/v3/profile/public/certificate");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Headers.Add("REQUEST-ID", Guid.NewGuid().ToString());
        request.Headers.Add("TIMESTAMP", DateTime.UtcNow.ToString("o"));

        var response = await client.SendAsync(request);
        var responseData = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"🔍 Response Status: {response.StatusCode}");
        Console.WriteLine($"📄 Response Body: {responseData}");

        certificate = responseData;
        _cache.Set(CertificateKey, certificate, TimeSpan.FromMinutes(55));

        Console.WriteLine("✅ Public key retrieved and cached.");

        return certificate;
    }
}
