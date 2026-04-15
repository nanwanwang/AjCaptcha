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

## 发布到 GitHub Packages

仓库已内置工作流：

- `.github/workflows/publish-github-packages.yml`

这套工作流会在两种情况下发布：

- 你在 GitHub 的 `Actions` 页面手动点 `Run workflow`
- 你推送形如 `v0.1.3` 的 tag

发布前需要先修改这两个文件里的版本号，避免重复版本被拒绝：

- `src/AjCaptcha.Core/AjCaptcha.Core.csproj`
- `src/AjCaptcha.AspNetCore/AjCaptcha.AspNetCore.csproj`

GitHub Actions 发布时不需要你自己再配 PAT。工作流会直接使用仓库自带的 `GITHUB_TOKEN` 发布到：

```text
https://nuget.pkg.github.com/nanwanwang/index.json
```

如果你想在自己电脑上手动发布，需要先创建一个 GitHub `Personal access token (classic)`，至少带：

- `write:packages`
- `read:packages`

然后执行：

```bash
dotnet nuget add source --username nanwanwang --password YOUR_GITHUB_PAT --store-password-in-clear-text --name github "https://nuget.pkg.github.com/nanwanwang/index.json"

dotnet nuget push artifacts/Nanwanwang.AjCaptcha.Core.0.1.3.nupkg --source github --api-key YOUR_GITHUB_PAT
dotnet nuget push artifacts/Nanwanwang.AjCaptcha.AspNetCore.0.1.3.nupkg --source github --api-key YOUR_GITHUB_PAT
```

发布成功后，可以在这里看到包：

- `https://github.com/nanwanwang?tab=packages`
- 仓库右侧 `Packages`

如果个人主页里已经看到包，但仓库右侧还没显示，进入包页面后手动连接仓库 `nanwanwang/AjCaptcha` 即可。
