using AjCaptcha.Core.Abstractions;
using AjCaptcha.Core.Infrastructure;
using AjCaptcha.Core.Models;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System.Globalization;
using System.Security.Cryptography;

namespace AjCaptcha.Core.Services;

public abstract class CaptchaChallengeHandlerBase : ICaptchaChallengeHandler
{
    protected readonly AjCaptchaOptions Options;
    protected readonly ICaptchaCacheStore CacheStore;
    protected readonly ICaptchaImageProvider ImageProvider;
    protected readonly ICaptchaFontProvider FontProvider;
    protected readonly CaptchaMessageProvider Messages;
    protected readonly ILogger Logger;

    protected CaptchaChallengeHandlerBase(
        AjCaptchaOptions options,
        ICaptchaCacheStore cacheStore,
        ICaptchaImageProvider imageProvider,
        ICaptchaFontProvider fontProvider,
        CaptchaMessageProvider messages,
        ILogger logger)
    {
        Options = options;
        CacheStore = cacheStore;
        ImageProvider = imageProvider;
        FontProvider = fontProvider;
        Messages = messages;
        Logger = logger;
    }

    public abstract string CaptchaType { get; }
    public abstract Task<CaptchaResponse> GetAsync(CaptchaRequest request, CancellationToken cancellationToken = default);
    public abstract Task<CaptchaResponse> CheckAsync(CaptchaCheckRequest request, CancellationToken cancellationToken = default);

    public virtual async Task<CaptchaResponse> VerifyAsync(CaptchaVerifyRequest request, CancellationToken cancellationToken = default)
    {
        var limitResult = await ValidateVerifyLimitAsync(request, cancellationToken).ConfigureAwait(false);
        if (limitResult is not null)
        {
            return limitResult;
        }

        var verificationKey = CaptchaCacheKeys.Verification(request.CaptchaVerification!);
        if (!await CacheStore.ExistsAsync(verificationKey, cancellationToken).ConfigureAwait(false))
        {
            return Failure(CaptchaCode.ApiCaptchaInvalid);
        }

        await CacheStore.DeleteAsync(verificationKey, cancellationToken).ConfigureAwait(false);
        return CaptchaResponse.SuccessMessage(Messages.Get(CaptchaCode.Success));
    }

    protected CaptchaResponse Success(object data) => CaptchaResponse.SuccessData(data);

    protected CaptchaResponse Failure(CaptchaCode code, params object[] args)
        => CaptchaResponse.Failure(code, Messages.Get(code, args));

