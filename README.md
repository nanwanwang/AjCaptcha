# AJ-Captcha .NET

这个仓库是 `AJ-Captcha` 的 .NET 8 实现，包含：

- `AjCaptcha.Core`
- `AjCaptcha.AspNetCore`
- 示例服务 `AjCaptcha.Sample`
- NuGet 引用演示 `AjCaptcha.NugetWebDemo`
- 与原项目兼容的接口：
  - `/captcha/get`
  - `/captcha/check`
  - `/captcha/verify`

当前已支持：

- `blockPuzzle`
- `clickWord`
- 内存缓存
- Redis 缓存
- AES 开关
- 一次性 token
- 二次校验

## 前端示例

仓库已包含与当前 .NET 接口兼容的前端示例，目录在 `view`：

- `view/vue`
- `view/vue3`
- `view/html`
- `view/doc`

默认本地联调地址为 `http://localhost:8080`，启动 `AjCaptcha.Sample` 后通常只需要直接运行前端示例或按需调整地址即可。

## NuGet

当前包名：

- `Nanwanwang.AjCaptcha.Core`
- `Nanwanwang.AjCaptcha.AspNetCore`

## 快速接入

在 ASP.NET Core 项目里注册：

```csharp
builder.Services.AddAjCaptcha(builder.Configuration.GetSection("AjCaptcha"));
```

最小配置：

```json
{
  "AjCaptcha": {
    "Type": "default",
    "CacheType": "Memory",
    "AesStatus": true,
    "InterferenceOptions": 2
  }
}
```

## 直接运行示例

```bash
dotnet run --project samples/AjCaptcha.Sample/AjCaptcha.Sample.csproj
```

开发环境默认地址：

- `http://localhost:8080`
- `https://localhost:7080`

示例服务已经打开开发用跨域，前端把后端地址切到这个服务即可联调。

## 打包

```bash
dotnet pack src/AjCaptcha.Core/AjCaptcha.Core.csproj -o artifacts
dotnet pack src/AjCaptcha.AspNetCore/AjCaptcha.AspNetCore.csproj -o artifacts
```
