using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

public class CertificateService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly TokenService _tokenService;
    private const string CertificateKey = "PublicCertificate";

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
            return certificate;
        }

        var accessToken = await _tokenService.GetAccessTokenAsync();
        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://abhasbx.abdm.gov.in/abha/api/v3/profile/public/certificate");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Headers.Add("REQUEST-ID", Guid.NewGuid().ToString());
        request.Headers.Add("TIMESTAMP", DateTime.UtcNow.ToString("o"));

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode) throw new Exception("Failed to retrieve public certificate");

        certificate = await response.Content.ReadAsStringAsync();
        _cache.Set(CertificateKey, certificate, TimeSpan.FromMinutes(55));
        return certificate;
    }
}
