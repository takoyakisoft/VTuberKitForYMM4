#include "Live2DRenderer.h"
#include "Live2DPal.h"
#include <Rendering/D3D11/CubismRenderer_D3D11.hpp>
#include <CubismFramework.hpp>
#include <Windows.h>
#include <string>

using namespace System;

namespace VTuberKitForNative {

namespace
{
    volatile LONG g_rendererInstanceCount = 0;
}

Live2DRenderer::Live2DRenderer() 
    : _initialized(false)
    , _viewportWidth(0)
    , _viewportHeight(0)
    , _device(nullptr)
    , _context(nullptr)
    , _depthStencilState(nullptr)
    , _defaultSamplerState(nullptr)
    , _clearColorR(0.0f)
    , _clearColorG(0.0f)
    , _clearColorB(0.0f)
    , _clearColorA(1.0f) {
}

Live2DRenderer::~Live2DRenderer() {
    this->!Live2DRenderer();
}

Live2DRenderer::!Live2DRenderer() {
    Release();
}

bool Live2DRenderer::Initialize(IntPtr d3d11Device, IntPtr d3d11Context) {
    if (_initialized) {
        return true;
    }

    _device = static_cast<ID3D11Device*>(d3d11Device.ToPointer());
    _context = static_cast<ID3D11DeviceContext*>(d3d11Context.ToPointer());

    if (!_device || !_context) {
        Live2DPal::PrintLogLn("Failed to initialize Live2DRenderer: Invalid D3D11 device or context");
        return false;
    }
    
    // 参照カウントをインクリメント（外部からもらったポインタを保持するため）
    _device->AddRef();
    _context->AddRef();

    const auto instanceCount = InterlockedIncrement(&g_rendererInstanceCount);

    // Cubism SDKのD3D11レンダラー設定は共有状態なので、初回のみ構築する。
    const csmUint32 bufferSetNum = 1;
    Rendering::CubismRenderer_D3D11::InitializeConstantSettings(bufferSetNum, _device);
    if (instanceCount == 1)
    {
        Rendering::CubismRenderer_D3D11::GenerateShader(_device);
    }
    
    // Live2D is 2D rendering; keep depth test disabled to avoid state-dependent full cull.
    D3D11_DEPTH_STENCIL_DESC dsDesc = {};
    dsDesc.DepthEnable = FALSE;
    dsDesc.DepthWriteMask = D3D11_DEPTH_WRITE_MASK_ZERO;
    dsDesc.DepthFunc = D3D11_COMPARISON_ALWAYS;
    dsDesc.StencilEnable = FALSE;
    
    ID3D11DepthStencilState* tempState = nullptr;
    HRESULT hr = _device->CreateDepthStencilState(&dsDesc, &tempState);
    if (SUCCEEDED(hr)) {
        _depthStencilState = tempState;
    } else {
        Live2DPal::PrintLogLn("Failed to create depth stencil state: 0x%08X", hr);
        // 深度ステンシル状態がなくても続行
    }

    D3D11_SAMPLER_DESC samplerDesc = {};
    samplerDesc.Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR;
    samplerDesc.AddressU = D3D11_TEXTURE_ADDRESS_CLAMP;
    samplerDesc.AddressV = D3D11_TEXTURE_ADDRESS_CLAMP;
    samplerDesc.AddressW = D3D11_TEXTURE_ADDRESS_CLAMP;
    samplerDesc.MipLODBias = 0.0f;
    samplerDesc.MaxAnisotropy = 1;
    samplerDesc.ComparisonFunc = D3D11_COMPARISON_NEVER;
    samplerDesc.BorderColor[0] = 0.0f;
    samplerDesc.BorderColor[1] = 0.0f;
    samplerDesc.BorderColor[2] = 0.0f;
    samplerDesc.BorderColor[3] = 0.0f;
    samplerDesc.MinLOD = 0.0f;
    samplerDesc.MaxLOD = D3D11_FLOAT32_MAX;

    ID3D11SamplerState* tempSampler = nullptr;
    hr = _device->CreateSamplerState(&samplerDesc, &tempSampler);
    if (SUCCEEDED(hr)) {
        _defaultSamplerState = tempSampler;
    } else {
        Live2DPal::PrintLogLn("Failed to create default sampler state: 0x%08X", hr);
    }

    _initialized = true;
    return true;
}

void Live2DRenderer::Release() {
    if (_initialized) {
        const auto instanceCount = InterlockedDecrement(&g_rendererInstanceCount);
        if (instanceCount <= 0) {
            Rendering::CubismRenderer_D3D11::DeleteShaderManager();
            Rendering::CubismRenderer_D3D11::DeleteRenderStateManager();
            g_rendererInstanceCount = 0;
        }

        if (_depthStencilState) {
            _depthStencilState->Release();
            _depthStencilState = nullptr;
        }

        if (_defaultSamplerState) {
            _defaultSamplerState->Release();
            _defaultSamplerState = nullptr;
        }
        
        if (_context) {
            _context->Release();
            _context = nullptr;
        }
        if (_device) {
            _device->Release();
            _device = nullptr;
        }
        
        _initialized = false;
    }
}

void Live2DRenderer::BeginFrame(int viewportWidth, int viewportHeight) {
    if (!_initialized || !_device || !_context) {
        return;
    }

    _viewportWidth = viewportWidth;
    _viewportHeight = viewportHeight;

    // 深度ステンシル状態を設定
    if (_depthStencilState) {
        _context->OMSetDepthStencilState(_depthStencilState, 0);
    }

    ID3D11SamplerState* currentSampler = nullptr;
    _context->PSGetSamplers(0, 1, &currentSampler);
    if (!currentSampler) {
        if (_defaultSamplerState) {
            ID3D11SamplerState* samplers[1] = { _defaultSamplerState };
            _context->PSSetSamplers(0, 1, samplers);
        }
    } else {
        currentSampler->Release();
    }

    // Cubism SDKのフレーム開始処理
    Rendering::CubismRenderer_D3D11::StartFrame(
        _device, 
        _context, 
        static_cast<csmUint32>(viewportWidth), 
        static_cast<csmUint32>(viewportHeight)
    );
}

void Live2DRenderer::EndFrame() {
    if (!_initialized || !_device) {
        return;
    }

    // Cubism SDKのフレーム終了処理
    Rendering::CubismRenderer_D3D11::EndFrame(_device);
}

IntPtr Live2DRenderer::GetDevice() {
    return IntPtr(_device);
}

IntPtr Live2DRenderer::GetDeviceContext() {
    return IntPtr(_context);
}

void Live2DRenderer::SetClearColor(float r, float g, float b, float a) {
    _clearColorR = r;
    _clearColorG = g;
    _clearColorB = b;
    _clearColorA = a;
}

void Live2DRenderer::SetViewport(int x, int y, int width, int height) {
    if (_context) {
        D3D11_VIEWPORT viewport;
        viewport.TopLeftX = static_cast<float>(x);
        viewport.TopLeftY = static_cast<float>(y);
        viewport.Width = static_cast<float>(width);
        viewport.Height = static_cast<float>(height);
        viewport.MinDepth = 0.0f;
        viewport.MaxDepth = 1.0f;
        _context->RSSetViewports(1, &viewport);
    }
}

void Live2DRenderer::CreateScreenBasedMatrix(float screenWidth, float screenHeight,
                                              float modelX, float modelY, float modelScale,
                                              CubismMatrix44& outMatrix) {
    // スクリーンサイズ基準で変換行列を作成
    outMatrix.LoadIdentity();

    // アスペクト比計算
    float aspect = screenWidth / screenHeight;
    
    // スクリーンサイズに合わせてスケーリング
    // モデルのキャンバスサイズが1.0を超える場合は縦長ウィンドウ向けの調整
    if (aspect < 1.0f) {
        outMatrix.Scale(1.0f, aspect);
    } else {
        outMatrix.Scale(1.0f / aspect, 1.0f);
    }

    // モデルの位置とスケールを適用
    outMatrix.Scale(modelScale, modelScale);
    outMatrix.Translate(modelX, modelY);
}

void Live2DRenderer::OnDeviceLost() {
    if (_initialized) {
        Rendering::CubismRenderer_D3D11::OnDeviceLost();
    }
}

void Live2DRenderer::OnDeviceReset(IntPtr newDevice, IntPtr newContext) {
    Release();
    Initialize(newDevice, newContext);
}

} // namespace VTuberKitForNative
