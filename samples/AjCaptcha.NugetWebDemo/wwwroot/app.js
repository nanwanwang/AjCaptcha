(function () {
  const state = {
    logLines: [],
    activeCaptchaType: "blockPuzzle"
  };

  const captchaModes = {
    blockPuzzle: {
      title: "滑块验证",
      description: "适合先确认接口和拖动校验流程是否正常。",
      buttonText: "开始滑块验证并登录",
      proxyButtonId: "slider-login-proxy"
    },
    clickWord: {
      title: "文字点选",
      description: "更接近图片点选场景，能一起验证文字绘制和坐标校验。",
      buttonText: "开始点选验证并登录",
      proxyButtonId: "point-login-proxy"
    }
  };

  const els = {
    username: document.getElementById("username"),
    password: document.getElementById("password"),
    loginResult: document.getElementById("login-result"),
    log: document.getElementById("event-log"),
    origin: document.getElementById("origin-pill"),
    cache: document.getElementById("cache-pill"),
    aes: document.getElementById("aes-pill"),
    serviceOrigin: document.getElementById("service-origin"),
    payloadTitle: document.getElementById("payload-title"),
    payloadView: document.getElementById("payload-view"),
    typeSwitch: document.getElementById("captcha-type-switch"),
    selectedModeTitle: document.getElementById("selected-mode-title"),
    selectedModeText: document.getElementById("selected-mode-text"),
    loginTrigger: document.getElementById("login-trigger"),
    clearLog: document.getElementById("clear-log")
  };

  function appendLog(title, payload) {
    const timestamp = new Date().toLocaleTimeString("zh-CN", { hour12: false });
    const lines = [`[${timestamp}] ${title}`];
    if (payload !== undefined) {
      lines.push(typeof payload === "string" ? payload : JSON.stringify(payload, null, 2));
    }

    state.logLines.unshift(lines.join("\n"));
    state.logLines = state.logLines.slice(0, 12);
    els.log.textContent = state.logLines.join("\n\n");
  }

  function setPayload(title, payload) {
    els.payloadTitle.textContent = title;
    els.payloadView.textContent = typeof payload === "string" ? payload : JSON.stringify(payload, null, 2);
  }

  function setResult(title, text, kind) {
    els.loginResult.classList.remove("success", "error");
    if (kind) {
      els.loginResult.classList.add(kind);
    }

    els.loginResult.querySelector(".result-title").textContent = title;
    els.loginResult.querySelector(".result-text").textContent = text;
  }

  function getCredentials() {
    return {
      username: els.username.value.trim(),
      password: els.password.value.trim()
    };
  }

  function ensureCredentials() {
    const credentials = getCredentials();
    if (!credentials.username || !credentials.password) {
      setResult("缺少信息", "请输入用户名和密码，再进行验证码验证。", "error");
      appendLog("表单校验未通过", credentials);
      setPayload("表单校验", credentials);
      return false;
    }

    return true;
  }

  async function loadStatus() {
    const response = await fetch("/api/demo/status");
    const data = await response.json();

    els.origin.textContent = `Origin: ${window.location.origin}`;
    els.cache.textContent = `Cache: ${data.cacheType}${data.redisConfigured ? " / Redis已配置" : ""}`;
    els.aes.textContent = `AES: ${data.aesStatus ? "开启" : "关闭"}`;
    els.serviceOrigin.textContent = window.location.origin;
    appendLog("状态已加载", data);
    setPayload("状态接口响应", data);
  }

  async function submitLogin(captchaType, captchaVerification) {
    const credentials = getCredentials();
    setResult("正在校验", "验证码已通过，正在请求模拟登录接口。");
    appendLog("提交模拟登录", { captchaType, username: credentials.username });
    setPayload("模拟登录请求", {
      captchaType: captchaType,
      username: credentials.username,
      captchaVerification: captchaVerification
    });

    const response = await fetch("/api/demo/login", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        username: credentials.username,
        password: credentials.password,
        captchaType: captchaType,
        captchaVerification: captchaVerification
      })
    });

    const payload = await response.json();
    appendLog("模拟登录响应", payload);
    setPayload("模拟登录响应", payload);

    if (payload.success) {
      setResult("模拟登录成功", `用户 ${credentials.username} 已完成 ${captchaType} 校验。`, "success");
      return;
    }

    setResult("模拟登录失败", payload.repMsg || "验证码二次校验没有通过。", "error");
  }

  function getWidgetSizes() {
    const viewport = window.innerWidth;
    const fixedSliderWidth = Math.min(Math.max(viewport - 88, 280), 460);
    const fixedPointWidth = Math.min(Math.max(viewport - 88, 280), 500);
    const popupWidth = Math.min(Math.max(viewport - 64, 280), 420);

    return {
      fixedSliderWidth: `${fixedSliderWidth}px`,
      fixedPointWidth: `${fixedPointWidth}px`,
      popupWidth: `${popupWidth}px`
    };
  }

  function renderActiveCaptchaType() {
    const current = captchaModes[state.activeCaptchaType];
    els.selectedModeTitle.textContent = current.title;
    els.selectedModeText.textContent = current.description;
    els.loginTrigger.textContent = current.buttonText;

    const buttons = els.typeSwitch.querySelectorAll("[data-captcha-type]");
    buttons.forEach(function (button) {
      button.classList.toggle("is-active", button.dataset.captchaType === state.activeCaptchaType);
    });
  }

  function bindTypeSwitch() {
    els.typeSwitch.addEventListener("click", function (event) {
      const target = event.target.closest("[data-captcha-type]");
      if (!target) {
        return;
      }

      state.activeCaptchaType = target.dataset.captchaType;
      renderActiveCaptchaType();
      appendLog("切换验证码模式", { captchaType: state.activeCaptchaType });
      setPayload("当前验证码模式", {
        captchaType: state.activeCaptchaType,
        title: captchaModes[state.activeCaptchaType].title
      });
    });

    els.loginTrigger.addEventListener("click", function () {
      if (!ensureCredentials()) {
        return;
      }

      document.getElementById(captchaModes[state.activeCaptchaType].proxyButtonId).click();
    });

    els.clearLog.addEventListener("click", function () {
      state.logLines = [];
      els.log.textContent = "[system] 日志已清空，等待新的请求…";
      setPayload("等待请求", { message: "日志已清空，等待新的接口响应" });
    });
  }

  function initializeWidgets() {
    const sizes = getWidgetSizes();

    $("#slider-fixed").slideVerify({
      baseUrl: "",
      mode: "fixed",
      imgSize: { width: sizes.fixedSliderWidth, height: "190px" },
      barSize: { width: sizes.fixedSliderWidth, height: "42px" },
      explain: "向右拖动，查看滑块校验效果",
      success: function (params) {
        appendLog("嵌入式滑块验证通过", params);
        setPayload("嵌入式滑块响应", params);
      },
      error: function () {
        appendLog("嵌入式滑块验证失败");
      }
    });

    $("#point-fixed").pointsVerify({
      baseUrl: "",
      mode: "fixed",
      imgSize: { width: sizes.fixedPointWidth, height: "240px" },
      success: function (params) {
        appendLog("嵌入式点选验证通过", params);
        setPayload("嵌入式点选响应", params);
      },
      error: function () {
        appendLog("嵌入式点选验证失败");
      }
    });

    $("#slider-pop").slideVerify({
      baseUrl: "",
      mode: "pop",
      containerId: "slider-login-btn",
      imgSize: { width: sizes.popupWidth, height: "210px" },
      barSize: { width: sizes.popupWidth, height: "46px" },
      explain: "拖动完成登录前验证",
      beforeCheck: ensureCredentials,
      success: function (params) {
        appendLog("滑块登录验证通过", params);
        setPayload("滑块登录校验响应", params);
        submitLogin("blockPuzzle", params.captchaVerification).catch(function (error) {
          setResult("请求失败", "模拟登录接口调用失败，请查看日志。", "error");
          appendLog("模拟登录接口异常", String(error));
          setPayload("模拟登录异常", String(error));
        });
      },
      error: function () {
        appendLog("滑块登录验证失败");
      }
    });

    $("#point-pop").pointsVerify({
      baseUrl: "",
      mode: "pop",
      containerId: "point-login-btn",
      imgSize: { width: sizes.popupWidth, height: "210px" },
      beforeCheck: ensureCredentials,
      success: function (params) {
        appendLog("点选登录验证通过", params);
        setPayload("点选登录校验响应", params);
        submitLogin("clickWord", params.captchaVerification).catch(function (error) {
          setResult("请求失败", "模拟登录接口调用失败，请查看日志。", "error");
          appendLog("模拟登录接口异常", String(error));
          setPayload("模拟登录异常", String(error));
        });
      },
      error: function () {
        appendLog("点选登录验证失败");
      }
    });
  }

  window.addEventListener("load", function () {
    loadStatus().catch(function (error) {
      appendLog("状态加载失败", String(error));
      els.origin.textContent = `Origin: ${window.location.origin}`;
      els.cache.textContent = "Cache: 状态获取失败";
      els.aes.textContent = "AES: 状态获取失败";
      els.serviceOrigin.textContent = window.location.origin;
      setPayload("状态加载失败", String(error));
    });

    bindTypeSwitch();
    renderActiveCaptchaType();
    initializeWidgets();
    appendLog("验证码组件已初始化");
  });
})();
