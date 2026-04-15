# AjCaptcha.Sample

示例服务用于直接联调现有前端组件。

启动：

```bash
dotnet run --project dotnet/samples/AjCaptcha.Sample/AjCaptcha.Sample.csproj
```

主要接口：

- `POST /captcha/get`
- `POST /captcha/check`
- `POST /captcha/verify`
- `POST /login`

`/login` 只是演示二次校验怎么接，不代表真实业务登录逻辑。
