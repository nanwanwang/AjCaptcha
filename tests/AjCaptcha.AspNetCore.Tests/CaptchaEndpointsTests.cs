using System.Net.Http.Json;
using System.Text.Json;
using AjCaptcha.Core.Abstractions;
using AjCaptcha.Core.Infrastructure;
using AjCaptcha.Core.Models;
using AjCaptcha.Sample;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AjCaptcha.AspNetCore.Tests;

public class CaptchaEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly WebApplicationFactory<Program> _factory;

    public CaptchaEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task BlockPuzzleEndpoints_KeepProtocolCompatible()
    {
        using var client = _factory.CreateClient();

        var getResponse = await client.PostAsJsonAsync("/captcha/get", new
        {
            captchaType = CaptchaTypes.BlockPuzzle,
            clientUid = "slider",
            ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });

        var getPayload = await getResponse.Content.ReadFromJsonAsync<CaptchaResponseEnvelope>(JsonOptions);
        Assert.NotNull(getPayload);
        Assert.Equal("0000", getPayload!.RepCode);
        Assert.True(getPayload.Success);
        Assert.False(getPayload.Error);

        var token = getPayload.RepData.GetProperty("token").GetString();
        var secretKey = getPayload.RepData.GetProperty("secretKey").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        var cache = _factory.Services.GetRequiredService<ICaptchaCacheStore>();
        var cachedJson = await cache.GetAsync(CaptchaCacheKeys.Captcha(token!));
        var point = CaptchaJson.Deserialize<CaptchaPoint>(cachedJson!);
        var pointJson = CaptchaJson.Serialize(new CaptchaPoint { X = point!.X, Y = point.Y });
        var encrypted = CaptchaCrypto.Encrypt(pointJson, secretKey);

        var checkResponse = await client.PostAsJsonAsync("/captcha/check", new
        {
            captchaType = CaptchaTypes.BlockPuzzle,
            token,
            pointJson = encrypted,
            clientUid = "slider"
        });
        var checkPayload = await checkResponse.Content.ReadFromJsonAsync<CaptchaResponseEnvelope>(JsonOptions);
        Assert.NotNull(checkPayload);
        Assert.Equal("0000", checkPayload!.RepCode);

        var loginResponse = await client.PostAsJsonAsync("/login", new
        {
            username = "demo",
            password = "secret",
            captchaType = CaptchaTypes.BlockPuzzle,
            captchaVerification = CaptchaCrypto.Encrypt($"{token}---{pointJson}", secretKey)
        });
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<CaptchaResponseEnvelope>(JsonOptions);
        Assert.NotNull(loginPayload);
        Assert.Equal("0000", loginPayload!.RepCode);
        Assert.True(loginPayload.RepData.GetProperty("loggedIn").GetBoolean());
    }

    [Fact]
    public async Task ClickWordEndpoints_WorkEndToEnd()
    {
        using var client = _factory.CreateClient();

        var getResponse = await client.PostAsJsonAsync("/captcha/get", new
        {
            captchaType = CaptchaTypes.ClickWord,
            clientUid = "point",
            ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });

        var getPayload = await getResponse.Content.ReadFromJsonAsync<CaptchaResponseEnvelope>(JsonOptions);
        Assert.NotNull(getPayload);
        Assert.Equal("0000", getPayload!.RepCode);
        Assert.Equal(3, getPayload.RepData.GetProperty("wordList").GetArrayLength());

        var token = getPayload.RepData.GetProperty("token").GetString();
        var secretKey = getPayload.RepData.GetProperty("secretKey").GetString();
        var cache = _factory.Services.GetRequiredService<ICaptchaCacheStore>();
        var cachedJson = await cache.GetAsync(CaptchaCacheKeys.Captcha(token!));
        var points = CaptchaJson.Deserialize<List<CaptchaPoint>>(cachedJson!);
        var pointJson = CaptchaJson.Serialize(points!.Select(x => new CaptchaPoint { X = x.X, Y = x.Y }).ToList());
        var encrypted = CaptchaCrypto.Encrypt(pointJson, secretKey);

        var checkResponse = await client.PostAsJsonAsync("/captcha/check", new
        {
            captchaType = CaptchaTypes.ClickWord,
            token,
            pointJson = encrypted,
            clientUid = "point"
        });
        var checkPayload = await checkResponse.Content.ReadFromJsonAsync<CaptchaResponseEnvelope>(JsonOptions);
        Assert.NotNull(checkPayload);
        Assert.Equal("0000", checkPayload!.RepCode);

        var verifyResponse = await client.PostAsJsonAsync("/captcha/verify", new
        {
            captchaType = CaptchaTypes.ClickWord,
            captchaVerification = CaptchaCrypto.Encrypt($"{token}---{pointJson}", secretKey)
        });
        var verifyPayload = await verifyResponse.Content.ReadFromJsonAsync<CaptchaResponseEnvelope>(JsonOptions);
        Assert.NotNull(verifyPayload);
        Assert.Equal("0000", verifyPayload!.RepCode);
    }

    [Fact]
    public async Task BlockPuzzleCheck_Returns6111_ForWrongCoordinates()
    {
        using var client = _factory.CreateClient();

        var challenge = await GetBlockPuzzleChallengeAsync(client);
        var pointJson = CaptchaJson.Serialize(new CaptchaPoint
        {
            X = challenge.Point.X + 20,
            Y = challenge.Point.Y
        });

        var checkResponse = await client.PostAsJsonAsync("/captcha/check", new
        {
            captchaType = CaptchaTypes.BlockPuzzle,
            token = challenge.Token,
            pointJson = CaptchaCrypto.Encrypt(pointJson, challenge.SecretKey),
            clientUid = "slider"
        });
        var checkPayload = await checkResponse.Content.ReadFromJsonAsync<CaptchaResponseEnvelope>(JsonOptions);

        Assert.NotNull(checkPayload);
        Assert.Equal(CaptchaCode.ApiCaptchaCoordinateError.ToCode(), checkPayload!.RepCode);
        Assert.False(checkPayload.Success);
        Assert.True(checkPayload.Error);
    }

    [Fact]
    public async Task VerifyEndpoint_RejectsReplay()
    {
        using var client = _factory.CreateClient();

        var challenge = await GetBlockPuzzleChallengeAsync(client);
        var pointJson = CaptchaJson.Serialize(new CaptchaPoint { X = challenge.Point.X, Y = challenge.Point.Y });

        var checkResponse = await client.PostAsJsonAsync("/captcha/check", new
        {
            captchaType = CaptchaTypes.BlockPuzzle,
            token = challenge.Token,
            pointJson = CaptchaCrypto.Encrypt(pointJson, challenge.SecretKey),
            clientUid = "slider"
        });
        var checkPayload = await checkResponse.Content.ReadFromJsonAsync<CaptchaResponseEnvelope>(JsonOptions);
        Assert.NotNull(checkPayload);
        Assert.Equal("0000", checkPayload!.RepCode);

        var verification = CaptchaCrypto.Encrypt($"{challenge.Token}---{pointJson}", challenge.SecretKey);
        var firstVerifyResponse = await client.PostAsJsonAsync("/captcha/verify", new
        {
            captchaType = CaptchaTypes.BlockPuzzle,
            captchaVerification = verification
        });
        var secondVerifyResponse = await client.PostAsJsonAsync("/captcha/verify", new
        {
            captchaType = CaptchaTypes.BlockPuzzle,
            captchaVerification = verification
        });

        var firstVerifyPayload = await firstVerifyResponse.Content.ReadFromJsonAsync<CaptchaResponseEnvelope>(JsonOptions);
        var secondVerifyPayload = await secondVerifyResponse.Content.ReadFromJsonAsync<CaptchaResponseEnvelope>(JsonOptions);

        Assert.NotNull(firstVerifyPayload);
        Assert.NotNull(secondVerifyPayload);
        Assert.Equal("0000", firstVerifyPayload!.RepCode);
        Assert.Equal(CaptchaCode.ApiCaptchaInvalid.ToCode(), secondVerifyPayload!.RepCode);
    }

    private async Task<BlockPuzzleChallenge> GetBlockPuzzleChallengeAsync(HttpClient client)
    {
        var getResponse = await client.PostAsJsonAsync("/captcha/get", new
        {
            captchaType = CaptchaTypes.BlockPuzzle,
            clientUid = "slider",
            ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });

        var getPayload = await getResponse.Content.ReadFromJsonAsync<CaptchaResponseEnvelope>(JsonOptions);
        Assert.NotNull(getPayload);
        Assert.Equal("0000", getPayload!.RepCode);

        var token = getPayload.RepData.GetProperty("token").GetString();
        var secretKey = getPayload.RepData.GetProperty("secretKey").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        var cache = _factory.Services.GetRequiredService<ICaptchaCacheStore>();
        var cachedJson = await cache.GetAsync(CaptchaCacheKeys.Captcha(token!));
        var point = CaptchaJson.Deserialize<CaptchaPoint>(cachedJson!);

        return new BlockPuzzleChallenge(token!, secretKey!, point!);
    }

    private sealed record BlockPuzzleChallenge(string Token, string SecretKey, CaptchaPoint Point);

    public sealed class CaptchaResponseEnvelope
    {
        public string RepCode { get; set; } = string.Empty;
        public string? RepMsg { get; set; }
        public JsonElement RepData { get; set; }
        public bool Success { get; set; }
        public bool Error { get; set; }
    }
}
