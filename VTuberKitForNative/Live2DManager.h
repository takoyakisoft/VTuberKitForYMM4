#pragma once

#include "Live2DModel.h"
#include <CubismFramework.hpp>
#include <Type/csmVector.hpp>

using namespace Csm;
using namespace System;
using namespace System::Collections::Generic;

namespace VTuberKitForNative {

class NativeCubismAllocator : public Csm::ICubismAllocator {
public:
    void* Allocate(const csmSizeType size) override {
        return malloc(size);
    }

    void Deallocate(void* memory) override {
        free(memory);
    }

    void* AllocateAligned(const csmSizeType size, const csmUint32 alignment) override {
        size_t offset, shift, alignedAddress;
        void* allocation;
        void** preamble;

        offset = alignment - 1 + sizeof(void*);
        allocation = Allocate(size + static_cast<csmUint32>(offset));
        alignedAddress = reinterpret_cast<size_t>(allocation) + sizeof(void*);
        shift = alignedAddress % alignment;

        preamble = reinterpret_cast<void**>(alignedAddress + (alignment - shift));
        --preamble;
        *preamble = allocation;

        return reinterpret_cast<void*>(alignedAddress + (alignment - shift));
    }

    void DeallocateAligned(void* alignedMemory) override {
        void** preamble;
        preamble = reinterpret_cast<void**>(alignedMemory);
        --preamble;
        Deallocate(*preamble);
    }
};

// Live2D SDK マネージャー
public ref class Live2DManager {
public:
    // シングルトン取得
    static Live2DManager^ GetInstance();
    static void ReleaseInstance();

    // 初期化・解放
    bool Initialize();
    void Release();
    bool IsInitialized();

    // デバイス設定（D3D11用）
    void SetD3D11Device(IntPtr device, IntPtr context);

    // モデル管理
    Live2DModelWrapper^ CreateModel();
    void ReleaseModel(Live2DModelWrapper^ model);
    void ReleaseAllModels();

    // 更新
    void Update(float deltaTime);

    // パラメータID定数
    static property System::String^ ParamAngleX { System::String^ get(); }
    static property System::String^ ParamAngleY { System::String^ get(); }
    static property System::String^ ParamAngleZ { System::String^ get(); }
    static property System::String^ ParamEyeLOpen { System::String^ get(); }
    static property System::String^ ParamEyeROpen { System::String^ get(); }
    static property System::String^ ParamEyeLSmile { System::String^ get(); }
    static property System::String^ ParamEyeRSmile { System::String^ get(); }
    static property System::String^ ParamEyeBallX { System::String^ get(); }
    static property System::String^ ParamEyeBallY { System::String^ get(); }
    static property System::String^ ParamEyeBallForm { System::String^ get(); }
    static property System::String^ ParamBrowLY { System::String^ get(); }
    static property System::String^ ParamBrowRY { System::String^ get(); }
    static property System::String^ ParamBrowLX { System::String^ get(); }
    static property System::String^ ParamBrowRX { System::String^ get(); }
    static property System::String^ ParamBrowLAngle { System::String^ get(); }
    static property System::String^ ParamBrowRAngle { System::String^ get(); }
    static property System::String^ ParamBrowLForm { System::String^ get(); }
    static property System::String^ ParamBrowRForm { System::String^ get(); }
    static property System::String^ ParamMouthForm { System::String^ get(); }
    static property System::String^ ParamMouthOpenY { System::String^ get(); }
    static property System::String^ ParamCheek { System::String^ get(); }
    static property System::String^ ParamBodyAngleX { System::String^ get(); }
    static property System::String^ ParamBodyAngleY { System::String^ get(); }
    static property System::String^ ParamBodyAngleZ { System::String^ get(); }
    static property System::String^ ParamBreath { System::String^ get(); }
    static property System::String^ ParamArmLA { System::String^ get(); }
    static property System::String^ ParamArmRA { System::String^ get(); }
    static property System::String^ ParamArmLB { System::String^ get(); }
    static property System::String^ ParamArmRB { System::String^ get(); }
    static property System::String^ ParamHandL { System::String^ get(); }
    static property System::String^ ParamHandR { System::String^ get(); }
    static property System::String^ ParamHairFront { System::String^ get(); }
    static property System::String^ ParamHairSide { System::String^ get(); }
    static property System::String^ ParamHairBack { System::String^ get(); }
    static property System::String^ ParamHairFluffy { System::String^ get(); }
    static property System::String^ ParamShoulderY { System::String^ get(); }
    static property System::String^ ParamBustX { System::String^ get(); }
    static property System::String^ ParamBustY { System::String^ get(); }
    static property System::String^ ParamBaseX { System::String^ get(); }
    static property System::String^ ParamBaseY { System::String^ get(); }

private:
    Live2DManager();
    ~Live2DManager();
    !Live2DManager();

    static Live2DManager^ s_instance;
    static Object^ s_lock = gcnew Object();
    int _refCount;
    bool _initialized;
    List<Live2DModelWrapper^>^ _models;

    NativeCubismAllocator* _allocator;
    CubismFramework::Option* _option;
};

} // namespace VTuberKitForNative
