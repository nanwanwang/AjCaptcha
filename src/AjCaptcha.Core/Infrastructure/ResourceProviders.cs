using System.Reflection;
using System.Security.Cryptography;
using AjCaptcha.Core.Abstractions;
using AjCaptcha.Core.Models;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace AjCaptcha.Core.Infrastructure;

public sealed class CaptchaImageProvider : ICaptchaImageProvider
{
    private static readonly Assembly Assembly = typeof(CaptchaImageProvider).Assembly;
    private readonly IReadOnlyList<byte[]> _jigsawOriginals;
    private readonly IReadOnlyList<byte[]> _jigsawTemplates;
    private readonly IReadOnlyList<byte[]> _picClickBackgrounds;
    private readonly ILogger<CaptchaImageProvider> _logger;

    public CaptchaImageProvider(AjCaptchaOptions options, ILogger<CaptchaImageProvider> logger)
    {
        _logger = logger;
        _jigsawOriginals = LoadImageBytes(string.IsNullOrWhiteSpace(options.Jigsaw) ? string.Empty : Path.Combine(options.Jigsaw, "original"), "AjCaptcha.Core.Resources.defaultImages.jigsaw.original.");
        _jigsawTemplates = LoadImageBytes(string.IsNullOrWhiteSpace(options.Jigsaw) ? string.Empty : Path.Combine(options.Jigsaw, "slidingBlock"), "AjCaptcha.Core.Resources.defaultImages.jigsaw.slidingBlock.");
        _picClickBackgrounds = LoadImageBytes(options.PicClick, "AjCaptcha.Core.Resources.defaultImages.pic_click.");
    }

    public SKBitmap? GetRandomJigsawOriginal() => GetRandomBitmap(_jigsawOriginals);
    public SKBitmap? GetRandomJigsawTemplate() => GetRandomBitmap(_jigsawTemplates);
    public SKBitmap? GetRandomPicClickBackground() => GetRandomBitmap(_picClickBackgrounds);

    private IReadOnlyList<byte[]> LoadImageBytes(string path, string embeddedPrefix)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            var directory = ResolveDirectory(path);
            if (Directory.Exists(directory))
            {
                var files = Directory.GetFiles(directory, "*.png", SearchOption.TopDirectoryOnly)
                    .OrderBy(static x => x, StringComparer.OrdinalIgnoreCase)
                    .Select(File.ReadAllBytes)
                    .ToList();
                if (files.Count > 0)
                {
                    return files;
                }
            }

            _logger.LogWarning("Captcha asset path {Path} was not found or empty. Falling back to embedded defaults.", directory);
        }

        return Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(embeddedPrefix, StringComparison.OrdinalIgnoreCase) && name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static x => x, StringComparer.OrdinalIgnoreCase)
            .Select(ReadEmbeddedBytes)
            .ToList();
    }

    private static string ResolveDirectory(string path)
    {
        if (path.StartsWith("classpath:", StringComparison.OrdinalIgnoreCase))
        {
            var relative = path["classpath:".Length..].TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(AppContext.BaseDirectory, relative);
        }

        return Path.GetFullPath(path);
    }

    private static byte[] ReadEmbeddedBytes(string resourceName)
    {
        using var stream = Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource {resourceName} was not found.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static SKBitmap? GetRandomBitmap(IReadOnlyList<byte[]> source)
    {
        if (source.Count == 0)
        {
            return null;
        }

        var bytes = source[RandomNumberGenerator.GetInt32(source.Count)];
        return SKBitmap.Decode(bytes);
    }
}

public sealed class CaptchaFontProvider : ICaptchaFontProvider
{
    private static readonly Assembly Assembly = typeof(CaptchaFontProvider).Assembly;
    private readonly Dictionary<string, SKTypeface> _cache = new(StringComparer.OrdinalIgnoreCase);

    public SKTypeface GetWatermarkTypeface(string? fontNameOrPath) => Resolve(fontNameOrPath);
    public SKTypeface GetTextTypeface(string? fontNameOrPath) => Resolve(fontNameOrPath);

    private SKTypeface Resolve(string? fontNameOrPath)
    {
        var key = string.IsNullOrWhiteSpace(fontNameOrPath) ? "WenQuanZhengHei.ttf" : fontNameOrPath;
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (LooksLikeFontFile(key))
        {
            var fromFile = LoadFontFromFile(key);
            if (fromFile is not null)
            {
                _cache[key] = fromFile;
                return fromFile;
            }

            var fromResource = LoadFontFromResource(Path.GetFileName(key));
            if (fromResource is not null)
            {
                _cache[key] = fromResource;
                return fromResource;
            }
        }

        var family = SKTypeface.FromFamilyName(key) ?? SKTypeface.Default;
        _cache[key] = family;
        return family;
    }

    private static bool LooksLikeFontFile(string value)
        => value.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
           || value.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase)
           || value.EndsWith(".otf", StringComparison.OrdinalIgnoreCase);

    private static SKTypeface? LoadFontFromFile(string value)
    {
        var fullPath = Path.IsPathRooted(value) ? value : Path.Combine(AppContext.BaseDirectory, value);
        return File.Exists(fullPath) ? SKTypeface.FromFile(fullPath) : null;
    }

    private static SKTypeface? LoadFontFromResource(string fileName)
    {
        var resourceName = Assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
        {
            return null;
        }

        using var stream = Assembly.GetManifestResourceStream(resourceName);
        return stream is null ? null : SKTypeface.FromStream(stream);
    }
}
