using AjCaptcha.Core.Abstractions;
using AjCaptcha.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace AjCaptcha.Sample.Controllers;

[ApiController]
[Route("login")]
public class LoginController : ControllerBase
{
    private readonly ICaptchaService _captchaService;

    public LoginController(ICaptchaService captchaService)
    {
        _captchaService = captchaService;
    }

    [HttpPost]
    public async Task<ActionResult<CaptchaResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var verifyResponse = await _captchaService.VerifyAsync(new CaptchaVerifyRequest
        {
            CaptchaType = request.CaptchaType,
            CaptchaVerification = request.CaptchaVerification
        }, cancellationToken).ConfigureAwait(false);

        if (!verifyResponse.Success)
        {
            return Ok(verifyResponse);
        }

        return Ok(CaptchaResponse.SuccessData(new
        {
            request.Username,
            LoggedIn = true
        }));
    }
}

public sealed class LoginRequest
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? CaptchaType { get; set; } = CaptchaTypes.BlockPuzzle;
    public string? CaptchaVerification { get; set; }
}
