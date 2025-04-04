using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly TokenService _tokenService;
    private readonly CertificateService _certificateService;
    private readonly AbhaVerificationService _abhaVerificationService;
    private readonly VerifyOtpService _verifyOtpService;

    public AuthController(
        TokenService tokenService,
        CertificateService certificateService,
        AbhaVerificationService abhaVerificationService,
        VerifyOtpService verifyOtpService) // Injected here
    {
        _tokenService = tokenService;
        _certificateService = certificateService;
        _abhaVerificationService = abhaVerificationService;
        _verifyOtpService = verifyOtpService; // Assigned here
    }

    // ✅ Generate Access Token
    [HttpGet("token")]
    public async Task<IActionResult> GetAccessToken()
    {
        try
        {
            var token = await _tokenService.GetAccessTokenAsync();
            return Ok(new { accessToken = token });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Failed to generate access token", message = ex.Message });
        }
    }

    // ✅ Get Public Certificate (Public Key)
    [HttpGet("certificate")]
    public async Task<IActionResult> GetPublicCertificate()
    {
        try
        {
            var publicKey = await _certificateService.GetPublicCertificateAsync();
            return Ok(new { publicKey });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Failed to fetch public key", message = ex.Message });
        }
    }

    // ✅ Send OTP (Mobile Number is hardcoded inside AbhaVerificationService)
    [HttpPost("send-otp")]
    public async Task<IActionResult> SendOtp()
    {
        try
        {
            var txnId = await _abhaVerificationService.SendOtpAsync();
            return Ok(new { txnId });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Failed to send OTP", message = ex.Message });
        }
    }

    // ✅ Verify OTP (Encrypted OTP is hardcoded inside VerifyOtpService)
    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp()
    {
        try
        {
            var jwtToken = await _verifyOtpService.VerifyOtpAsync();
            return Ok(new { token = jwtToken });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "OTP verification failed", message = ex.Message });
        }
    }
}
