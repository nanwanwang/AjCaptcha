using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AjCaptcha.Core.Models;

namespace AjCaptcha.Core.Infrastructure;

public enum CaptchaCode
{
    Success,
    Error,
    Exception,
    BlankError,
    NullError,
    NotNullError,
    NotExistError,
    ExistError,
    ParamTypeError,
    ParamFormatError,
    ApiCaptchaInvalid,
    ApiCaptchaCoordinateError,
    ApiCaptchaError,
    ApiCaptchaBaseMapNull,
    ApiReqLimitGetError,
    ApiReqLockGetError,
    ApiReqLimitCheckError,
    ApiReqLimitVerifyError,
    ApiReqInvalid
}

public static class CaptchaCodeExtensions
{
    public static string ToCode(this CaptchaCode code) => code switch
    {
        CaptchaCode.Success => "0000",
        CaptchaCode.Error => "0001",
        CaptchaCode.Exception => "9999",
        CaptchaCode.BlankError => "0011",
        CaptchaCode.NullError => "0011",
        CaptchaCode.NotNullError => "0012",
        CaptchaCode.NotExistError => "0013",
        CaptchaCode.ExistError => "0014",
        CaptchaCode.ParamTypeError => "0015",
        CaptchaCode.ParamFormatError => "0016",
        CaptchaCode.ApiCaptchaInvalid => "6110",
        CaptchaCode.ApiCaptchaCoordinateError => "6111",
        CaptchaCode.ApiCaptchaError => "6112",
        CaptchaCode.ApiCaptchaBaseMapNull => "6113",
        CaptchaCode.ApiReqLimitGetError => "6201",
        CaptchaCode.ApiReqLockGetError => "6202",
        CaptchaCode.ApiReqLimitCheckError => "6204",
        CaptchaCode.ApiReqLimitVerifyError => "6205",
        CaptchaCode.ApiReqInvalid => "6206",
        _ => "0001"
    };
}

public static class CaptchaCacheKeys
{
    public const string CaptchaKeyPattern = "RUNNING:CAPTCHA:{0}";
    public const string SecondCaptchaKeyPattern = "RUNNING:CAPTCHA:second-{0}";
    public const string RateLimitKeyPattern = "AJ.CAPTCHA.REQ.LIMIT-{0}-{1}";

    public static string Captcha(string token) => string.Format(CaptchaKeyPattern, token);
    public static string Verification(string verification) => string.Format(SecondCaptchaKeyPattern, verification);
    public static string RateLimit(string type, string clientUid) => string.Format(RateLimitKeyPattern, type, clientUid);
}

public sealed class CaptchaMessageProvider
{
    private static readonly IReadOnlyDictionary<CaptchaCode, string> ZhMessages = new Dictionary<CaptchaCode, string>
    {
        [CaptchaCode.Success] = "成功",
        [CaptchaCode.Error] = "操作失败",
        [CaptchaCode.Exception] = "服务器内部异常",
        [CaptchaCode.BlankError] = "{0}不能为空",
        [CaptchaCode.NullError] = "{0}不能为空",
        [CaptchaCode.NotNullError] = "{0}必须为空",
        [CaptchaCode.NotExistError] = "{0}数据库中不存在",
        [CaptchaCode.ExistError] = "{0}数据库中已存在",
        [CaptchaCode.ParamTypeError] = "{0}类型错误",
        [CaptchaCode.ParamFormatError] = "{0}格式错误",
        [CaptchaCode.ApiCaptchaInvalid] = "验证码已失效，请重新获取",
        [CaptchaCode.ApiCaptchaCoordinateError] = "验证失败",
        [CaptchaCode.ApiCaptchaError] = "获取验证码失败，请联系管理员",
        [CaptchaCode.ApiCaptchaBaseMapNull] = "底图未初始化成功，请检查路径",
        [CaptchaCode.ApiReqLimitGetError] = "get接口请求次数超限，请稍后再试!",
        [CaptchaCode.ApiReqLockGetError] = "接口验证失败数过多，请稍后再试!",
        [CaptchaCode.ApiReqLimitCheckError] = "check接口请求次数超限，请稍后再试!",
        [CaptchaCode.ApiReqLimitVerifyError] = "verify请求次数超限!",
        [CaptchaCode.ApiReqInvalid] = "无效请求，请重新获取验证码"
    };

