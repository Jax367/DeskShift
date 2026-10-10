# DeskShift 桌面随心换

作者：Lar · 联系邮箱：turelar367@gmail.com

Windows 10/11 桌面程序。已内置用户提供的三张壁纸；导入新图片时自动提取主色、五色调色板和明暗方案。

## 开始使用

双击 `DeskShift.exe`（源码目录中为 `dist\DeskShift.exe`）。选择壁纸，点击「一键应用桌面」。Windows 会应用填充模式壁纸、主色、窗口强调色及浅色/深色模式。应用系统主题时，Windows 可能打开个性化设置窗口；无需管理员权限。开始菜单与任务栏强调色在 Windows 浅色模式中不受支持。部分应用需重新打开才能显示新的明暗模式。

- 点击「添加壁纸」或将 JPG、PNG、BMP 拖入窗口；导入后图片会复制到个人壁纸库，原文件可以移动。
- 点击「调整主色」修正自动取色；明暗模式可选自动、浅色、深色。
- 取消「同步 Windows 配色」可仅切换壁纸并发布浏览器配色。
- 选择壁纸或修改配色时，预览使用约 220 毫秒的淡入过渡。应用、撤销在后台运行，底部动画显示正在处理，切换期间可以移动窗口或收起到托盘。桌面图片按原始分辨率无损缓存，不缩小、不重采样，重复切换直接复用。预览缩略图只用于窗口显示，不用于设置桌面。Chrome 新标签页配色采用约 260 毫秒渐变，遵循系统减少动画设置。
- 默认启用「全屏过渡」，提供斜向扫幕、对开幕帘、交错条带、圆形扩散和方格翻涌。默认随机且不连续重复，可以在「过渡效果」中固定选择。先在后台准备原尺寸图片、主题文件、文件读取缓存，以及每个显示器的动画背景和遮罩帧，再开始约 500 毫秒覆盖。完全遮住后执行切换，等待壁纸设置稳定约 450 毫秒和本程序的 DWM 更新同步，然后约 650 毫秒揭幕；系统较慢时等待操作返回，不再因为固定动画时间到了提前揭幕。遮罩不显示文字中间页，等待阶段中央随机展示六种几何设计动画：轨道光环、无限回环、模块矩阵、节奏波列、呼吸花环、折叠棱镜。每次切换独立随机且不连续重复，多显示器保持同款同步，配色跟随壁纸。只预生成当前选中的动画，按 30 帧每秒循环，等待时仅重绘小块区域；切换完成即揭幕，不额外等待动画播完。12 秒超时保护仍可撤去覆盖。在程序拥有键盘焦点时 Esc 可以跳过动画，也可取消勾选「全屏过渡」。全屏过渡遵循系统动画设置。
- 「下一张」立即应用下一张壁纸。关闭窗口会收起到托盘，双击托盘图标重新打开。右键托盘图标，在「切换壁纸」子菜单中直接选择图片，会按当前设置应用壁纸、配色与过渡动画；当前使用的壁纸显示勾选标记，新导入的图片自动加入菜单。切换期间相关菜单项暂时禁用。右键菜单也可切换下一张、撤销或退出。
- 「撤销上次切换」恢复上一次切换前的壁纸及程序修改的注册表值。只能撤销一步；切换备份会跨程序重启保留。壁纸恢复依赖原壁纸文件仍存在。Windows 主题引擎异步应用，待主题应用完成再撤销；个别 Windows 版本的强调色刷新可能需要重新打开应用或注销。

## Chrome 联动

动画启动优化：程序打开后会在隐藏状态预热全屏窗口、原生窗口句柄和首次绘制。每次切换先准备当前效果，等各显示器的首帧绘制返回并交还界面消息循环，再启动动画计时，避免初始化耗时吃掉开场动画。全屏窗口按显示器位置和尺寸复用；背景、遮罩、等待帧按尺寸、配色和效果缓存。不同显示器共享同一份等待帧，缓存采用约 160 MiB 的预算和最近使用淘汰，正在播放的资源不会被回收。此预算不包含窗口缓冲等系统内存。上述优化不改变桌面原图分辨率，也不能保证系统或显卡呈现完全没有停顿。

