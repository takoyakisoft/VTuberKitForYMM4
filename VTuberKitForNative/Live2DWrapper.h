#pragma once

// C++/CLI ラッパー メインエントリーポイント
// このファイルをC#側から参照してください

#include "Live2DModel.h"
#include "Live2DRenderer.h"
#include "Live2DManager.h"

namespace VTuberKitForNative {

// プラグイン用エクスポート関数
public ref class Live2DPlugin {
public:
    // プラグイン初期化
    static bool InitializePlugin();
    
    // プラグイン終了
    static void ReleasePlugin();
    
    // バージョン情報
    static System::String^ GetVersion();
    
    // Cubism SDKバージョン
    static System::String^ GetCubismVersion();
};

} // namespace VTuberKitForNative
