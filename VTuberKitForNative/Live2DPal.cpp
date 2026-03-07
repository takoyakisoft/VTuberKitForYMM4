#include "Live2DPal.h"
#include <Windows.h>
#include <mmsystem.h>
#include <stdio.h>
#include <stdarg.h>
#include <string>
#include <cstdio> // Added for fopen, fprintf
#include <ctime>  // Added for time, localtime_s

#pragma comment(lib, "winmm.lib")

#pragma unmanaged

#include "Live2DShader.h"

namespace VTuberKitForNative {

static void WriteLogToFile(const char* message) {
    FILE* fp = nullptr;
    errno_t err = fopen_s(&fp, "VTuberKitForNative.log", "a");
    if (err == 0 && fp) {
        time_t now = time(nullptr);
        struct tm ltm;
        localtime_s(&ltm, &now);
        fprintf(fp, "[%04d/%02d/%02d %02d:%02d:%02d] %s\n", 
            ltm.tm_year + 1900, ltm.tm_mon + 1, ltm.tm_mday,
            ltm.tm_hour, ltm.tm_min, ltm.tm_sec, message);
        fclose(fp);
    }
}

static float s_currentFrame = 0.0f;
static float s_lastFrame = 0.0f;
static float s_deltaTime = 0.0f;

void Live2DPal::PrintMessage(const char* message) {
    OutputDebugStringA(message);
    WriteLogToFile(message);
}

void Live2DPal::PrintLog(const char* format, ...) {
    va_list args;
    va_start(args, format);
    char buf[1024];
    vsnprintf(buf, sizeof(buf), format, args);
    va_end(args);
    OutputDebugStringA(buf);
    WriteLogToFile(buf);
}

void Live2DPal::PrintLogLn(const char* format, ...) {
    va_list args;
    va_start(args, format);
    char buf[1024];
    vsnprintf(buf, sizeof(buf), format, args);
    va_end(args);
    OutputDebugStringA(buf);
    OutputDebugStringA("\n"); // Keep this for debug output
    
    std::string str = buf;
    WriteLogToFile(str.c_str()); // WriteLogToFile adds its own newline
}

// SDKのcsmLoadFileFunctionに厳密に合わせる
// typedef csmByte* (*csmLoadFileFunction)(const std::string filePath, csmSizeInt* outSize);
Csm::csmByte* __cdecl Live2DPal::LoadFileAsBytes(const std::string filePath, Csm::csmSizeInt* outSize) {
    // シェーダーファイルの場合はDLLの隣のShadersフォルダから読み込む
    std::wstring widePath;
    if (filePath.find("CubismEffect.fx") != std::string::npos) {
        // DLLのパスを取得
        wchar_t dllPath[MAX_PATH];
        HMODULE hModule = NULL;
        GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                           (LPCWSTR)&Live2DPal::LoadFileAsBytes, &hModule);
        GetModuleFileNameW(hModule, dllPath, MAX_PATH);
        
        std::wstring shaderPath = dllPath;
        size_t lastSlash = shaderPath.find_last_of(L"\\/");
        if (lastSlash != std::wstring::npos) {
            shaderPath = shaderPath.substr(0, lastSlash + 1) + L"Shaders\\CubismEffect.fx";
            widePath = shaderPath;
        }
    } else {
        // 動的サイズ計算による安全な変換
        widePath = StringToWString(filePath);
    }

    FILE* fp = nullptr;
    _wfopen_s(&fp, widePath.c_str(), L"rb");
    if (fp == nullptr) {
        if (outSize) *outSize = 0;
        PrintLogLn("[Error] Failed to open file: %s", filePath.c_str());
        return nullptr;
    }

    fseek(fp, 0, SEEK_END);
    long size = ftell(fp);
    fseek(fp, 0, SEEK_SET);

    if (size < 0) {
        fclose(fp);
        if (outSize) *outSize = 0;
        return nullptr;
    }

    Csm::csmByte* buf = new Csm::csmByte[size];
    size_t readSize = fread(buf, 1, size, fp); // 引数順序修正 (elemSize, count)
    fclose(fp);

    if (outSize) *outSize = static_cast<Csm::csmSizeInt>(readSize);
    return buf;
}

void Live2DPal::ReleaseBytes(Csm::csmByte* bytes) {
    if (bytes) {
        delete[] bytes;
    }
}

void Live2DPal::ConvertWideToMultiByte(const wchar_t* wideStr, char* dest, int destSize) {
    WideCharToMultiByte(CP_UTF8, 0, wideStr, -1, dest, destSize, nullptr, nullptr);
}

void Live2DPal::ConvertMultiByteToWide(const char* multiByteStr, wchar_t* dest, int destSize) {
    MultiByteToWideChar(CP_UTF8, 0, multiByteStr, -1, dest, destSize);
}

std::wstring Live2DPal::StringToWString(const std::string& str) {
    if (str.empty()) return std::wstring();
    int size_needed = MultiByteToWideChar(CP_UTF8, 0, &str[0], (int)str.size(), NULL, 0);
    std::wstring wstrTo(size_needed, 0);
    MultiByteToWideChar(CP_UTF8, 0, &str[0], (int)str.size(), &wstrTo[0], size_needed);
    return wstrTo;
}

void Live2DPal::UpdateTime() {
    // timeGetTime は精度が低いため QueryPerformanceCounter が望ましいが、簡易実装として維持
    // ただし、初回呼び出し時の差分が大きくなるのを防ぐ
    float time = static_cast<float>(timeGetTime()) / 1000.0f;
    if (s_lastFrame == 0.0f) {
        s_lastFrame = time;
    }
    s_currentFrame = time;
    s_deltaTime = s_currentFrame - s_lastFrame;
    s_lastFrame = s_currentFrame;
}

float Live2DPal::GetDeltaTime() {
    return s_deltaTime;
}

float Live2DPal::GetAppTime() {
    return s_currentFrame;
}

} // namespace VTuberKitForNative