安装一次配套新标签页扩展后，新的标签页会每 2.5 秒读取本机 DeskShift 的当前配色，使用纯色背景，不显示桌面壁纸。完整标签栏配色另外提供静态 Chrome 主题导出，需要用户手动加载。**一键自动联动覆盖壁纸、Windows 主题和 Chrome 新标签页；不包含 Chrome 完整标签栏自动切换。** Chrome 没有提供公开的动态主题接口，本程序不修改浏览器的内部用户配置文件。

安装步骤见程序内「Chrome 扩展安装说明」，或打开 `browser\chrome\INSTALL.html`。扩展会替换原有新标签页；移除扩展即可恢复。DeskShift 需要在托盘运行且已应用过壁纸。退出后新标签页保留最近的配色。连接冲突时程序会显示错误，系统切换仍可使用。

仅启动程序、选择壁纸或导入图片不会改变系统设置。配色和图片在本地处理。数据放在 `%LOCALAPPDATA%\DeskShift`，包含壁纸库、切换前备份、主题导出和壁纸 BMP。卸载时先撤销切换，再退出程序并删除该目录与程序目录。程序不自动开机启动。

## 构建和验证

软件包含专属图标，统一用于可执行文件、窗口、任务栏与系统托盘。

不依赖 NuGet、Python 或 Node。使用 Windows 自带 .NET Framework 4.x C# 编译器与 Windows Forms / GDI+。

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
$p = Start-Process .\dist\DeskShift.exe -ArgumentList '--self-test' -Wait -PassThru
$p.ExitCode
Start-Process .\dist\DeskShift.exe -ArgumentList '--smoke' -Wait
```

测试输出位于 `test-output`。自测验证初始图片取色、文字对比度、导入与持久化、主题格式和只读浏览器桥接；`--smoke` 生成窗口预览截图。上述命令不会切换真实桌面。实际 Windows 主题与 Chrome 安装效果需要在目标系统上确认。

## 实现与接口

`src/Core.cs`：160×100 像素采样、RGB 分桶、色彩覆盖率与饱和度加权取色；浅色背景不会压过全部彩色主体。`src/WindowsTheme.cs`：SPI_SETDESKWALLPAPER、当前用户注册表备份及 Windows `.theme` 主题引擎。`src/Bridge.cs`：127.0.0.1:47831 上仅开放 `GET /theme` 和 `GET /wallpaper`，检查 Host 与 Origin，不接受写入、任意文件路径或执行指令。`src/App.cs`：预览、壁纸库、导入及托盘。`src/DesktopTransition.cs`：不抢焦点的多显示器全屏过渡，提前缓存背景与遮罩帧。Windows 没有统一的所有应用主题渲染完成信号；本程序检查壁纸设置稳定并给刷新留出时间，DwmFlush 仅同步本程序的排队更新，不能保证其他应用或整个会话已经渲染完毕。

浏览器扩展仅申请本机连接和自身存储权限。桥接不上传图片，也不会操作 Chrome 里的其他扩展或网页。原始图片保留在 `assets\wallpapers`。

接口参考：[Chrome 官方主题文档](https://developer.chrome.com/docs/extensions/develop/ui/themes)、[Windows SystemParametersInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfow)、[WM_SETTINGCHANGE](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-settingchange)。

渲染同步参考：[DwmFlush 官方文档](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmflush)。

等待动效为本项目原创几何绘制，设计参考：[MetaLab 几何加载动效](https://dribbble.com/shots/4037784-Geometric-shapes-loading-animation)、[Nick Buturishvili 的 Infinity Loader](https://dribbble.com/shots/2465356-Infinity-Loader)、[IBM 动效技巧](https://www.ibm.com/design/language/animation/tips-and-techniques/)。