    private static readonly IReadOnlyDictionary<CaptchaCode, string> EnMessages = new Dictionary<CaptchaCode, string>
    {
        [CaptchaCode.Success] = "Success",
        [CaptchaCode.Error] = "Operation failed",
        [CaptchaCode.Exception] = "Internal server error",
        [CaptchaCode.BlankError] = "{0} cannot be empty",
        [CaptchaCode.NullError] = "{0} cannot be empty",
        [CaptchaCode.NotNullError] = "{0} must be empty",
        [CaptchaCode.NotExistError] = "{0} does not exist",
        [CaptchaCode.ExistError] = "{0} already exists",
        [CaptchaCode.ParamTypeError] = "{0} has an invalid type",
        [CaptchaCode.ParamFormatError] = "{0} has an invalid format",
        [CaptchaCode.ApiCaptchaInvalid] = "Captcha is invalid or expired. Please fetch a new one.",
        [CaptchaCode.ApiCaptchaCoordinateError] = "Captcha validation failed",
        [CaptchaCode.ApiCaptchaError] = "Failed to create captcha. Please contact the administrator.",
        [CaptchaCode.ApiCaptchaBaseMapNull] = "Captcha assets were not initialized correctly",
        [CaptchaCode.ApiReqLimitGetError] = "Too many get requests. Please try again later.",
        [CaptchaCode.ApiReqLockGetError] = "Too many failed attempts. Please try again later.",
        [CaptchaCode.ApiReqLimitCheckError] = "Too many check requests. Please try again later.",
        [CaptchaCode.ApiReqLimitVerifyError] = "Too many verify requests. Please try again later.",
        [CaptchaCode.ApiReqInvalid] = "Invalid request. Please fetch a new captcha."
    };

    private readonly AjCaptchaOptions _options;

    public CaptchaMessageProvider(AjCaptchaOptions options)
    {
        _options = options;
    }

    public string Get(CaptchaCode code, params object[] args)
    {
        var useEnglish = ResolveLanguage().Equals("en", StringComparison.OrdinalIgnoreCase);
        var source = useEnglish ? EnMessages : ZhMessages;
        var template = source.TryGetValue(code, out var message) ? message : source[CaptchaCode.Error];
        return args.Length == 0 ? template : string.Format(CultureInfo.InvariantCulture, template, args);
    }

    private string ResolveLanguage()
    {
        if (!_options.I18nEnabled)
        {
            return _options.DefaultCulture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh";
        }

        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase)
            ? "en"
            : "zh";
    }
}

public static class CaptchaJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
}

public static class CaptchaCrypto
{
    private const string Chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public static string GenerateToken() => Guid.NewGuid().ToString("N");

    public static string GenerateKey(int length = 16)
    {
        Span<char> buffer = stackalloc char[length];
        for (var index = 0; index < buffer.Length; index++)
        {
            buffer[index] = Chars[RandomNumberGenerator.GetInt32(Chars.Length)];
        }

        return new string(buffer);
    }

    public static string Encrypt(string content, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return content;
        }

        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = Encoding.UTF8.GetBytes(key);
        using var encryptor = aes.CreateEncryptor();
        var bytes = Encoding.UTF8.GetBytes(content);
        var encrypted = encryptor.TransformFinalBlock(bytes, 0, bytes.Length);
        return Convert.ToBase64String(encrypted);
    }

    public static string Decrypt(string content, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return content;
        }

        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = Encoding.UTF8.GetBytes(key);
        using var decryptor = aes.CreateDecryptor();
        var bytes = Convert.FromBase64String(content);
        var decrypted = decryptor.TransformFinalBlock(bytes, 0, bytes.Length);
        return Encoding.UTF8.GetString(decrypted);
    }

    public static string Md5(string value)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(value));
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var item in hash)
        {
            builder.Append(item.ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}

public static class CaptchaCorpus
{
    public const string HanZi = "的一了是我不在人们有来他这上着个地到大里说就去子得也和那要下看天时过出小么起你都把好还多没为又可家学只以主会样年想生同老中十从自面前头道它后然走很像见两用她国动进成回什边作对开而己些现山民候经发工向事命给长水几义三声于高手知理眼志点心战二问但身方实吃做叫当住听革打呢真全才四已所敌之最光产情路分总条白话东席次亲如被花口放儿常气五第使写军吧文运再果怎定许快明行因别飞外树物活部门无往船望新带队先力完却站代员机更九您每风级跟笑啊孩万少直意夜比阶连车重便斗马哪化太指变社似士者干石满日决百原拿群究各六本思解立河村八难早论吗根共让相研今其书坐接应关信觉步反处记将千找争领或师结块跑谁草越字加脚紧爱等习阵怕月青半火法题建赶位唱海七女任件感准张团屋离色脸片科倒睛利世刚且由送切星导晚表够整认响雪流未场该并底深刻平伟忙提确近亮轻讲农古黑告界拉名呀土清阳照办史改历转画造嘴此治北必服雨穿内识验传业菜爬睡兴形量咱观苦体众通冲合破友度术饭公旁房极南枪读沙岁线野坚空收算至政城劳落钱特围弟胜教热展包歌类渐强数乡呼性音答哥际旧神座章帮啦受系令跳非何牛取入岸敢掉忽种装顶急林停息句区衣般报叶压慢叔背细";
}

public static class CaptchaClientIdentity
{
    public static string? ResolveClientUid(CaptchaClientRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.BrowserInfo))
        {
            return CaptchaCrypto.Md5(request.BrowserInfo);
        }

        return string.IsNullOrWhiteSpace(request.ClientUid) ? null : request.ClientUid;
    }
}
