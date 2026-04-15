using AjCaptcha.Core.Abstractions;
using AjCaptcha.Core.Infrastructure;
using AjCaptcha.Core.Models;

namespace AjCaptcha.Core.Services;

public sealed class AjCaptchaService : ICaptchaService
{
    private readonly Dictionary<string, ICaptchaChallengeHandler> _handlers;
    private readonly CaptchaMessageProvider _messages;
    private readonly AjCaptchaOptions _options;

    public AjCaptchaService(IEnumerable<ICaptchaChallengeHandler> handlers, CaptchaMessageProvider messages, AjCaptchaOptions options)
    {
        _handlers = handlers.ToDictionary(x => x.CaptchaType, StringComparer.OrdinalIgnoreCase);
        _messages = messages;
        _options = options;
    }

    public Task<CaptchaResponse> GetAsync(CaptchaRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Task.FromResult(Failure(CaptchaCode.NullError, "captchaVO"));
        }

        if (string.IsNullOrWhiteSpace(request.CaptchaType))
        {
            return Task.FromResult(Failure(CaptchaCode.NullError, "captchaType"));
        }

        return TryResolveHandler(request.CaptchaType, out var handler)
            ? handler.GetAsync(request, cancellationToken)
            : Task.FromResult(Failure(CaptchaCode.ParamTypeError, "captchaType"));
    }

    public Task<CaptchaResponse> CheckAsync(CaptchaCheckRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Task.FromResult(Failure(CaptchaCode.NullError, "captchaVO"));
        }

        if (string.IsNullOrWhiteSpace(request.CaptchaType))
        {
            return Task.FromResult(Failure(CaptchaCode.NullError, "captchaType"));
        }

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Task.FromResult(Failure(CaptchaCode.NullError, "token"));
        }

        return TryResolveHandler(request.CaptchaType, out var handler)
            ? handler.CheckAsync(request, cancellationToken)
            : Task.FromResult(Failure(CaptchaCode.ParamTypeError, "captchaType"));
    }

    public Task<CaptchaResponse> VerifyAsync(CaptchaVerifyRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Task.FromResult(Failure(CaptchaCode.NullError, "captchaVO"));
        }

        if (string.IsNullOrWhiteSpace(request.CaptchaVerification))
        {
            return Task.FromResult(Failure(CaptchaCode.NullError, "captchaVerification"));
        }

        var captchaType = string.IsNullOrWhiteSpace(request.CaptchaType)
            ? (_options.Type.Equals(CaptchaTypes.Default, StringComparison.OrdinalIgnoreCase) ? CaptchaTypes.BlockPuzzle : _options.Type)
            : request.CaptchaType;

        request.CaptchaType = captchaType;
        return TryResolveHandler(captchaType, out var handler)
            ? handler.VerifyAsync(request, cancellationToken)
            : Task.FromResult(Failure(CaptchaCode.ParamTypeError, "captchaType"));
    }

    private bool TryResolveHandler(string captchaType, out ICaptchaChallengeHandler handler)
        => _handlers.TryGetValue(captchaType, out handler!);

    private CaptchaResponse Failure(CaptchaCode code, params object[] args)
        => CaptchaResponse.Failure(code, _messages.Get(code, args));
}
