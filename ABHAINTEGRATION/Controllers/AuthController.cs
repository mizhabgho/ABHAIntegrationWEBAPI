using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly TokenService _tokenService;
    private readonly CertificateService _certificateService;

    public AuthController(TokenService tokenService, CertificateService certificateService)
    {
        _tokenService = tokenService;
        _certificateService = certificateService;
    }

    [HttpGet("token")]
    public async Task<IActionResult> GetToken()
    {
        var token = await _tokenService.GetAccessTokenAsync();
        return Ok(new { accessToken = token });
    }

    [HttpGet("certificate")]
    public async Task<IActionResult> GetCertificate()
    {
        var certificate = await _certificateService.GetPublicCertificateAsync();
        return Ok(new { certificate });
    }
}
