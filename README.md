<div align="center">

<img src="docs/media/hero.svg" width="100%" alt="DeskShift — One wallpaper. A whole new mood." />

### 让整个桌面，跟随一张壁纸。

[简体中文](README.md) · [English](README.en.md) · [日本語](README.ja.md)

<img src="https://img.shields.io/badge/Windows-10%20%2F%2011-526f5d?style=flat-square&amp;logo=windows" alt="Windows 10 / 11" />
<img src="https://img.shields.io/badge/C%23-.NET%20Framework-797593?style=flat-square" alt="C# / .NET Framework" />
<img src="https://img.shields.io/badge/UI-WinForms-84bdb2?style=flat-square" alt="WinForms" />
<img src="https://img.shields.io/badge/Chrome-Manifest%20V3-b4aa8d?style=flat-square&amp;logo=googlechrome&amp;logoColor=white" alt="Chrome Manifest V3" />
<img src="https://img.shields.io/github/stars/Jax367/DeskShift?style=flat-square&amp;color=797593" alt="GitHub stars" />

**[快速开始](#快速开始) · [技术栈](#技术栈) · [反馈问题](https://github.com/Jax367/DeskShift/issues)**

</div>

DeskShift 是一款 Windows 桌面配色工具。从壁纸提取主色，一键更新桌面、系统配色与 Chrome 新标签页，让浅色、薄荷绿和暮色蓝自然延伸到日常界面。

> **完整清晰度**：桌面壁纸以原始分辨率无损缓存。Chrome 新标签页只跟随配色，不显示壁纸；完整标签栏主题需要手动导出、加载。

## 界面一览

![DeskShift 主界面](docs/media/app-preview.png)

## 功能亮点

| 功能 | 体验 |
| :--- | :--- |
| 🎨 自动取色 | 导入 JPG、PNG、BMP，自动生成五色调色板与明暗方案；支持手动调整主色 |
| 🖥️ 一键联动 | 更新桌面壁纸、Windows 强调色和浅色 / 深色模式 |
| 🌐 Chrome 配色 | 新标签页自动读取本机配色；支持导出完整静态主题 |
| ✨ 全屏过渡 | 五种覆盖与揭幕效果，随机不连重复；六种几何等待动效 |
| ⚡ 启动优化 | 隐藏窗口预热、首帧就绪后计时、窗口与资源复用、多显示器共享等待帧 |
| 🧭 托盘切换 | 右键 → 切换壁纸，直接选择图片；当前壁纸显示勾选标记 |
| ↩️ 一步撤销 | 保存切换前的壁纸与程序修改的注册表值，跨重启保留备份 |
| 🏠 本地处理 | 取色与图片处理均在本机完成，不上传壁纸 |

<details>
<summary><b>查看过渡效果与等待动效</b></summary>

五种全屏过渡：斜向扫幕、对开幕帘、交错条带、圆形扩散、方格翻涌。

![全屏过渡效果静态预览](docs/media/transition-preview.png)

六种等待动效：轨道光环、无限回环、模块矩阵、节奏波列、呼吸花环、折叠棱镜。以下为静态预览。

![几何等待动效静态预览](docs/media/waiting-preview.png)

</details>

## 快速开始

**环境**：Windows 10 / 11，.NET Framework 4.x。使用系统自带编译器，无需安装 .NET SDK、NuGet、Node 或 Python。

```powershell
git clone https://github.com/Jax367/DeskShift.git
cd DeskShift
powershell -ExecutionPolicy Bypass -File .\build.ps1
.\dist\DeskShift.exe
```

1. 选择内置壁纸，或通过「添加壁纸」 / 拖放导入图片。
2. 调整主色、明暗模式、系统配色与过渡设置。
3. 点击「一键应用桌面」。关闭窗口后留在托盘，右键即可切换壁纸。

当前仓库提供源码，运行文件由构建脚本生成。完整操作说明见 [中文使用指南](docs/GUIDE.zh-CN.md)。

### 安装 Chrome 扩展

打开 `chrome://extensions`，开启「开发者模式」，点击「加载已解压的扩展程序」，选择构建输出中的 `dist/browser/chrome` 文件夹。保持 DeskShift 在后台运行，并至少应用一次壁纸。扩展会替换原有新标签页。

| 联动范围 | 使用方式 |
| :--- | :--- |
| 桌面壁纸、Windows 配色 | 程序内一键切换 |
| Chrome 新标签页配色 | 安装扩展后自动同步，每 2.5 秒读取一次 |
| Chrome 完整标签栏主题 | 程序内导出，手动加载；不自动切换 |

## 技术栈

| 层级 | 技术 | 用途 |
| :--- | :--- | :--- |
| 桌面应用 | C# · .NET Framework 4.x · WinForms | 壁纸库、设置、预览、托盘菜单 |
| 图像与动画 | System.Drawing · GDI+ | 像素采样、RGB 分桶取色、遮罩与几何动效 |
| Windows 联动 | Win32 P/Invoke · 注册表 · Windows `.theme` | 原尺寸壁纸、强调色、明暗模式与撤销 |
| 动画资源 | 后台任务 · 首帧屏障 · 窗口池 · LRU 缓存 | 提前准备、资源复用、跨显示器共享 |
| 浏览器扩展 | JavaScript · HTML · CSS · Chrome Manifest V3 | 新标签页颜色同步与渐变 |
| 本机通信 | C# HTTP 桥接 · JSON | `127.0.0.1:47831` 只读配色接口 |
| 构建与验证 | PowerShell · 系统 C# 编译器 · 内置自测 | 无额外包依赖构建、隐藏窗口验证 |

### 项目结构

```text
src/                    桌面应用、系统接口、动画与本机桥接
browser/chrome/         Chrome 新标签页扩展
assets/                 软件图标与三张原始壁纸
tests/                  动画性能诊断
tools/                  图标生成工具
docs/                   使用指南与展示素材
build.ps1               构建入口
```

## 验证与说明

```powershell
$p = Start-Process .\dist\DeskShift.exe -ArgumentList '--self-test' -Wait -PassThru
$p.ExitCode
Start-Process .\dist\DeskShift.exe -ArgumentList '--smoke' -Wait
```

自测和窗口预览不会修改真实桌面；输出保存在 `test-output`。真实 Windows 主题刷新和 Chrome 扩展安装需在目标环境确认。

- Windows 浅色模式不支持开始菜单与任务栏强调色，部分应用需重新打开才能刷新主题。
- 首帧预热和缓存能减少初始化开销；`DwmFlush` 只同步本程序，无法保证所有应用同时完成渲染。
- 数据位于 `%LOCALAPPDATA%\DeskShift`，程序不会自动开机启动。
- 本机桥接检查 Host 与 Origin，仅开放只读接口；扩展只申请自身存储和本机连接权限。
- README 支持三种语言；当前软件界面为简体中文。

## 参考与致谢

[Chrome 主题文档](https://developer.chrome.com/docs/extensions/develop/ui/themes) · [Windows 壁纸接口](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfow) · [DwmFlush](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmflush)

动效为项目原创几何绘制，设计参考 [MetaLab](https://dribbble.com/shots/4037784-Geometric-shapes-loading-animation)、[Infinity Loader](https://dribbble.com/shots/2465356-Infinity-Loader) 与 [IBM Design](https://www.ibm.com/design/language/animation/tips-and-techniques/)。内置壁纸由用户提供；作者署名针对本项目程序，不代表壁纸版权归属。

## 来访记录

<div align="center">
<a href="https://github.com/journey-ad/Moe-Counter">
<img src="https://moecount.lolihouse.top/get/@jax367-deskshift?theme=moebooru" alt="DeskShift Moe Counter — Moebooru theme" />
</a>

基于 [Moe Counter](https://github.com/journey-ad/Moe-Counter)，使用 [Lolihouse 托管服务](https://moecount.lolihouse.top/)。图片请求计数会受 GitHub 图片缓存影响，不代表独立访客数。

**Lar** · [turelar367@gmail.com](mailto:turelar367@gmail.com)

</div>
