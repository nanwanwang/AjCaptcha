using AjCaptcha.Core.Abstractions;
using AjCaptcha.Core.Infrastructure;
using AjCaptcha.Core.Models;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace AjCaptcha.Core.Services;

public sealed class ClickWordCaptchaHandler : CaptchaChallengeHandlerBase
{
    public ClickWordCaptchaHandler(
        AjCaptchaOptions options,
        ICaptchaCacheStore cacheStore,
        ICaptchaImageProvider imageProvider,
        ICaptchaFontProvider fontProvider,
        CaptchaMessageProvider messages,
        ILogger<ClickWordCaptchaHandler> logger)
        : base(options, cacheStore, imageProvider, fontProvider, messages, logger)
    {
    }

    public override string CaptchaType => CaptchaTypes.ClickWord;

    public override async Task<CaptchaResponse> GetAsync(CaptchaRequest request, CancellationToken cancellationToken = default)
    {
        var limitResult = await ValidateGetLimitAsync(request, cancellationToken).ConfigureAwait(false);
        if (limitResult is not null)
        {
            return limitResult;
        }

        using var background = ImageProvider.GetRandomPicClickBackground();
        if (background is null)
        {
            return Failure(CaptchaCode.ApiCaptchaBaseMapNull);
        }

        var wordCount = Math.Max(4, Options.ClickWordCount);
        var skipIndex = RandomInt(0, wordCount);
        var words = GetRandomWords(wordCount);
        var points = new List<CaptchaPoint>();
        var shownWords = new List<string>();
        var secretKey = Options.AesStatus ? CaptchaCrypto.GenerateKey() : null;

        using var canvas = new SKCanvas(background);
        for (var index = 0; index < words.Count; index++)
        {
            var point = RandomWordPoint(background.Width, background.Height, index, words.Count);
            point.SecretKey = secretKey;
            using var paint = new SKPaint
            {
                IsAntialias = true,
                Color = new SKColor(
                    (byte)RandomInt(1, 255),
                    (byte)RandomInt(1, 255),
                    (byte)RandomInt(1, 255)),
                Typeface = FontProvider.GetTextTypeface(Options.FontType),
                TextSize = Options.FontSize,
                FakeBoldText = Options.FontStyle == 1
            };

            canvas.Save();
            canvas.Translate((float)point.X, (float)point.Y);
            canvas.RotateDegrees(RandomInt(-45, 46));
            canvas.DrawText(words[index], 0, 0, paint);
            canvas.Restore();

            if (index != skipIndex)
            {
                shownWords.Add(words[index]);
                points.Add(point);
            }
        }

        DrawWatermark(background);
        var token = CaptchaCrypto.GenerateToken();
        await CacheStore.SetAsync(CaptchaCacheKeys.Captcha(token), CaptchaJson.Serialize(points), Options.CaptchaExpire, cancellationToken).ConfigureAwait(false);

        return Success(new CaptchaPayload
        {
            CaptchaType = CaptchaType,
            OriginalImageBase64 = EncodeBitmap(background),
            Token = token,
            SecretKey = secretKey,
            WordList = shownWords,
            Result = false,
            OpAdmin = false
        });
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
            var expectedPoints = CaptchaJson.Deserialize<List<CaptchaPoint>>(pointValue)!;
            var secretKey = expectedPoints.FirstOrDefault()?.SecretKey;
            var normalizedJson = NormalizePointJson(request.PointJson!, secretKey);
            var actualPoints = CaptchaJson.Deserialize<List<CaptchaPoint>>(normalizedJson)!;
            if (expectedPoints.Count != actualPoints.Count)
            {
                await AfterValidateFailAsync(request, cancellationToken).ConfigureAwait(false);
                return Failure(CaptchaCode.ApiCaptchaCoordinateError);
            }

            for (var index = 0; index < expectedPoints.Count; index++)
            {
                var expected = expectedPoints[index];
                var actual = actualPoints[index];
                var xMatches = actual.X >= expected.X - Options.FontSize && actual.X <= expected.X + Options.FontSize;
                var yMatches = actual.Y >= expected.Y - Options.FontSize && actual.Y <= expected.Y + Options.FontSize;
                if (!xMatches || !yMatches)
                {
                    await AfterValidateFailAsync(request, cancellationToken).ConfigureAwait(false);
                    return Failure(CaptchaCode.ApiCaptchaCoordinateError);
                }
            }

            await CacheVerificationAsync(request.Token!, normalizedJson, secretKey, cancellationToken).ConfigureAwait(false);
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
            Logger.LogWarning(ex, "Failed to validate click-word captcha.");
            await AfterValidateFailAsync(request, cancellationToken).ConfigureAwait(false);
            return Failure(CaptchaCode.Error, ex.Message);
        }
    }

    private List<string> GetRandomWords(int count)
    {
        var result = new List<string>(count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (result.Count < count)
        {
            var next = CaptchaCorpus.HanZi[RandomInt(0, CaptchaCorpus.HanZi.Length)].ToString();
            if (seen.Add(next))
            {
                result.Add(next);
            }
        }

        return result;
    }

    private CaptchaPoint RandomWordPoint(int imageWidth, int imageHeight, int wordIndex, int wordCount)
    {
        var avgWidth = imageWidth / (wordCount + 1);
        var fontHalf = Math.Max(1, Options.FontSize / 2);
        int x;
        if (avgWidth < fontHalf)
        {
            x = RandomInt(fontHalf + 1, imageWidth);
        }
        else if (wordIndex == 0)
        {
            x = RandomInt(fontHalf + 1, Math.Max(fontHalf + 2, avgWidth * (wordIndex + 1) - fontHalf));
        }
        else
        {
            x = RandomInt(avgWidth * wordIndex + fontHalf, Math.Max(avgWidth * wordIndex + fontHalf + 1, avgWidth * (wordIndex + 1) - fontHalf));
        }

        var y = RandomInt(Options.FontSize, Math.Max(Options.FontSize + 1, imageHeight - fontHalf));
        return new CaptchaPoint { X = x, Y = y };
    }
}
