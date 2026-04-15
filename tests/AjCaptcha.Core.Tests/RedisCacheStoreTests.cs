using AjCaptcha.Core.Infrastructure;
using AjCaptcha.Core.Models;

namespace AjCaptcha.Core.Tests;

public class RedisCacheStoreTests
{
    [Fact]
    [Trait("Category", "Redis")]
    public async Task RedisStore_SupportsBasicOperations()
    {
        var connection = Environment.GetEnvironmentVariable("AJCAPTCHA_REDIS_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
        {
            return;
        }

        var options = new AjCaptchaOptions
        {
            CacheType = CaptchaCacheType.Redis,
            RedisConnectionString = connection,
            RedisInstanceName = $"AjCaptcha:Tests:{Guid.NewGuid():N}:"
        };

        await using var store = new RedisCaptchaCacheStore(options);
        const string key = "captcha:test";

        await store.SetAsync(key, "1", TimeSpan.FromSeconds(10));
        Assert.True(await store.ExistsAsync(key));
        Assert.Equal("1", await store.GetAsync(key));

        var incremented = await store.IncrementAsync(key, 2);
        Assert.Equal(3, incremented);
        Assert.Equal("3", await store.GetAsync(key));

        await store.SetExpireAsync(key, TimeSpan.FromMilliseconds(300));
        await Task.Delay(700);

        Assert.Null(await store.GetAsync(key));
    }
}
