using System.Text.Json.Serialization;
using AjCaptcha.Core.Infrastructure;

namespace AjCaptcha.Core.Models;

public class CaptchaClientRequest
{
    public string? ClientUid { get; set; }
    public long? Ts { get; set; }
    public string? BrowserInfo { get; set; }
}

public sealed class CaptchaRequest : CaptchaClientRequest
{
    public string? CaptchaType { get; set; }
}

public sealed class CaptchaCheckRequest : CaptchaClientRequest
{
    public string? CaptchaType { get; set; }
    public string? PointJson { get; set; }
    public string? Token { get; set; }
}

public sealed class CaptchaVerifyRequest : CaptchaClientRequest
{
    public string? CaptchaType { get; set; }
    public string? CaptchaVerification { get; set; }
}

public sealed class CaptchaPoint
{
    public string? SecretKey { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class CaptchaPayload
{
    public string? CaptchaType { get; set; }
    public string? OriginalImageBase64 { get; set; }
    public CaptchaPoint? Point { get; set; }
    public string? JigsawImageBase64 { get; set; }
    public IReadOnlyList<string>? WordList { get; set; }
    public IReadOnlyList<CaptchaPoint>? PointList { get; set; }
    public string? PointJson { get; set; }
    public string? Token { get; set; }
    public bool Result { get; set; }
    public string? CaptchaVerification { get; set; }
    public string? SecretKey { get; set; }
    public bool OpAdmin { get; set; }
}

public sealed class CaptchaResponse
{
    public string RepCode { get; init; } = CaptchaCode.Success.ToCode();
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RepMsg { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? RepData { get; init; }
    public bool Success => RepCode == CaptchaCode.Success.ToCode();
    public bool Error => !Success;

    public static CaptchaResponse SuccessData(object data) => new() { RepData = data };

    public static CaptchaResponse SuccessMessage(string message) => new()
    {
        RepCode = CaptchaCode.Success.ToCode(),
        RepMsg = message
    };

    public static CaptchaResponse Failure(CaptchaCode code, string message) => new()
    {
        RepCode = code.ToCode(),
        RepMsg = message
    };
}
