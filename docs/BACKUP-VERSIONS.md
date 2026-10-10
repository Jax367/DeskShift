# GitHub 版本备份

2026-10-11 保存了两个独立版本，署名 Lar。

| 版本 | 源码标签 | 下载 |
| --- | --- | --- |
| 稳定版：深色新标签页、独立压缩浏览器背景 | `backup/stable-2026-10-11` | [稳定版便携包](https://github.com/Jax367/DeskShift/raw/refs/tags/backup/stable-2026-10-11/releases/DeskShift-stable.zip) |
| 试验版：覆盖期间自动加载标题栏主题，默认关闭 | `backup/auto-theme-2026-10-11` | [试验版便携包](https://github.com/Jax367/DeskShift/raw/refs/tags/backup/auto-theme-2026-10-11/releases/DeskShift-auto-theme-experimental.zip) |

解压后运行 DeskShift.exe。启动另一版本前，请退出系统托盘中的旧版本。

稳定版不包含自动加载试验。试验版的适用条件、测试结果及回退步骤见 [自动加载说明](AUTO-THEME.zh-CN.md)。该说明只在试验版源码中提供。

2026-10-11 用户实测：关闭所有 Edge 窗口后重新打开，顶部标签栏配色生效。这是当前用户环境中的观察；没有确认后台进程是否完全退出，也不能据此认定覆盖期间的自动加载已经成功。

发布包仅包含程序、内置壁纸、扩展及文档；本机设置、浏览器资料、测试配置与 .pem/.crx 私钥或打包文件不在发布包内。
