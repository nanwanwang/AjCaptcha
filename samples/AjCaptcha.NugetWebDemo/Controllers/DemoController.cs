using AjCaptcha.Core.Abstractions;
using AjCaptcha.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AjCaptcha.NugetWebDemo.Controllers;

[ApiController]
[Route("api/demo")]
public class DemoController : ControllerBase
{
    private readonly ICaptchaService _captchaService;
    private readonly AjCaptchaOptions _options;
    private readonly IWebHostEnvironment _environment;

    public DemoController(
        ICaptchaService captchaService,
        IOptions<AjCaptchaOptions> options,
        IWebHostEnvironment environment)
    {
        _captchaService = captchaService;
        _options = options.Value;
        _environment = environment;
    }

    [HttpGet("status")]
    public ActionResult<DemoStatusResponse> Status()
    {
        return Ok(new DemoStatusResponse
        {
            Environment = _environment.EnvironmentName,
            CacheType = _options.CacheType.ToString(),
            DefaultCaptchaType = _options.Type,
            AesStatus = _options.AesStatus,
            RedisConfigured = !string.IsNullOrWhiteSpace(_options.RedisConnectionString),
            EndpointPrefix = _options.EndpointPrefix
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<CaptchaResponse>> Login([FromBody] DemoLoginRequest request, CancellationToken cancellationToken)
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
            request.CaptchaType,
            loggedIn = true,
            message = "模拟登录成功"
        }));
    }
}

public sealed class DemoLoginRequest
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? CaptchaType { get; set; } = CaptchaTypes.BlockPuzzle;
    public string? CaptchaVerification { get; set; }
}

public sealed class DemoStatusResponse
{
    public string Environment { get; set; } = string.Empty;
    public string CacheType { get; set; } = string.Empty;
    public string DefaultCaptchaType { get; set; } = string.Empty;
    public bool AesStatus { get; set; }
    public bool RedisConfigured { get; set; }
    public string EndpointPrefix { get; set; } = string.Empty;
}
