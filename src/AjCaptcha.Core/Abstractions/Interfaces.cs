using AjCaptcha.Core.Models;
using SkiaSharp;

namespace AjCaptcha.Core.Abstractions;

public interface ICaptchaService
{
    Task<CaptchaResponse> GetAsync(CaptchaRequest request, CancellationToken cancellationToken = default);
    Task<CaptchaResponse> CheckAsync(CaptchaCheckRequest request, CancellationToken cancellationToken = default);
    Task<CaptchaResponse> VerifyAsync(CaptchaVerifyRequest request, CancellationToken cancellationToken = default);
}

public interface ICaptchaCacheStore : IAsyncDisposable
{
    Task SetAsync(string key, string value, TimeSpan? ttl = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task<long> IncrementAsync(string key, long value, CancellationToken cancellationToken = default);
    Task SetExpireAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default);
}

public interface ICaptchaChallengeHandler
{
    string CaptchaType { get; }
    Task<CaptchaResponse> GetAsync(CaptchaRequest request, CancellationToken cancellationToken = default);
    Task<CaptchaResponse> CheckAsync(CaptchaCheckRequest request, CancellationToken cancellationToken = default);
    Task<CaptchaResponse> VerifyAsync(CaptchaVerifyRequest request, CancellationToken cancellationToken = default);
}

public interface ICaptchaImageProvider
{
    SKBitmap? GetRandomJigsawOriginal();
    SKBitmap? GetRandomJigsawTemplate();
    SKBitmap? GetRandomPicClickBackground();
}

public interface ICaptchaFontProvider
{
    SKTypeface GetWatermarkTypeface(string? fontNameOrPath);
    SKTypeface GetTextTypeface(string? fontNameOrPath);
}
