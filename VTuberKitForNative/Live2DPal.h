#pragma once

#include <CubismFramework.hpp>
#include <string>

// Live2D用ユーティリティ関数
namespace VTuberKitForNative {

class Live2DPal {
public:
    // ログ出力
    static void PrintMessage(const char* message);
    static void PrintLog(const char* format, ...);
    static void PrintLogLn(const char* format, ...);

    // ファイル読み込み - SDKのcsmLoadFileFunctionに厳密に合わせる
    // typedef csmByte* (*csmLoadFileFunction)(const std::string filePath, csmSizeInt* outSize);
    static Csm::csmByte* __cdecl LoadFileAsBytes(const std::string filePath, Csm::csmSizeInt* outSize);
    static void ReleaseBytes(Csm::csmByte* bytes);

    // 文字列変換
    static void ConvertWideToMultiByte(const wchar_t* wideStr, char* dest, int destSize);
    static void ConvertMultiByteToWide(const char* multiByteStr, wchar_t* dest, int destSize);

    // 安全な文字列変換ヘルパー
    static std::wstring StringToWString(const std::string& str);

    // デルタ時間
    static void UpdateTime();
    static float GetDeltaTime();
    static float GetAppTime();
};

} // namespace VTuberKitForNative
