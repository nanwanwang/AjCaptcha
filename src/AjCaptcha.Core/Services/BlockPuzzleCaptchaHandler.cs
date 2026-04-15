using AjCaptcha.Core.Abstractions;
using AjCaptcha.Core.Infrastructure;
using AjCaptcha.Core.Models;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace AjCaptcha.Core.Services;

public sealed class BlockPuzzleCaptchaHandler : CaptchaChallengeHandlerBase
{
    public BlockPuzzleCaptchaHandler(
        AjCaptchaOptions options,
        ICaptchaCacheStore cacheStore,
        ICaptchaImageProvider imageProvider,
        ICaptchaFontProvider fontProvider,
        CaptchaMessageProvider messages,
        ILogger<BlockPuzzleCaptchaHandler> logger)
        : base(options, cacheStore, imageProvider, fontProvider, messages, logger)
    {
    }

    public override string CaptchaType => CaptchaTypes.BlockPuzzle;

    public override async Task<CaptchaResponse> GetAsync(CaptchaRequest request, CancellationToken cancellationToken = default)
    {
        var limitResult = await ValidateGetLimitAsync(request, cancellationToken).ConfigureAwait(false);
        if (limitResult is not null)
        {
            return limitResult;
        }

        using var original = ImageProvider.GetRandomJigsawOriginal();
        using var template = ImageProvider.GetRandomJigsawTemplate();
        if (original is null || template is null)
        {
            return Failure(CaptchaCode.ApiCaptchaBaseMapNull);
        }

        DrawWatermark(original);
        var point = GeneratePoint(original.Width, original.Height, template.Width, template.Height);
        using var output = new SKBitmap(template.Width, template.Height, false);
        output.Erase(SKColors.Transparent);
        CutByTemplate(original, template, output, (int)point.X, 0);

        if (Options.InterferenceOptions > 0)
        {
            using var interference = ImageProvider.GetRandomJigsawTemplate();
            if (interference is not null)
            {
                var position = original.Width - point.X - 5 > template.Width * 2
                    ? RandomInt((int)point.X + template.Width + 5, Math.Max((int)point.X + template.Width + 6, original.Width - template.Width))
                    : RandomInt(0, Math.Max(1, (int)point.X - template.Width - 5));
                InterferenceByTemplate(original, interference, position, 0);
            }
        }

        if (Options.InterferenceOptions > 1)
        {
            using var interference = ImageProvider.GetRandomJigsawTemplate();
            if (interference is not null)
            {
                var maxX = Math.Max(template.Width + 1, original.Width - template.Width);
                var position = RandomInt(template.Width, maxX);
                InterferenceByTemplate(original, interference, position, 0);
            }
        }

        var token = CaptchaCrypto.GenerateToken();
        var payload = new CaptchaPayload
        {
            CaptchaType = CaptchaType,
            OriginalImageBase64 = EncodeBitmap(original),
            JigsawImageBase64 = EncodeBitmap(output),
            Token = token,
            SecretKey = point.SecretKey,
            Result = false,
            OpAdmin = false
        };

        await CacheStore.SetAsync(CaptchaCacheKeys.Captcha(token), CaptchaJson.Serialize(point), Options.CaptchaExpire, cancellationToken).ConfigureAwait(false);
        return Success(payload);
    }

    public override async Task<CaptchaResponse> CheckAsync(CaptchaCheckRequest request, CancellationToken cancellationToken = default)
    {
        var limitResult = await ValidateCheckLimitAsync(request, cancellationToken).ConfigureAwait(false);
        if (limitResult is not null)
        {
            return limitResult;
        }

        var key = CaptchaCacheKeys.Captcha(request.Token!);
        var pointValue = await CacheStore.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(pointValue))
        {
            return Failure(CaptchaCode.ApiCaptchaInvalid);
        }

        await CacheStore.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
        try
        {
            var expectedPoint = CaptchaJson.Deserialize<CaptchaPoint>(pointValue)!;
            var normalizedJson = NormalizePointJson(request.PointJson!, expectedPoint.SecretKey);
            var actualPoint = CaptchaJson.Deserialize<CaptchaPoint>(normalizedJson)!;

            var xMatches = Math.Abs(expectedPoint.X - actualPoint.X) <= Options.SlipOffset;
            var yMatches = Math.Abs(expectedPoint.Y - actualPoint.Y) < 0.001;
            if (!xMatches || !yMatches)
            {
                await AfterValidateFailAsync(request, cancellationToken).ConfigureAwait(false);
                return Failure(CaptchaCode.ApiCaptchaCoordinateError);
            }

            await CacheVerificationAsync(request.Token!, normalizedJson, expectedPoint.SecretKey, cancellationToken).ConfigureAwait(false);
            return Success(new CaptchaPayload
            {
                CaptchaType = CaptchaType,
                Token = request.Token,
                Result = true,
                OpAdmin = false
            });
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to validate block puzzle captcha.");
            await AfterValidateFailAsync(request, cancellationToken).ConfigureAwait(false);
            return Failure(CaptchaCode.Error, ex.Message);
        }
    }

    private CaptchaPoint GeneratePoint(int originalWidth, int originalHeight, int templateWidth, int templateHeight)
    {
        var widthDifference = originalWidth - templateWidth;
        var heightDifference = originalHeight - templateHeight;
        var x = widthDifference <= 0
            ? 5
            : widthDifference <= 100
                ? RandomInt(5, Math.Max(6, widthDifference))
                : RandomInt(100, widthDifference);
        var y = heightDifference <= 0 ? 5 : RandomInt(5, Math.Max(6, heightDifference));
        return new CaptchaPoint
        {
            X = x,
            Y = y,
            SecretKey = Options.AesStatus ? CaptchaCrypto.GenerateKey() : null
        };
    }

    private static void CutByTemplate(SKBitmap original, SKBitmap template, SKBitmap output, int offsetX, int offsetY)
    {
        for (var x = 0; x < template.Width; x++)
        {
            for (var y = 0; y < template.Height; y++)
            {
                var opaque = IsOpaque(template, x, y);
                if (opaque)
                {
                    output.SetPixel(x, y, original.GetPixel(offsetX + x, offsetY + y));
                    BlurPixel(original, offsetX + x, offsetY + y);
                }

                if (x == template.Width - 1 || y == template.Height - 1)
                {
                    continue;
                }

                var right = IsOpaque(template, x + 1, y);
                var down = IsOpaque(template, x, y + 1);
                if (opaque != right || opaque != down)
                {
                    output.SetPixel(x, y, SKColors.White);
                    original.SetPixel(offsetX + x, offsetY + y, SKColors.White);
                }
            }
        }
    }

    private static void InterferenceByTemplate(SKBitmap original, SKBitmap template, int offsetX, int offsetY)
    {
        for (var x = 0; x < template.Width; x++)
        {
            for (var y = 0; y < template.Height; y++)
            {
                var opaque = IsOpaque(template, x, y);
                if (opaque)
                {
                    BlurPixel(original, Math.Clamp(offsetX + x, 0, original.Width - 1), Math.Clamp(offsetY + y, 0, original.Height - 1));
                }

                if (x == template.Width - 1 || y == template.Height - 1)
                {
                    continue;
                }

                var right = IsOpaque(template, x + 1, y);
                var down = IsOpaque(template, x, y + 1);
                if (opaque != right || opaque != down)
                {
                    original.SetPixel(Math.Clamp(offsetX + x, 0, original.Width - 1), Math.Clamp(offsetY + y, 0, original.Height - 1), SKColors.White);
                }
            }
        }
    }
}
