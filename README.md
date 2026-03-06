# VTuberKitForYMM4

> [!CAUTION]
> このプラグインに興味を持っていただきありがとうございます。
> Live2Dの[拡張性アプリケーション](https://www.live2d.com/sdk/license/expandable/)に審査と契約が必要なことを確認していなかったため、一旦リリースビルドを削除しました。
> 完全無料でも契約が可能な場合があるので、一旦申請してみます。その結果を見て今後のことは考えます。
> お騒がせして申し訳ないです。

[![Build Status](https://github.com/takoyakisoft/VTuberKitForYMM4/actions/workflows/build.yml/badge.svg)](https://github.com/takoyakisoft/VTuberKitForYMM4/actions/workflows/build.yml)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](#)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](#)

![Image](assets/VTuberKitForYMM4.png)


Live2DモデルをゆっくりMovieMaker4（YMM4）で立ち絵として使用するためのプラグインです。

<p align="center">
  <img src="assets/sample.gif" width="100%" alt="サンプル動画"/>
</p>

## 使用例
私のチャンネルでの使用例です。もしよければ見てみてください。
[▶ YouTubeチャンネル（takoyaki-soft）](https://www.youtube.com/@takoyaki-soft)

> [!NOTE]
> 動画内で使用しているプラグインは旧バージョンのため、現在のものとは仕様が異なります。

## インストール方法

1. [GitHub Releases](https://github.com/takoyakisoft/VTuberKitForYMM4/releases)から最新の`VTuberKitForYMM4.zip`をダウンロードします。
2. `YukkuriMovieMaker4/user/plugin`フォルダ内に、ダウンロードしたファイルをそのまま解凍します。

## 使い方

### モデルの準備

#### 1. 公式のサンプルモデルを使う場合
次のリンクから公式のサンプルモデルをダウンロードできます。
[Live2D サンプルデータ集](https://www.live2d.com/learn/sample/)

> [!NOTE]
> 個人利用なら商用利用可能とのことですが、モデルごとに利用規約を確認してから使用してください。

#### 2. ご自身で用意する場合
> [!NOTE]
> Live2D Cubism EditorのFree版で試しています。

自作モデルを作る際の参考として、手描きで作成したLive2Dサンプルモデルを [GitHub Releases](https://github.com/takoyakisoft/VTuberKitForYMM4/releases/tag/sample_model) にて **CC0（パブリックドメイン）** で配布しています。自由にお使いください。
- `takoyakisoft_runtime.zip`: YMM4ですぐに読み込める書き出し済みモデルデータです。
- `takoyakisoft_source.zip`: Live2D Cubism Editorで開ける編集用の元データ（`.cmo3`, `.can3`）です。

自身で独自のモデルを作成する場合は、Live2D Cubism Editorで以下のファイルを用意します。
- モデルデータ: `.cmo3`
- アニメーションデータ: `.can3` （モーションがないとデフォルトの動作が不安定になる場合があります）

##### モデルデータの書き出し
![Image](assets/docs/cmo3_1.png)
「ファイル」→「組込み用ファイル書き出し」→「moc3ファイル書き出し」をクリックします。

![Image](assets/docs/cmo3_2.png)
「OK」をクリックします。

![Image](assets/docs/cmo3_3.png)

##### アニメーションデータの書き出し
![Image](assets/docs/can3_1.png)
「ファイル」→「組込み用ファイル書き出し」→「アニメーションファイル書き出し」をクリックします。

![Image](assets/docs/can3_2.png)
「OK」をクリックします。

![Image](assets/docs/can3_3.png)
「新規フォルダ作成」をクリックし「motions」フォルダを作成します。
その中に「保存」します。


> [!WARNING]
> **Free版で書き出す場合のアニメーション設定について**
> Live2D Cubism EditorのFree版では、書き出し時にアニメーション（モーション）が `.model3.json` に自動的に紐付けられません。
> そのため、書き出し完了後、出力された `.model3.json` をメモ帳などのテキストエディタで開き、以下のように手動で `Motions` の項目を追記する必要があります。
> 
> 追記例:
> ```json
> 	"FileReferences": {
> 		"Moc": "takoyakisoft.moc3",
> 		"Textures": [ ... ],
> 		"Physics": "takoyakisoft.physics3.json",
> 		"DisplayInfo": "takoyakisoft.cdi3.json",
> 		// ↓ ここから追記                       ↑ このカンマも忘れないように
> 		"Motions": {
> 			"Idle": [
> 				{ "File": "motions/idle.motion3.json" }
> 			]
> 		}
> 		// ↑ ここまで
> 	},
> ```

### VTuberKitForYMM4での読み込み

![Image](assets/docs/YMM4_1.png)
ここをクリックします。

![Image](assets/docs/YMM4_2.png)
> [!CAUTION]
> キャラクター設定は必ず複製してください。このプラグインがなくなるときキャラクター設定も消えます！
「立ち絵」の「種類」を「VTuberKit Live2D」を選択します。
「モデルファイル」に用意したモデルデータ（`.model3.json`）を選択します。

### 実際に動かす

![Image](assets/docs/YMM4_3.png)
ここはYMM4の説明なので、ざっくり説明します。
1の先ほど作ったキャラクターを選択して、2で立ち絵アイテムを最初のトラックに配置します。
3と4で声と表情のアイテムを配置します。
5では立ち絵アイテムに待機モーションを割り当てています。


![Image](assets/docs/YMM4_4.png)
声と表情のアイテムの設定は共通です。
Live2D自体に表情やモーションの機能があるらしいですが、待機モーション一つでも動いています。


## 開発について

このプロジェクトは 1年前の私の動画で使用したプラグインを元に公開するために[GitHub Copilot](https://github.com/features/copilot)（GPT-5.3-Codex, Gemini 3.1 Pro, Claude Opus 4.6）を使用してコードを全面的に書き直しました。私のコードはほぼなくなったので、コードの編集には LLM の活用をお勧めします。

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
