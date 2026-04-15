# vue3-aj-captcha

## 说明

这个示例保留了与 AJ-Captcha 协议兼容的 Vue 3 组件，可直接对接本仓库的 .NET 后端。

默认本地联调地址：

- `http://localhost:8080`

如果需要切换地址，可设置：

- `VUE_APP_BASE_API`

## 使用

将 `src/components/verifition` 复制到你的项目中，并安装依赖：

```bash
yarn add axios crypto-js
```

参考入口可看 `App.vue`。

## 本地运行

```bash
yarn install
yarn serve
```
