# VTuberKitForYMM4（C# プラグイン本体）

このフォルダは YMM4 向け Live2D 立ち絵プラグインの C# 実装です。

## 主な役割

- `Plugin/Live2DTachieSource.cs` : 描画更新と D3D11/D2D 連携
- `Plugin/TachieMotionEvaluator.cs` : Face/Item のモーション・パラメータ適用
- `Parameters/*.cs` : YMM4 の編集UIに表示するパラメータ定義
- `Commons/CustomPropertyEditor/*` : カスタムコンボ UI（Expression/Motion/Param/Part 選択）

## 参照先

セットアップ、ビルド手順、機能概要はルートの [README.md](../README.md) を参照してください。