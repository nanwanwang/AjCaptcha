# AJ-Captcha .NET

这是 `AJ-Captcha` 的 .NET 8 实现，包含：

- `AjCaptcha.Core`
- `AjCaptcha.AspNetCore`
- 示例服务 `AjCaptcha.Sample`
- NuGet 接入演示 `AjCaptcha.NugetWebDemo`

兼容接口：

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

默认本地联调地址是 `http://localhost:8080`。启动 `AjCaptcha.Sample` 后，前端示例通常只需要把后端地址指向这个服务即可。

## NuGet 包名

- `AjCaptcha.Core`
- `AjCaptcha.AspNetCore`

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

## 运行示例

```bash
dotnet run --project samples/AjCaptcha.Sample/AjCaptcha.Sample.csproj
```

开发环境默认地址：

- `http://localhost:8080`
- `https://localhost:7080`

## 本地打包

```bash
dotnet pack src/AjCaptcha.Core/AjCaptcha.Core.csproj -c Release -o artifacts
dotnet pack src/AjCaptcha.AspNetCore/AjCaptcha.AspNetCore.csproj -c Release -o artifacts
```

## 发布说明

GitHub Packages 的自动发布与手动发布说明已移到单独文档：

- `docs/github-packages.md`
