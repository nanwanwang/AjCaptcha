using AjCaptcha.Core.Abstractions;
using AjCaptcha.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AjCaptcha.AspNetCore.Controllers;

[ApiController]
[Route("captcha")]
public class CaptchaController : ControllerBase
{
    private readonly ICaptchaService _captchaService;

    public CaptchaController(ICaptchaService captchaService)
    {
        _captchaService = captchaService;
    }

    [HttpPost("get")]
    public async Task<ActionResult<CaptchaResponse>> Get([FromBody] CaptchaRequest? request, CancellationToken cancellationToken)
    {
        request ??= new CaptchaRequest();
        request.BrowserInfo ??= GetRemoteId(Request);
        return Ok(await _captchaService.GetAsync(request, cancellationToken).ConfigureAwait(false));
    }

    [HttpPost("check")]
    public async Task<ActionResult<CaptchaResponse>> Check([FromBody] CaptchaCheckRequest? request, CancellationToken cancellationToken)
    {
        request ??= new CaptchaCheckRequest();
        request.BrowserInfo ??= GetRemoteId(Request);
        return Ok(await _captchaService.CheckAsync(request, cancellationToken).ConfigureAwait(false));
    }

    [HttpPost("verify")]
    public async Task<ActionResult<CaptchaResponse>> Verify([FromBody] CaptchaVerifyRequest? request, CancellationToken cancellationToken)
    {
        request ??= new CaptchaVerifyRequest();
        request.BrowserInfo ??= GetRemoteId(Request);
        return Ok(await _captchaService.VerifyAsync(request, cancellationToken).ConfigureAwait(false));
    }

    private static string GetRemoteId(HttpRequest request)
    {
        var forwarded = request.Headers["X-Forwarded-For"].FirstOrDefault();
        var ip = !string.IsNullOrWhiteSpace(forwarded)
            ? forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault()
            : request.HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = request.Headers.UserAgent.ToString();
        return string.Concat(ip ?? string.Empty, userAgent);
    }
}
