using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace AbhaProfileApi.Services
{
    public interface IProfileManagementService
    {
        Task<string> GetProfileDetailsAsync();
        Task<string> GetQrCodeAsync();
        Task<string> UpdateProfilePhotoAsync(string encryptedPhoto);
        Task<string> DownloadAbhaCardAsync();
    }

    public class ProfileManagementService : IProfileManagementService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly string _baseUrl;

        public ProfileManagementService(HttpClient httpClient, IConfiguration configuration, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _cache = cache;
            _baseUrl = _configuration["Abha:BaseUrl"] ?? "https://abhasbx.abdm.gov.in/abha/api/v3";
        }

        private void AddCommonHeaders(HttpRequestMessage request)
        {
            var xToken = _cache.Get<string>("xToken") ?? throw new InvalidOperationException("X-Token missing from cache");
            request.Headers.Add("X-Token", $"Bearer {xToken}");
            request.Headers.Add("REQUEST-ID", Guid.NewGuid().ToString());
            request.Headers.Add("TIMESTAMP", DateTime.UtcNow.ToString("O"));
        }

        public async Task<string> GetProfileDetailsAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/profile/account");
            AddCommonHeaders(request);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetQrCodeAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/profile/account/qrCode");
            AddCommonHeaders(request);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> UpdateProfilePhotoAsync(string encryptedPhoto)
        {
            var request = new HttpRequestMessage(HttpMethod.Patch, $"{_baseUrl}/profile/account");
            AddCommonHeaders(request);

            var content = new { profilePhoto = encryptedPhoto };
            request.Content = new StringContent(
                JsonSerializer.Serialize(content),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> DownloadAbhaCardAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/profile/account/abha-card");
            AddCommonHeaders(request);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsStringAsync();
        }
    }
}