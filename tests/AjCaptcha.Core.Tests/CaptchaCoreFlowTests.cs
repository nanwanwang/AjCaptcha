using AjCaptcha.Core.Abstractions;
using AjCaptcha.Core.Infrastructure;
using AjCaptcha.Core.Models;
using AjCaptcha.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;

namespace AjCaptcha.Core.Tests;

public class CaptchaCoreFlowTests
{
    [Fact]
    public async Task BlockPuzzleRoundTrip_WithAesEnabled_Works()
    {
        var (service, cache) = CreateService();
        await using var _ = cache;

        var getResponse = await service.GetAsync(new CaptchaRequest
        {
            CaptchaType = CaptchaTypes.BlockPuzzle,
            ClientUid = "slider"
        });

        Assert.True(getResponse.Success);
        var payload = Assert.IsType<CaptchaPayload>(getResponse.RepData);
        Assert.False(string.IsNullOrWhiteSpace(payload.Token));
        Assert.False(string.IsNullOrWhiteSpace(payload.SecretKey));

        var cachedJson = await cache.GetAsync(CaptchaCacheKeys.Captcha(payload.Token!));
        var point = CaptchaJson.Deserialize<CaptchaPoint>(cachedJson!);
        var pointJson = CaptchaJson.Serialize(new CaptchaPoint { X = point!.X, Y = point.Y });
        var encrypted = CaptchaCrypto.Encrypt(pointJson, point.SecretKey);

        var checkResponse = await service.CheckAsync(new CaptchaCheckRequest
        {
            CaptchaType = CaptchaTypes.BlockPuzzle,
            Token = payload.Token,
            PointJson = encrypted,
            ClientUid = "slider"
        });

        Assert.True(checkResponse.Success);

        var verifyResponse = await service.VerifyAsync(new CaptchaVerifyRequest
        {
            CaptchaType = CaptchaTypes.BlockPuzzle,
            CaptchaVerification = CaptchaCrypto.Encrypt($"{payload.Token}---{pointJson}", point.SecretKey),
            ClientUid = "slider"
        });

        Assert.True(verifyResponse.Success);
    }

    [Fact]
    public async Task BlockPuzzleRoundTrip_WithPlainJson_Works()
    {
        var (service, cache) = CreateService(options => options.AesStatus = false);
        await using var _ = cache;

        var getResponse = await service.GetAsync(new CaptchaRequest
        {
            CaptchaType = CaptchaTypes.BlockPuzzle,
            ClientUid = "slider"
        });

        var payload = Assert.IsType<CaptchaPayload>(getResponse.RepData);
        Assert.True(string.IsNullOrWhiteSpace(payload.SecretKey));

        var cachedJson = await cache.GetAsync(CaptchaCacheKeys.Captcha(payload.Token!));
        var point = CaptchaJson.Deserialize<CaptchaPoint>(cachedJson!);
        var pointJson = CaptchaJson.Serialize(new CaptchaPoint { X = point!.X, Y = point.Y });

        var checkResponse = await service.CheckAsync(new CaptchaCheckRequest
        {
            CaptchaType = CaptchaTypes.BlockPuzzle,
            Token = payload.Token,
            PointJson = pointJson,
            ClientUid = "slider"
        });

        Assert.True(checkResponse.Success);
    }

