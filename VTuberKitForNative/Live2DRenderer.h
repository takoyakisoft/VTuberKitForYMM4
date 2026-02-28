#pragma once

#include <d3d11.h>
#include <CubismFramework.hpp>
#include <Math/CubismMatrix44.hpp>
#include <Type/csmVector.hpp>

using namespace Csm;

namespace VTuberKitForNative {

// C++/CLIマネージドラッパー
public ref class Live2DRenderer {
public:
    Live2DRenderer();
    ~Live2DRenderer();
    !Live2DRenderer();

    // 初期化・解放
    bool Initialize(System::IntPtr d3d11Device, System::IntPtr d3d11Context);
    void Release();

    // フレーム制御
    void BeginFrame(int viewportWidth, int viewportHeight);
    void EndFrame();

    // デバイス取得
    System::IntPtr GetDevice();
    System::IntPtr GetDeviceContext();

    // レンダリング設定
    void SetClearColor(float r, float g, float b, float a);
    void SetViewport(int x, int y, int width, int height);

    // スクリーンサイズ基準の変換行列作成
    void CreateScreenBasedMatrix(float screenWidth, float screenHeight, 
                                  float modelX, float modelY, float modelScale,
                                  CubismMatrix44& outMatrix);

    // デバイスロスト対応
    void OnDeviceLost();
    void OnDeviceReset(System::IntPtr newDevice, System::IntPtr newContext);

private:
    ID3D11Device* _device;
    ID3D11DeviceContext* _context;
    ID3D11DepthStencilState* _depthStencilState;
    ID3D11SamplerState* _defaultSamplerState;
    bool _initialized;
    float _clearColorR;
    float _clearColorG;
    float _clearColorB;
    float _clearColorA;
    int _viewportWidth;
    int _viewportHeight;
};

} // namespace VTuberKitForNative
