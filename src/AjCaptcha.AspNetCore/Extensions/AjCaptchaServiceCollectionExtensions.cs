using AjCaptcha.AspNetCore.Controllers;
using AjCaptcha.Core.Abstractions;
using AjCaptcha.Core.Infrastructure;
using AjCaptcha.Core.Models;
using AjCaptcha.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AjCaptcha.AspNetCore.Extensions;

public static class AjCaptchaServiceCollectionExtensions
{
    public static IServiceCollection AddAjCaptcha(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AjCaptchaOptions>(configuration);
        return services.AddAjCaptchaCore();
    }

    public static IServiceCollection AddAjCaptcha(this IServiceCollection services, Action<AjCaptchaOptions> configure)
    {
        services.Configure(configure);
        return services.AddAjCaptchaCore();
    }

    private static IServiceCollection AddAjCaptchaCore(this IServiceCollection services)
    {
        services.AddOptions<AjCaptchaOptions>();
        services.TryAddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AjCaptchaOptions>>().Value);
        services.TryAddSingleton<CaptchaMessageProvider>();
        services.TryAddSingleton<ICaptchaImageProvider, CaptchaImageProvider>();
        services.TryAddSingleton<ICaptchaFontProvider, CaptchaFontProvider>();
        services.TryAddSingleton<ICaptchaCacheStore>(sp =>
        {
            var options = sp.GetRequiredService<AjCaptchaOptions>();
            return options.CacheType == CaptchaCacheType.Redis
                ? new RedisCaptchaCacheStore(options)
                : new MemoryCaptchaCacheStore(options, sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<MemoryCaptchaCacheStore>>());
        });
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ICaptchaChallengeHandler, BlockPuzzleCaptchaHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ICaptchaChallengeHandler, ClickWordCaptchaHandler>());
        services.TryAddSingleton<ICaptchaService, AjCaptchaService>();
        services.AddControllers().AddApplicationPart(typeof(CaptchaController).Assembly);
        return services;
    }
}
