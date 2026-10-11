<div align="center">

<img src="docs/media/hero.svg" width="100%" alt="DeskShift — One wallpaper. A whole new mood." />

### 一枚の壁紙で、デスクトップの雰囲気を変える。

[简体中文](README.md) · [English](README.en.md) · [日本語](README.ja.md)

<img src="https://img.shields.io/badge/Windows-10%20%2F%2011-526f5d?style=flat-square&amp;logo=windows" alt="Windows 10 / 11" />
<img src="https://img.shields.io/badge/C%23-.NET%20Framework-797593?style=flat-square" alt="C# / .NET Framework" />
<img src="https://img.shields.io/badge/UI-WinForms-84bdb2?style=flat-square" alt="WinForms" />
<img src="https://img.shields.io/badge/Chrome-Manifest%20V3-b4aa8d?style=flat-square&amp;logo=googlechrome&amp;logoColor=white" alt="Chrome Manifest V3" />
<img src="https://img.shields.io/github/stars/Jax367/DeskShift?style=flat-square&amp;color=797593" alt="GitHub stars" />

**[使い方](#使い方) · [技術スタック](#技術スタック) · [不具合の報告](https://github.com/Jax367/DeskShift/issues)**

</div>

DeskShift は Windows 向けのデスクトップ配色ツールです。壁紙から配色を抽出し、壁紙と Windows の色をワンクリックで切り替え、Chrome / Edge 用の配色テーマを書き出します。

> **シンプルな新しいタブ**：元の壁紙・検索・よく訪れるサイトだけ。時計、日付、ニュースは表示しません。タブバーのテーマは引き続き手動で読み込みます。

ショートカットはブラウザーが保存したサイト本来の favicon を使用し、検索欄のすぐ下に中央配置します。タイトルバーのテーマは配色ガイドから別途読み込む必要があり、新しいタブの同期だけでは自動更新されません。

## プレビュー

![DeskShift の画面 — 現在の UI は簡体字中国語](docs/media/app-preview.png)

標準サイズは約 940 × 660。サイドバーで壁紙・切り替え設定・ブラウザー連携を分け、角丸カードとスイッチで設定を整理しました。小さい画面ではウィンドウを調整し、スクロールで操作できます。

![DeskShift の切り替え設定](docs/media/settings-preview.png)

## 主な機能

| 機能 | 内容 |
| :--- | :--- |
| 🎨 自動配色 | JPG・PNG・BMP から 5 色とライト / ダークの配色を抽出。アクセント色の手動調整も可能 |
| 🖥️ デスクトップ連動 | 壁紙、Windows のアクセント色、ライト / ダークモードを変更 |
| 🌐 新しいタブ | 壁紙と配色を同期し、検索と最大 8 件のよく訪れるサイトを表示 |
| ✨ 全画面トランジション | 5 種類の切り替え効果と 6 種類の幾何学的な待機アニメーション |
| ⚡ 起動の最適化 | 非表示ウィンドウの事前準備、初回描画後の計時開始、ウィンドウと素材の再利用 |
| 🧭 トレイから切り替え | 右クリックで壁紙を選択。現在の壁紙にはチェックを表示 |
| 🫧 フローティングボタン | 表示を切り替え可能。ドラッグ位置を保存し、クリックで壁紙のサムネイル一覧を表示 |
| ↩️ 一段階の取り消し | 変更前の壁紙とアプリが変更したレジストリ値を保存。再起動後も利用可能 |
| 🏠 ローカル処理 | 配色抽出と画像処理は PC 内で実行 |

<details>
<summary><b>アニメーションのデザインを見る</b></summary>

斜めのスイープ、左右のカーテン、交互の帯、円形、タイルによる全画面効果。

![トランジションの静止画プレビュー](docs/media/transition-preview.png)

軌道リング、無限ループ、グリッド、波、呼吸する花形、折り畳みプリズム。以下は静止画です。

![待機アニメーションの静止画プレビュー](docs/media/waiting-preview.png)

</details>

## 使い方

**必要環境**：Windows 10 / 11、.NET Framework 4.x。Windows の Framework コンパイラーを使い、.NET SDK・NuGet・Node・Python の追加インストールは不要です。

```powershell
git clone https://github.com/Jax367/DeskShift.git
cd DeskShift
powershell -ExecutionPolicy Bypass -File .\build.ps1
.\dist\DeskShift.exe
```

1. 内蔵壁紙を選ぶか、追加ボタン / ドラッグ＆ドロップで画像を取り込みます。
2. 主色、明暗モード、Windows 配色の同期、切り替え効果を設定します。
3. メインの適用ボタンを押します。ウィンドウを閉じてもトレイに常駐し、右クリックで壁紙を切り替えられます。

このリポジトリはソースコードを提供します。実行ファイルはビルドで生成されます。README は 3 言語に対応していますが、**アプリの UI は現在、簡体字中国語です**。

切り替え設定のデスクトップショートカットでフローティングボタンを有効にできます。クリックで壁紙を選び、ドラッグで移動します。一覧の外をクリックするか Esc で閉じます。右クリックでアプリを開くかボタンを非表示にできます。初期状態はオフで、設定と位置を保存します。

### シンプルな新しいタブ

よく訪れるサイトを自動表示するほか、「添加网址」から手動で追加できます。手動のリンクを優先し、空いた枠を自動候補で補います。カードの「⋯」で編集・削除・候補の非表示が可能です。ダイアログの自動補充をオフにすると手動リストだけを表示します。非表示候補は復元でき、設定は現在のブラウザー内に保存して再起動後も保持します。


`chrome://extensions` または `edge://extensions` の開発者モードで `browser/chrome` または `browser/edge` を読み込みます。新版 DeskShift を起動したまま壁紙を適用してください。新しいタブを置き換えますが、起動 URL・ホームボタン URL・ブラウザーの既定検索設定は変更しません。

壁紙、検索、最大 8 件のよく訪れるサイトだけを表示します。`topSites` を使い、完全な `history` 権限は要求せず、サイト一覧も送信しません。Chrome は Google、Edge は Bing を検索します。ブラウザーが決める順位は Edge の固定ショートカットと異なる場合があります。

切替設定の「ブラウザー背景」で、圧縮した原画像・アクリルぼかし・抽象的な色面を選べます。原画像は構図を保持し、ぼかしは細部を柔らかくします。抽象的な色面は壁紙のパレットだけで大きな曲線と色面を描き、人物や輪郭を残しません。スタイル変更はブラウザー背景に反映され、システムテーマを再適用しません。デスクトップの原画像は保持し、ブラウザー用には長辺最大 2560 ピクセル・品質 82 の独立した JPEG コピーを生成し、最近の 1 枚だけをオフライン用に保存します。非表示タブは更新を停止します。時計や日付はありません。タブバーの配色テーマは手動で読み込みます。任意の検証：`node tests/browser-sync.js`。

![新しいタブの例・サイトはデモデータ](docs/media/newtab-preview.png)

## 技術スタック

| 層 | 技術 | 用途 |
| :--- | :--- | :--- |
| デスクトップ | C# · .NET Framework 4.x · WinForms | 壁紙ライブラリ、設定、プレビュー、トレイ |
| 描画 | System.Drawing · GDI+ | 色抽出、RGB の分類、マスク、幾何学アニメーション |
| Windows 連携 | Win32 P/Invoke · レジストリ · `.theme` | 原寸壁紙、アクセント色、明暗モード、取り消し |
| アニメーション素材 | バックグラウンド処理 · 初回描画の待機 · ウィンドウプール · LRU キャッシュ | 事前準備と再利用、複数画面での待機フレーム共有 |
| ブラウザー拡張機能 | JavaScript · HTML · CSS · Manifest V3 · topSites | 新しいタブ、よく訪れるサイト、壁紙キャッシュ |
| ローカル通信 | C# · TCP / HTTP · JSON | 壁紙・配色の読み取り専用 API |
| ビルド・検証 | PowerShell · Framework C# コンパイラー · 内蔵テスト | 外部パッケージ不要のビルドと非表示ウィンドウの検証 |

### 構成

```text
src/                    デスクトップアプリ、Windows 連携、アニメーション、テーマ
browser/chrome/         Chrome 新しいタブとガイド
browser/edge/           Edge 新しいタブとガイド
assets/                 アイコンと元の壁紙 3 枚
tests/                  アニメーションの性能計測
tools/                  アイコン生成
docs/                   ガイドと画像
build.ps1               ビルドスクリプト
```

## 検証と動作について

```powershell
$p = Start-Process .\dist\DeskShift.exe -ArgumentList '--self-test' -Wait -PassThru
$p.ExitCode
Start-Process .\dist\DeskShift.exe -ArgumentList '--smoke' -Wait
```

テストと画面プレビューは実際のデスクトップを変更しません。結果は `test-output` に保存されます。Windows のテーマ更新と Chrome の導入は実環境で確認してください。

- Windows のライトモードではスタートメニューとタスクバーのアクセント色は使えません。一部のアプリは再起動が必要です。
- 事前準備は初期化の負荷を減らします。`DwmFlush` は本アプリのみを同期し、他のアプリの描画完了は保証しません。
- データは `%LOCALAPPDATA%\DeskShift` に保存します。自動起動は登録しません。
- 権限は自身の保存、よく訪れるサイト、ローカル接続のみ。ブラウザー設定ファイルは編集せず、実行・ビルドに Node は不要です。
- [詳細な中国語ガイド](docs/GUIDE.zh-CN.md)。

## 参考・クレジット

[Chrome テーマ](https://developer.chrome.com/docs/extensions/develop/ui/themes) · [Windows 壁紙 API](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfow) · [DwmFlush](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmflush)

幾何学アニメーションは本プロジェクト独自の描画です。デザイン参考：[MetaLab](https://dribbble.com/shots/4037784-Geometric-shapes-loading-animation)、[Infinity Loader](https://dribbble.com/shots/2465356-Infinity-Loader)、[IBM Design](https://www.ibm.com/design/language/animation/tips-and-techniques/)。内蔵壁紙はユーザー提供です。プロジェクトの作者表記は壁紙の著作権を意味しません。

## 訪問カウンター

<div align="center">
<a href="https://github.com/journey-ad/Moe-Counter">
<img src="https://moecount.lolihouse.top/get/@jax367-deskshift?theme=moebooru" alt="DeskShift Moe Counter — Moebooru theme" />
</a>

[Moe Counter](https://github.com/journey-ad/Moe-Counter) に基づく [Lolihouse](https://moecount.lolihouse.top/) のサービスを使用しています。GitHub の画像キャッシュの影響を受けるため、ユニークユーザー数ではありません。

**Lar** · [turelar367@gmail.com](mailto:turelar367@gmail.com)

</div>

バージョンのバックアップとダウンロード：[安定版・試験版](docs/BACKUP-VERSIONS.md)。
