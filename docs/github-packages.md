# GitHub Packages 发布说明

## 自动发布

仓库已内置工作流：

- `.github/workflows/publish-github-packages.yml`

这套工作流会在两种情况下发布：

- 你推送代码到 `main`
- 你在 GitHub 的 `Actions` 页面手动点 `Run workflow`

发布前需要先修改这两个文件里的版本号，避免重复版本被拒绝：

- `src/AjCaptcha.Core/AjCaptcha.Core.csproj`
- `src/AjCaptcha.AspNetCore/AjCaptcha.AspNetCore.csproj`

工作流会直接使用仓库自带的 `GITHUB_TOKEN` 发布到：

```text
https://nuget.pkg.github.com/nanwanwang/index.json
```

同一个版本号如果已经发过，GitHub Packages 不会覆盖。当前工作流已经加了重复版本跳过，所以再次推送 `main` 时不会因为旧版本重复而整条工作流失败。

## 手动发布

如果你想在自己电脑上手动发布，需要先创建一个 GitHub `Personal access token (classic)`，至少带：

- `write:packages`
- `read:packages`

然后执行：

```bash
dotnet nuget add source --username nanwanwang --password YOUR_GITHUB_PAT --store-password-in-clear-text --name github "https://nuget.pkg.github.com/nanwanwang/index.json"

dotnet nuget push artifacts/AjCaptcha.Core.0.1.4.nupkg --source github --api-key YOUR_GITHUB_PAT
dotnet nuget push artifacts/AjCaptcha.AspNetCore.0.1.4.nupkg --source github --api-key YOUR_GITHUB_PAT
```

## 发布后查看

发布成功后，可以在这里看到包：

- `https://github.com/nanwanwang?tab=packages`
- 仓库右侧 `Packages`

如果个人主页里已经看到包，但仓库右侧还没显示，进入包页面后手动连接仓库 `nanwanwang/AjCaptcha` 即可。