    [Fact]
    public async Task ClickWordRoundTrip_Works()
    {
        var (service, cache) = CreateService();
        await using var _ = cache;

        var getResponse = await service.GetAsync(new CaptchaRequest
        {
            CaptchaType = CaptchaTypes.ClickWord,
            ClientUid = "point"
        });

        Assert.True(getResponse.Success);
        var payload = Assert.IsType<CaptchaPayload>(getResponse.RepData);
        Assert.NotNull(payload.WordList);
        Assert.Equal(3, payload.WordList!.Count);

        var cachedJson = await cache.GetAsync(CaptchaCacheKeys.Captcha(payload.Token!));
        var points = CaptchaJson.Deserialize<List<CaptchaPoint>>(cachedJson!);
        Assert.NotNull(points);
        Assert.NotEmpty(points!);
        var pointJson = CaptchaJson.Serialize(points!.Select(x => new CaptchaPoint { X = x.X, Y = x.Y }).ToList());
        var encrypted = CaptchaCrypto.Encrypt(pointJson, points[0].SecretKey);

        var checkResponse = await service.CheckAsync(new CaptchaCheckRequest
        {
            CaptchaType = CaptchaTypes.ClickWord,
            Token = payload.Token,
            PointJson = encrypted,
            ClientUid = "point"
        });

        Assert.True(checkResponse.Success);

        var verifyResponse = await service.VerifyAsync(new CaptchaVerifyRequest
        {
            CaptchaType = CaptchaTypes.ClickWord,
            CaptchaVerification = CaptchaCrypto.Encrypt($"{payload.Token}---{pointJson}", points[0].SecretKey),
            ClientUid = "point"
        });

        Assert.True(verifyResponse.Success);
    }

    [Fact]
    public async Task ReusingTokenAfterCheck_Fails()
    {
        var (service, cache) = CreateService();
        await using var _ = cache;

        var getResponse = await service.GetAsync(new CaptchaRequest
        {
            CaptchaType = CaptchaTypes.BlockPuzzle,
            ClientUid = "slider"
        });

        var payload = Assert.IsType<CaptchaPayload>(getResponse.RepData);
        var cachedJson = await cache.GetAsync(CaptchaCacheKeys.Captcha(payload.Token!));
        var point = CaptchaJson.Deserialize<CaptchaPoint>(cachedJson!);
        var pointJson = CaptchaJson.Serialize(new CaptchaPoint { X = point!.X, Y = point.Y });
        var encrypted = CaptchaCrypto.Encrypt(pointJson, point.SecretKey);

        var firstCheck = await service.CheckAsync(new CaptchaCheckRequest
        {
            CaptchaType = CaptchaTypes.BlockPuzzle,
            Token = payload.Token,
            PointJson = encrypted,
            ClientUid = "slider"
        });

        var secondCheck = await service.CheckAsync(new CaptchaCheckRequest
        {
            CaptchaType = CaptchaTypes.BlockPuzzle,
            Token = payload.Token,
            PointJson = encrypted,
            ClientUid = "slider"
        });

        Assert.True(firstCheck.Success);
        Assert.Equal(CaptchaCode.ApiCaptchaInvalid.ToCode(), secondCheck.RepCode);
    }

    [Fact]
    public async Task WrongBlockPuzzleCoordinates_ReturnCoordinateError()
    {
        var (service, cache) = CreateService();
        await using var _ = cache;

        var getResponse = await service.GetAsync(new CaptchaRequest
        {
            CaptchaType = CaptchaTypes.BlockPuzzle,
            ClientUid = "slider"
        });

        var payload = Assert.IsType<CaptchaPayload>(getResponse.RepData);
        var cachedJson = await cache.GetAsync(CaptchaCacheKeys.Captcha(payload.Token!));
        var point = CaptchaJson.Deserialize<CaptchaPoint>(cachedJson!);
        var pointJson = CaptchaJson.Serialize(new CaptchaPoint
        {
            X = point!.X + 20,
            Y = point.Y
        });

        var checkResponse = await service.CheckAsync(new CaptchaCheckRequest
        {
            CaptchaType = CaptchaTypes.BlockPuzzle,
            Token = payload.Token,
            PointJson = CaptchaCrypto.Encrypt(pointJson, point.SecretKey),
            ClientUid = "slider"
        });

        Assert.False(checkResponse.Success);
        Assert.Equal(CaptchaCode.ApiCaptchaCoordinateError.ToCode(), checkResponse.RepCode);
    }

