# 前端示例

这个目录保留了与当前 .NET 后端协议兼容的前端示例，默认按以下接口对接：

- `POST /captcha/get`
- `POST /captcha/check`
- `POST /captcha/verify`

默认本地联调地址：

- `http://localhost:8080`

推荐直接使用这些示例：

- `view/vue`
- `view/vue3`
- `view/html`
- `view/doc`

说明：

- `vue`、`vue3`、`html` 已按本仓库的 .NET 示例服务默认地址整理
- 启动 [AjCaptcha.Sample](/D:/code/AjCaptcha/samples/AjCaptcha.Sample/AjCaptcha.Sample.csproj) 后，前端通常只需要改后端地址或直接使用默认值即可联调
- `view/doc` 保留了原项目的接入文档，可作为多端协议参考
