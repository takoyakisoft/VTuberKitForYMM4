<p align="center">
  <img src="assets/VTuberKitForYMM4.png" alt="VTuberKitForYMM4" width="800"/>
</p>

<p align="center">
  <a href="https://github.com/takoyakisoft/VTuberKitForYMM4/actions/workflows/build.yml"><img src="https://github.com/takoyakisoft/VTuberKitForYMM4/actions/workflows/build.yml/badge.svg" alt="Build Status"/></a>
  <a href="#"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License"/></a>
  <a href="#"><img src="https://img.shields.io/badge/.NET-10.0-purple.svg" alt=".NET"/></a>
</p>

Live2DモデルをゆっくりMovieMaker4（YMM4）で立ち絵として使用するためのプラグインです。

<p align="center">
  <img src="assets/sample.gif" alt="サンプル動画" width="640"/>
</p>

## インストール方法

1. [GitHub Releases](https://github.com/takoyakisoft/VTuberKitForYMM4/releases)から最新の`VTuberKitForYMM4.zip`をダウンロードします。
2. `YukkuriMovieMaker4/user/plugin`フォルダ内に、ダウンロードしたファイルをそのまま解凍します。

## 使い方

1. YMM4の立ち絵設定で「Live2D立ち絵」を選択します。
2. Live2Dモデルファイル（`.model3.json`）を指定します。
3. タイムライン上でパラメータ・モーション・表情などを編集できます。

### 主な機能

- **Parameters** — 顔角度・目・口・視線・頬・腕などの操作
- **Custom Param / Part** — 任意の Parameter ID / Part ID を直接指定して上書き
- **Motions** — Idle / Face モーションの選択と再生
- **Expressions** — 表情の選択
- **自動動作** — EyeBlink / LipSync / Physics / Breath の有効化・調整
- **トランスフォーム** — Item / Face 単位の位置・拡大・回転
- **見た目調整** — 乗算色・不透明度
- **描画品質** — 内部倍率、RT最大サイズ、MSAA、FXAA

## 開発について

このプロジェクトは [GitHub Copilot](https://github.com/features/copilot)（GPT-5.3-Codex, Gemini 3.1 Pro, Claude Opus 4.6）を使用して作成しています。コードの編集には LLM の活用をお勧めします。

## ソースからビルド

### 必要なもの

- **Visual Studio 2026**（C++ デスクトップ開発ワークロード導入済み）
- **YukkuriMovieMaker4**
- **Live2D Cubism SDK for Native** (v5-r.4.1)

### 手順

1. [Live2D 公式サイト](https://www.live2d.com/download/cubism-sdk/download-native/)から Cubism SDK for Native をダウンロードし、リポジトリルートに展開します。
   ```
   VTuberKitForYMM4/
   ├── CubismSdkForNative/
   ├── VTuberKitForNative/
   ├── VTuberKitForYMM4/
   └── ...
   ```

2. `Directory.Build.props.sample` を `Directory.Build.props` にコピーし、`YMM4DirPath` を自分の環境に合わせて編集します。

3. Visual Studio 2026 で `VTuberKitForYMM4.sln` を開き、`Release|x64` でビルドします。

4. ビルド後、自動的に YMM4 のプラグインフォルダにコピーされます。

**注意：** C++/CLI プロジェクト（`VTuberKitForNative`）を含むため、`dotnet build` 単体ではビルドできません。

## 動作環境

- YukkuriMovieMaker4 v4.49.0.2
- Windows 11 (64bit)

## ライセンス

このソフトウェアはMITライセンスの下で公開されています。

### 使用ライブラリ

- **Live2D Cubism SDK for Native** (v5-r.4.1) — [Live2D Proprietary Software License](https://www.live2d.com/eula/live2d-proprietary-software-license-agreement_en.html)
- **YukkuriMovieMaker4** (v4.49.0.2)

### 謝辞

このプラグインは、以下のプロジェクト・ライブラリのおかげで実現できました。開発者の皆様に心より感謝いたします。

- [YukkuriMovieMaker4](https://manjubox.net/ymm4/) — 饅頭遣い様
- [Live2D Cubism SDK](https://www.live2d.com/) — Live2D Inc.
