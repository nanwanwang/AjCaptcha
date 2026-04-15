using System.Net.Http.Json;
using System.Text.Json;
using AjCaptcha.Core.Abstractions;
using AjCaptcha.Core.Infrastructure;
using AjCaptcha.Core.Models;
using AjCaptcha.Sample;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AjCaptcha.AspNetCore.Tests;

public class RedisCaptchaEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    [Trait("Category", "Redis")]
    public async Task BlockPuzzleEndpoints_WorkEndToEnd_WithRedisCache()
    {
        var connection = Environment.GetEnvironmentVariable("AJCAPTCHA_REDIS_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
        {
            return;
        }

        using var factory = CreateFactory(connection);
        using var client = factory.CreateClient();

        var getResponse = await client.PostAsJsonAsync("/captcha/get", new
        {
            captchaType = CaptchaTypes.BlockPuzzle,
            clientUid = "redis-slider",
            ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });

        var getPayload = await getResponse.Content.ReadFromJsonAsync<CaptchaResponseEnvelope>(JsonOptions);
        Assert.NotNull(getPayload);
        Assert.Equal("0000", getPayload!.RepCode);

        var token = getPayload.RepData.GetProperty("token").GetString();
        var secretKey = getPayload.RepData.GetProperty("secretKey").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.False(string.IsNullOrWhiteSpace(secretKey));

        var cache = factory.Services.GetRequiredService<ICaptchaCacheStore>();
        var cachedJson = await cache.GetAsync(CaptchaCacheKeys.Captcha(token!));
        var point = CaptchaJson.Deserialize<CaptchaPoint>(cachedJson!);
        var pointJson = CaptchaJson.Serialize(new CaptchaPoint { X = point!.X, Y = point.Y });
        var encrypted = CaptchaCrypto.Encrypt(pointJson, secretKey);

        var checkResponse = await client.PostAsJsonAsync("/captcha/check", new
        {
            captchaType = CaptchaTypes.BlockPuzzle,
            token,
            pointJson = encrypted,
            clientUid = "redis-slider"
        });
        var checkPayload = await checkResponse.Content.ReadFromJsonAsync<CaptchaResponseEnvelope>(JsonOptions);
        Assert.NotNull(checkPayload);
        Assert.Equal("0000", checkPayload!.RepCode);

        var verifyResponse = await client.PostAsJsonAsync("/captcha/verify", new
        {
            captchaType = CaptchaTypes.BlockPuzzle,
            captchaVerification = CaptchaCrypto.Encrypt($"{token}---{pointJson}", secretKey)
        });
        var verifyPayload = await verifyResponse.Content.ReadFromJsonAsync<CaptchaResponseEnvelope>(JsonOptions);
        Assert.NotNull(verifyPayload);
        Assert.Equal("0000", verifyPayload!.RepCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(string connection)
    {
        var instanceName = $"AjCaptcha:Tests:{Guid.NewGuid():N}:";
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Redis");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AjCaptcha:CacheType"] = nameof(CaptchaCacheType.Redis),
                    ["AjCaptcha:RedisConnectionString"] = connection,
                    ["AjCaptcha:RedisInstanceName"] = instanceName
                });
            });
        });
    }

    public sealed class CaptchaResponseEnvelope
    {
        public string RepCode { get; set; } = string.Empty;
        public string? RepMsg { get; set; }
        public JsonElement RepData { get; set; }
        public bool Success { get; set; }
        public bool Error { get; set; }
    }
}