    protected async Task<CaptchaResponse?> ValidateGetLimitAsync(CaptchaClientRequest request, CancellationToken cancellationToken)
    {
        if (!Options.ReqFrequencyLimitEnable)
        {
            return null;
        }

        var clientUid = CaptchaClientIdentity.ResolveClientUid(request);
        if (string.IsNullOrWhiteSpace(clientUid))
        {
            return null;
        }

        var lockKey = CaptchaCacheKeys.RateLimit("LOCK", clientUid);
        if (await CacheStore.ExistsAsync(lockKey, cancellationToken).ConfigureAwait(false))
        {
            return Failure(CaptchaCode.ApiReqLockGetError);
        }

        var getKey = CaptchaCacheKeys.RateLimit("GET", clientUid);
        var count = await CacheStore.IncrementAsync(getKey, 1L, cancellationToken).ConfigureAwait(false);
        if (count <= 1)
        {
            await CacheStore.SetExpireAsync(getKey, TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
        }

        if (count > Options.ReqGetMinuteLimit)
        {
            return Failure(CaptchaCode.ApiReqLimitGetError);
        }

        var failKey = CaptchaCacheKeys.RateLimit("FAIL", clientUid);
        var failValue = await CacheStore.GetAsync(failKey, cancellationToken).ConfigureAwait(false);
        if (long.TryParse(failValue, out var failCount) && failCount > Options.ReqGetLockLimit)
        {
            await CacheStore.SetAsync(lockKey, "1", TimeSpan.FromSeconds(Options.ReqGetLockSeconds), cancellationToken).ConfigureAwait(false);
            return Failure(CaptchaCode.ApiReqLockGetError);
        }

        return null;
    }

    protected async Task<CaptchaResponse?> ValidateCheckLimitAsync(CaptchaClientRequest request, CancellationToken cancellationToken)
    {
        if (!Options.ReqFrequencyLimitEnable)
        {
            return null;
        }

        var clientUid = CaptchaClientIdentity.ResolveClientUid(request);
        if (string.IsNullOrWhiteSpace(clientUid))
        {
            return null;
        }

        var key = CaptchaCacheKeys.RateLimit("CHECK", clientUid);
        var count = await CacheStore.IncrementAsync(key, 1L, cancellationToken).ConfigureAwait(false);
        if (count <= 1)
        {
            await CacheStore.SetExpireAsync(key, TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
        }

        return count > Options.ReqCheckMinuteLimit ? Failure(CaptchaCode.ApiReqLimitCheckError) : null;
    }

    protected async Task<CaptchaResponse?> ValidateVerifyLimitAsync(CaptchaClientRequest request, CancellationToken cancellationToken)
    {
        if (!Options.ReqFrequencyLimitEnable)
        {
            return null;
        }

        var clientUid = CaptchaClientIdentity.ResolveClientUid(request);
        if (string.IsNullOrWhiteSpace(clientUid))
        {
            return null;
        }

        var key = CaptchaCacheKeys.RateLimit("VERIFY", clientUid);
        var count = await CacheStore.IncrementAsync(key, 1L, cancellationToken).ConfigureAwait(false);
        if (count <= 1)
        {
            await CacheStore.SetExpireAsync(key, TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
        }

        return count > Options.ReqVerifyMinuteLimit ? Failure(CaptchaCode.ApiReqLimitVerifyError) : null;
    }

    protected async Task AfterValidateFailAsync(CaptchaClientRequest request, CancellationToken cancellationToken)
    {
        if (!Options.ReqFrequencyLimitEnable)
        {
            return;
        }

        var clientUid = CaptchaClientIdentity.ResolveClientUid(request);
        if (string.IsNullOrWhiteSpace(clientUid))
        {
            return;
        }

        var failKey = CaptchaCacheKeys.RateLimit("FAIL", clientUid);
        var count = await CacheStore.IncrementAsync(failKey, 1L, cancellationToken).ConfigureAwait(false);
        if (count <= 1)
        {
            await CacheStore.SetExpireAsync(failKey, TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
        }
    }

    protected void DrawWatermark(SKBitmap bitmap)
    {
        if (!Options.WaterMarkEnabled || string.IsNullOrWhiteSpace(Options.WaterMark))
        {
            return;
        }

        var textSize = Options.WaterMarkFontSize > 0f
            ? Options.WaterMarkFontSize
            : Math.Max(Options.FontSize / 2f, 12f);
        var baseColor = ParseColor(Options.WaterMarkColor, SKColors.White);
        var opacity = Math.Clamp(Options.WaterMarkOpacity, 0d, 1d);
        var alpha = (byte)Math.Clamp((int)Math.Round(baseColor.Alpha * opacity), 0, byte.MaxValue);

        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint
        {
            Color = baseColor.WithAlpha(alpha),
            IsAntialias = true,
            TextSize = textSize,
            Typeface = FontProvider.GetWatermarkTypeface(Options.WaterFont)
        };

        var width = paint.MeasureText(Options.WaterMark);
        var x = Math.Max(2f, bitmap.Width - width - Math.Max(0f, Options.WaterMarkOffsetX));
        var y = Math.Max(paint.TextSize, bitmap.Height - Math.Max(0f, Options.WaterMarkOffsetY));
        canvas.DrawText(Options.WaterMark, x, y, paint);
    }

    private static SKColor ParseColor(string? colorValue, SKColor fallback)
    {
        if (string.IsNullOrWhiteSpace(colorValue))
        {
            return fallback;
        }

        var trimmed = colorValue.Trim();
        if (SKColor.TryParse(trimmed, out var parsed))
        {
            return parsed;
        }

        var segments = trimmed.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length is 3 or 4
            && byte.TryParse(segments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var red)
            && byte.TryParse(segments[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var green)
            && byte.TryParse(segments[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var blue))
        {
            if (segments.Length == 4
                && byte.TryParse(segments[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var alpha))
            {
                return new SKColor(red, green, blue, alpha);
            }

            return new SKColor(red, green, blue);
        }

        return fallback;
    }

    protected static string EncodeBitmap(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return Convert.ToBase64String(data.ToArray());
    }

    protected static string NormalizePointJson(string pointJson, string? secretKey)
    {
        if (string.IsNullOrWhiteSpace(pointJson))
        {
            throw new InvalidOperationException("pointJson is required.");
        }

        return CaptchaCrypto.Decrypt(pointJson, secretKey);
    }

    protected static int RandomInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            return minInclusive;
        }

        return RandomNumberGenerator.GetInt32(minInclusive, maxExclusive);
    }

    protected static bool IsOpaque(SKBitmap bitmap, int x, int y) => bitmap.GetPixel(x, y).Alpha > 0;

    protected static SKColor GetPixelClamped(SKBitmap bitmap, int x, int y)
    {
        var clampedX = Math.Clamp(x, 0, bitmap.Width - 1);
        var clampedY = Math.Clamp(y, 0, bitmap.Height - 1);
        return bitmap.GetPixel(clampedX, clampedY);
    }

    protected static void BlurPixel(SKBitmap bitmap, int x, int y)
    {
        var r = 0;
        var g = 0;
        var b = 0;
        var count = 0;

        for (var offsetX = -1; offsetX <= 1; offsetX++)
        {
            for (var offsetY = -1; offsetY <= 1; offsetY++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                var color = GetPixelClamped(bitmap, x + offsetX, y + offsetY);
                r += color.Red;
                g += color.Green;
                b += color.Blue;
                count++;
            }
        }

        bitmap.SetPixel(x, y, new SKColor((byte)(r / count), (byte)(g / count), (byte)(b / count), 255));
    }

    protected async Task CacheVerificationAsync(string token, string pointJson, string? secretKey, CancellationToken cancellationToken)
    {
        var value = CaptchaCrypto.Encrypt($"{token}---{pointJson}", secretKey);
        await CacheStore.SetAsync(CaptchaCacheKeys.Verification(value), token, Options.VerifyExpire, cancellationToken).ConfigureAwait(false);
    }
}
