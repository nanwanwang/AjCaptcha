using System.Text.Json.Serialization;

namespace AjCaptcha.Core.Models;

public enum CaptchaCacheType
{
    Memory,
    Redis
}

public static class CaptchaTypes
{
    public const string Default = "default";
    public const string BlockPuzzle = "blockPuzzle";
    public const string ClickWord = "clickWord";
}

public sealed class AjCaptchaOptions
{
    public const string SectionName = "AjCaptcha";

    public string Type { get; set; } = CaptchaTypes.Default;
    public string Jigsaw { get; set; } = string.Empty;
    public string PicClick { get; set; } = string.Empty;
    public bool WaterMarkEnabled { get; set; } = true;
    public string WaterMark { get; set; } = "AJ-Captcha";
    public string WaterFont { get; set; } = "WenQuanZhengHei.ttf";
    public string WaterMarkColor { get; set; } = "#FFFFFF";
    public double WaterMarkOpacity { get; set; } = 0.65d;
    public float WaterMarkFontSize { get; set; }
    public float WaterMarkOffsetX { get; set; } = 8f;
    public float WaterMarkOffsetY { get; set; } = 8f;
    public string FontType { get; set; } = "WenQuanZhengHei.ttf";
    public int SlipOffset { get; set; } = 5;
    public bool AesStatus { get; set; } = true;
    public int InterferenceOptions { get; set; } = 2;
    public int CacheNumber { get; set; } = 1000;
    public int TimingClear { get; set; } = 180;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CaptchaCacheType CacheType { get; set; } = CaptchaCacheType.Memory;
    public bool HistoryDataClearEnable { get; set; }
    public bool ReqFrequencyLimitEnable { get; set; }
    public int ReqGetLockLimit { get; set; } = 5;
    public int ReqGetLockSeconds { get; set; } = 360;
    public int ReqGetMinuteLimit { get; set; } = 30;
    public int ReqCheckMinuteLimit { get; set; } = 30;
    public int ReqVerifyMinuteLimit { get; set; } = 30;
    public int FontStyle { get; set; } = 1;
    public int FontSize { get; set; } = 25;
    public int ClickWordCount { get; set; } = 4;
    public bool I18nEnabled { get; set; } = true;
    public string DefaultCulture { get; set; } = "zh-CN";
    public string EndpointPrefix { get; set; } = "/captcha";
    public string? RedisConnectionString { get; set; }
    public string? RedisInstanceName { get; set; }
    public int CaptchaExpireSeconds { get; set; } = 120;
    public int VerifyExpireSeconds { get; set; } = 180;

    public TimeSpan CaptchaExpire => TimeSpan.FromSeconds(CaptchaExpireSeconds);
    public TimeSpan VerifyExpire => TimeSpan.FromSeconds(VerifyExpireSeconds);
}
