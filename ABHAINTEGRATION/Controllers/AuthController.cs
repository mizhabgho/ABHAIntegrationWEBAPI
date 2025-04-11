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
    private readonly VerifyUserService _verifyUserService;

    public AuthController(
        TokenService tokenService,
        CertificateService certificateService,
        AbhaVerificationService abhaVerificationService,
        VerifyOtpService verifyOtpService,
        VerifyUserService verifyUserService)
    {
        _tokenService = tokenService;
        _certificateService = certificateService;
        _abhaVerificationService = abhaVerificationService;
        _verifyOtpService = verifyOtpService;
        _verifyUserService = verifyUserService;
    }

    /// <summary>
    /// 🔐 Generate Access Token
    /// </summary>
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

    /// <summary>
    /// 🔑 Get Public Certificate (Public Key)
    /// </summary>
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

    /// <summary>
    /// 📩 Send OTP (Mobile Number is hardcoded inside service)
    /// </summary>
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

    /// <summary>
    /// ✅ Verify OTP (Encrypted OTP is hardcoded inside service)
    /// </summary>
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

    /// <summary>
    /// 👤 Verify ABHA User (using cached txnId & jwtToken)
    /// </summary>
    [HttpPost("verify-user")]
    public async Task<IActionResult> VerifyUser()
    {
        try
        {
            var token = await _verifyUserService.VerifyUserAsync();
            return Ok(new { Token = token });
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"❌ Controller error: {ex.Message}");
            return BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Controller error: {ex.Message}\nStackTrace: {ex.StackTrace}");
            return BadRequest(new { Error = "An unexpected error occurred." });
        }
    }
}