    [Fact]
    public async Task BlockPuzzleJigsawImage_KeepsTransparentBackground()
    {
        var (service, cache) = CreateService();
        await using var _ = cache;

        var getResponse = await service.GetAsync(new CaptchaRequest
        {
            CaptchaType = CaptchaTypes.BlockPuzzle,
            ClientUid = "slider"
        });

        var payload = Assert.IsType<CaptchaPayload>(getResponse.RepData);
        Assert.False(string.IsNullOrWhiteSpace(payload.JigsawImageBase64));

        using var bitmap = SKBitmap.Decode(Convert.FromBase64String(payload.JigsawImageBase64!));
        Assert.NotNull(bitmap);
        Assert.True(bitmap!.Height > bitmap.Width);

        var bottomPixel = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height - 5);
        Assert.Equal(0, bottomPixel.Alpha);
    }

    [Fact]
    public async Task VerificationCannotBeReplayed()
    {
        var (service, cache) = CreateService();
        await using var _ = cache;

        var getResponse = await service.GetAsync(new CaptchaRequest
        {
            CaptchaType = CaptchaTypes.BlockPuzzle,
            ClientUid = "slider"
        });

        var payload = Assert.IsType<CaptchaPayload>(getResponse.RepData);
        var cachedJson = await cache.GetAsync(CaptchaCacheKeys.Captcha(payload.Token!));
        var point = CaptchaJson.Deserialize<CaptchaPoint>(cachedJson!);
        var pointJson = CaptchaJson.Serialize(new CaptchaPoint { X = point!.X, Y = point.Y });
        var verification = CaptchaCrypto.Encrypt($"{payload.Token}---{pointJson}", point.SecretKey);

        var checkResponse = await service.CheckAsync(new CaptchaCheckRequest
        {
            CaptchaType = CaptchaTypes.BlockPuzzle,
            Token = payload.Token,
            PointJson = CaptchaCrypto.Encrypt(pointJson, point.SecretKey),
            ClientUid = "slider"
        });
        var firstVerify = await service.VerifyAsync(new CaptchaVerifyRequest
        {
            CaptchaType = CaptchaTypes.BlockPuzzle,
            CaptchaVerification = verification,
            ClientUid = "slider"
        });
        var secondVerify = await service.VerifyAsync(new CaptchaVerifyRequest
        {
            CaptchaType = CaptchaTypes.BlockPuzzle,
            CaptchaVerification = verification,
            ClientUid = "slider"
        });

        Assert.True(checkResponse.Success);
        Assert.True(firstVerify.Success);
        Assert.Equal(CaptchaCode.ApiCaptchaInvalid.ToCode(), secondVerify.RepCode);
    }

    [Fact]
    public async Task CheckRateLimit_ReturnsLimitCode_WhenExceeded()
    {
        var (service, cache) = CreateService(options =>
        {
            options.ReqFrequencyLimitEnable = true;
            options.ReqCheckMinuteLimit = 1;
        });
        await using var _ = cache;

        var firstCheck = await SolveBlockPuzzleAsync(service, cache, "slider");
        var secondCheck = await SolveBlockPuzzleAsync(service, cache, "slider");

        Assert.True(firstCheck.Success);
        Assert.Equal(CaptchaCode.ApiReqLimitCheckError.ToCode(), secondCheck.RepCode);
    }

    [Fact]
    public void WatermarkDisabled_DoesNotDraw()
    {
        var options = new AjCaptchaOptions
        {
            WaterMarkEnabled = false,
            WaterMark = "veichi"
        };
        using var bitmap = new SKBitmap(240, 120, true);
        bitmap.Erase(SKColors.Transparent);

        CreateWatermarkHandler(options).Draw(bitmap);

        Assert.False(HasVisiblePixel(bitmap));
    }

    [Fact]
    public void WatermarkColor_UsesConfiguredColor()
    {
        var options = new AjCaptchaOptions
        {
            WaterMark = "veichi",
            WaterMarkColor = "#FF0000",
            WaterMarkOpacity = 1,
            WaterMarkFontSize = 28,
            WaterMarkOffsetX = 12,
            WaterMarkOffsetY = 10
        };
        using var bitmap = new SKBitmap(240, 120, true);
        bitmap.Erase(SKColors.Transparent);

        CreateWatermarkHandler(options).Draw(bitmap);

        Assert.True(HasVisiblePixel(bitmap));
        Assert.True(HasColorPixel(bitmap, color => color.Red > color.Green && color.Red > color.Blue && color.Alpha > 0));
    }

    private static (AjCaptchaService service, MemoryCaptchaCacheStore cache) CreateService(Action<AjCaptchaOptions>? configure = null)
    {
        var options = new AjCaptchaOptions
        {
            InterferenceOptions = 0,
            ReqFrequencyLimitEnable = false
        };
        configure?.Invoke(options);

        var cache = new MemoryCaptchaCacheStore(options, NullLogger<MemoryCaptchaCacheStore>.Instance);
        ICaptchaImageProvider imageProvider = new CaptchaImageProvider(options, NullLogger<CaptchaImageProvider>.Instance);
        ICaptchaFontProvider fontProvider = new CaptchaFontProvider();
        var messages = new CaptchaMessageProvider(options);
        var handlers = new ICaptchaChallengeHandler[]
        {
            new BlockPuzzleCaptchaHandler(options, cache, imageProvider, fontProvider, messages, NullLogger<BlockPuzzleCaptchaHandler>.Instance),
            new ClickWordCaptchaHandler(options, cache, imageProvider, fontProvider, messages, NullLogger<ClickWordCaptchaHandler>.Instance)
        };

        return (new AjCaptchaService(handlers, messages, options), cache);
    }

    private static WatermarkTestHandler CreateWatermarkHandler(AjCaptchaOptions options)
    {
        var cache = new MemoryCaptchaCacheStore(options, NullLogger<MemoryCaptchaCacheStore>.Instance);
        ICaptchaImageProvider imageProvider = new CaptchaImageProvider(options, NullLogger<CaptchaImageProvider>.Instance);
        ICaptchaFontProvider fontProvider = new CaptchaFontProvider();
        var messages = new CaptchaMessageProvider(options);
        return new WatermarkTestHandler(options, cache, imageProvider, fontProvider, messages);
    }

    private static bool HasVisiblePixel(SKBitmap bitmap)
    {
        for (var x = 0; x < bitmap.Width; x++)
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                if (bitmap.GetPixel(x, y).Alpha > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasColorPixel(SKBitmap bitmap, Func<SKColor, bool> predicate)
    {
        for (var x = 0; x < bitmap.Width; x++)
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                if (predicate(bitmap.GetPixel(x, y)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static async Task<CaptchaResponse> SolveBlockPuzzleAsync(AjCaptchaService service, MemoryCaptchaCacheStore cache, string clientUid)
    {
        var getResponse = await service.GetAsync(new CaptchaRequest
        {
            CaptchaType = CaptchaTypes.BlockPuzzle,
            ClientUid = clientUid
        });

        var payload = Assert.IsType<CaptchaPayload>(getResponse.RepData);
        var cachedJson = await cache.GetAsync(CaptchaCacheKeys.Captcha(payload.Token!));
        var point = CaptchaJson.Deserialize<CaptchaPoint>(cachedJson!);
        var pointJson = CaptchaJson.Serialize(new CaptchaPoint { X = point!.X, Y = point.Y });

        return await service.CheckAsync(new CaptchaCheckRequest
        {
            CaptchaType = CaptchaTypes.BlockPuzzle,
            Token = payload.Token,
            PointJson = CaptchaCrypto.Encrypt(pointJson, point.SecretKey),
            ClientUid = clientUid
        });
    }

    private sealed class WatermarkTestHandler : CaptchaChallengeHandlerBase
    {
        public WatermarkTestHandler(
            AjCaptchaOptions options,
            ICaptchaCacheStore cacheStore,
            ICaptchaImageProvider imageProvider,
            ICaptchaFontProvider fontProvider,
            CaptchaMessageProvider messages)
            : base(options, cacheStore, imageProvider, fontProvider, messages, NullLogger<WatermarkTestHandler>.Instance)
        {
        }

        public override string CaptchaType => CaptchaTypes.Default;

        public void Draw(SKBitmap bitmap) => DrawWatermark(bitmap);

        public override Task<CaptchaResponse> GetAsync(CaptchaRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public override Task<CaptchaResponse> CheckAsync(CaptchaCheckRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
