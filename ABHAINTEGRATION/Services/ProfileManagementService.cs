using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace AbhaProfileApi.Services
{
    public interface IProfileManagementService
    {
        Task<string> GetProfileDetailsAsync(string xToken);
        Task<string> GetQrCodeAsync(string xToken);
        Task<string> UpdateProfilePhotoAsync(string xToken, string encryptedPhoto);
        Task<string> DownloadAbhaCardAsync(string xToken);
    }

    public class ProfileManagementService : IProfileManagementService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly string _baseUrl;

        public ProfileManagementService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _baseUrl = _configuration["Abha:BaseUrl"] ?? "https://abhasbx.abdm.gov.in/abha/api/v3";
        }

        private void AddCommonHeaders(HttpRequestMessage request, string xToken)
        {
            request.Headers.Add("X-Token", $"Bearer {xToken}");
            request.Headers.Add("REQUEST-ID", Guid.NewGuid().ToString());
            request.Headers.Add("TIMESTAMP", DateTime.UtcNow.ToString("O"));
        }

        public async Task<string> GetProfileDetailsAsync(string xToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/profile/account");
            AddCommonHeaders(request, xToken);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetQrCodeAsync(string xToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/profile/account/qrCode");
            AddCommonHeaders(request, xToken);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> UpdateProfilePhotoAsync(string xToken, string encryptedPhoto)
        {
            var request = new HttpRequestMessage(HttpMethod.Patch, $"{_baseUrl}/profile/account");
            AddCommonHeaders(request, xToken);

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

        public async Task<string> DownloadAbhaCardAsync(string xToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/profile/account/abha-card");
            AddCommonHeaders(request, xToken);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsStringAsync();
        }
    }
}