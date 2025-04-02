using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly TokenService _tokenService;
    private readonly CertificateService _certificateService;
    private readonly AbhaVerificationService _abhaVerificationService;

    public AuthController(TokenService tokenService, CertificateService certificateService, AbhaVerificationService abhaVerificationService)
    {
        _tokenService = tokenService;
        _certificateService = certificateService;
        _abhaVerificationService = abhaVerificationService;
    }

    [HttpGet("token")]
    public async Task<IActionResult> GetToken()
    {
        var token = await _tokenService.GetAccessTokenAsync();
        return Ok(new { accessToken = token });
    }

    [HttpGet("certificate")]
    public async Task<IActionResult> GetPublicKey()
    {
        var publicKey = await _certificateService.GetPublicCertificateAsync();
        return Ok(new { publicKey });
    }

    [HttpPost("send-otp")]
    public async Task<IActionResult> SendOtp()
    {
        var response = await _abhaVerificationService.SendOtpAsync();
        return Ok(response);
    }
}
