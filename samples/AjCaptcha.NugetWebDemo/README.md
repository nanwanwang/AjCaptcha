# AjCaptcha.NugetWebDemo

这个项目用于验证两件事：

1. 业务项目通过 NuGet 引用 `Nanwanwang.AjCaptcha.AspNetCore` 后能否直接跑通
2. 前端页面是否能直接联通 `/captcha/get`、`/captcha/check`、`/captcha/verify`

默认启动：

```bash
dotnet run --project samples/AjCaptcha.NugetWebDemo/AjCaptcha.NugetWebDemo.csproj
```

默认地址：

- `http://localhost:8090`
- `https://localhost:7090`

如果要切到 Redis：

```bash
$env:ASPNETCORE_ENVIRONMENT='Redis'
dotnet run --project samples/AjCaptcha.NugetWebDemo/AjCaptcha.NugetWebDemo.csproj
```
