#include "Live2DManager.h"
#include "Live2DPal.h"
#include "Live2DModel.h"
#include <CubismFramework.hpp>
#include <CubismDefaultParameterId.hpp>
#include <Rendering/D3D11/CubismRenderer_D3D11.hpp>
#include <d3d11.h>

using namespace System::Threading;
using namespace Live2D::Cubism::Framework;
using namespace Live2D::Cubism::Framework::DefaultParameterId;

// Global D3D11 Device pointers for NativeModel access
ID3D11Device* g_d3d11Device = nullptr;
ID3D11DeviceContext* g_d3d11Context = nullptr;

namespace VTuberKitForNative {

// s_instance definition removed from here because it's a managed static member, 
// which should be defined in the class or via property, but in CLI it's complicated.
// Actually, for managed types, static members are defined in metadata. 
// We don't need "Live2DManager^ Live2DManager::s_instance = nullptr;" in .cpp for managed types.
// It initializes to null automatically.

Live2DManager^ Live2DManager::GetInstance() {
    Monitor::Enter(s_lock);
    try {
        if (s_instance == nullptr) {
            s_instance = gcnew Live2DManager();
        }
        return s_instance;
    }
    finally {
        Monitor::Exit(s_lock);
    }
}

void Live2DManager::ReleaseInstance() {
    Monitor::Enter(s_lock);
    try {
        if (s_instance != nullptr) {
            s_instance->Release();
            s_instance = nullptr;
        }
    }
    finally {
        Monitor::Exit(s_lock);
    }
}

Live2DManager::Live2DManager()
    : _refCount(0)
    , _initialized(false)
    , _allocator(nullptr)
    , _option(nullptr) {
    _models = gcnew List<Live2DModelWrapper^>();
}

Live2DManager::~Live2DManager() {
    this->!Live2DManager();
}

Live2DManager::!Live2DManager() {
    // ファイナライザーではReleaseを呼ばない（GCタイミングで予期せぬ解放を防ぐ）
    // 代わりにデストラクタで明示的に解放されることを期待
}

// 修正: 確実にリスト内のモデルも管理から外す
void Live2DManager::ReleaseAllModels() {
    // リストをクリアするだけだと、Wrapper側でReleaseが呼ばれていない場合に
    // Live2DManagerが管理しているNativeリソースへのポインタが宙に浮く可能性がある
    // C#側で正しくDisposeされていれば問題ないが、念のため
    Monitor::Enter(s_lock);
    try {
        _models->Clear();
    }
    finally {
        Monitor::Exit(s_lock);
    }
}

// 修正: Initialize内のキャストを削除
bool Live2DManager::Initialize() {
    Monitor::Enter(s_lock);
    try {
        _refCount++;
        if (_initialized) return true;

        _allocator = new NativeCubismAllocator();
        _option = new CubismFramework::Option();

        _option->LogFunction = Live2DPal::PrintMessage;
        _option->LoggingLevel = CubismFramework::Option::LogLevel_Verbose;
        
        // SDKの型定義 (csmLoadFileFunction) に一致するよう関数ポインタを設定
        _option->LoadFileFunction = Live2DPal::LoadFileAsBytes;
        _option->ReleaseBytesFunction = Live2DPal::ReleaseBytes;

        CubismFramework::StartUp(static_cast<Csm::ICubismAllocator*>(_allocator), _option);
        CubismFramework::Initialize();

        _initialized = true;
        return true;
    }
    finally {
        Monitor::Exit(s_lock);
    }
}

void Live2DManager::Release() {
    Monitor::Enter(s_lock);
    try {
        if (!_initialized) {
            return;
        }

        _refCount--;

        if (_refCount > 0) {
            // まだ参照されている
            return;
        }

        // 最後の参照が解放された
        ReleaseAllModels();
        CubismFramework::Dispose();
        
        if (_allocator) {
            delete _allocator;
            _allocator = nullptr;
        }
        if (_option) {
            delete _option;
            _option = nullptr;
        }

        _initialized = false;
    }
    finally {
        Monitor::Exit(s_lock);
    }
}

bool Live2DManager::IsInitialized() {
    return _initialized;
}

void Live2DManager::SetD3D11Device(IntPtr device, IntPtr context) {
    g_d3d11Device = static_cast<ID3D11Device*>(device.ToPointer());
    g_d3d11Context = static_cast<ID3D11DeviceContext*>(context.ToPointer());

    // 追加: レンダラーの静的設定（モデルロード前に一度だけ必要）
    if (g_d3d11Device) {
        Live2D::Cubism::Framework::Rendering::CubismRenderer_D3D11::InitializeConstantSettings(1, g_d3d11Device);
    }
}

Live2DModelWrapper^ Live2DManager::CreateModel() {
    Live2DModelWrapper^ model = gcnew Live2DModelWrapper();
    _models->Add(model);
    return model;
}

void Live2DManager::ReleaseModel(Live2DModelWrapper^ model) {
    _models->Remove(model);
}

void Live2DManager::Update(float deltaTime) {
    Live2DPal::UpdateTime();
    
    for (int i = 0; i < _models->Count; ++i) {
        _models[i]->Update(deltaTime);
    }
}

// パラメータID定数
// 静的変数を関数内で定義してキャッシュする
#define CACHED_STRING_PROP(PropName, NativeId) \
    System::String^ Live2DManager::PropName::get() { \
        return gcnew System::String((const char*)NativeId); \
    }

CACHED_STRING_PROP(ParamAngleX, Live2D::Cubism::Framework::DefaultParameterId::ParamAngleX)
CACHED_STRING_PROP(ParamAngleY, Live2D::Cubism::Framework::DefaultParameterId::ParamAngleY)
CACHED_STRING_PROP(ParamAngleZ, Live2D::Cubism::Framework::DefaultParameterId::ParamAngleZ)
CACHED_STRING_PROP(ParamEyeLOpen, Live2D::Cubism::Framework::DefaultParameterId::ParamEyeLOpen)
CACHED_STRING_PROP(ParamEyeROpen, Live2D::Cubism::Framework::DefaultParameterId::ParamEyeROpen)
CACHED_STRING_PROP(ParamEyeLSmile, Live2D::Cubism::Framework::DefaultParameterId::ParamEyeLSmile)
CACHED_STRING_PROP(ParamEyeRSmile, Live2D::Cubism::Framework::DefaultParameterId::ParamEyeRSmile)
CACHED_STRING_PROP(ParamEyeBallX, Live2D::Cubism::Framework::DefaultParameterId::ParamEyeBallX)
CACHED_STRING_PROP(ParamEyeBallY, Live2D::Cubism::Framework::DefaultParameterId::ParamEyeBallY)
CACHED_STRING_PROP(ParamEyeBallForm, Live2D::Cubism::Framework::DefaultParameterId::ParamEyeBallForm)
CACHED_STRING_PROP(ParamBrowLY, Live2D::Cubism::Framework::DefaultParameterId::ParamBrowLY)
CACHED_STRING_PROP(ParamBrowRY, Live2D::Cubism::Framework::DefaultParameterId::ParamBrowRY)
CACHED_STRING_PROP(ParamBrowLX, Live2D::Cubism::Framework::DefaultParameterId::ParamBrowLX)
CACHED_STRING_PROP(ParamBrowRX, Live2D::Cubism::Framework::DefaultParameterId::ParamBrowRX)
CACHED_STRING_PROP(ParamBrowLAngle, Live2D::Cubism::Framework::DefaultParameterId::ParamBrowLAngle)
CACHED_STRING_PROP(ParamBrowRAngle, Live2D::Cubism::Framework::DefaultParameterId::ParamBrowRAngle)
CACHED_STRING_PROP(ParamBrowLForm, Live2D::Cubism::Framework::DefaultParameterId::ParamBrowLForm)
CACHED_STRING_PROP(ParamBrowRForm, Live2D::Cubism::Framework::DefaultParameterId::ParamBrowRForm)
CACHED_STRING_PROP(ParamMouthForm, Live2D::Cubism::Framework::DefaultParameterId::ParamMouthForm)
CACHED_STRING_PROP(ParamMouthOpenY, Live2D::Cubism::Framework::DefaultParameterId::ParamMouthOpenY)
CACHED_STRING_PROP(ParamCheek, Live2D::Cubism::Framework::DefaultParameterId::ParamCheek)
CACHED_STRING_PROP(ParamBodyAngleX, Live2D::Cubism::Framework::DefaultParameterId::ParamBodyAngleX)
CACHED_STRING_PROP(ParamBodyAngleY, Live2D::Cubism::Framework::DefaultParameterId::ParamBodyAngleY)
CACHED_STRING_PROP(ParamBodyAngleZ, Live2D::Cubism::Framework::DefaultParameterId::ParamBodyAngleZ)
CACHED_STRING_PROP(ParamBreath, Live2D::Cubism::Framework::DefaultParameterId::ParamBreath)
CACHED_STRING_PROP(ParamArmLA, Live2D::Cubism::Framework::DefaultParameterId::ParamArmLA)
CACHED_STRING_PROP(ParamArmRA, Live2D::Cubism::Framework::DefaultParameterId::ParamArmRA)
CACHED_STRING_PROP(ParamArmLB, Live2D::Cubism::Framework::DefaultParameterId::ParamArmLB)
CACHED_STRING_PROP(ParamArmRB, Live2D::Cubism::Framework::DefaultParameterId::ParamArmRB)
CACHED_STRING_PROP(ParamHandL, Live2D::Cubism::Framework::DefaultParameterId::ParamHandL)
CACHED_STRING_PROP(ParamHandR, Live2D::Cubism::Framework::DefaultParameterId::ParamHandR)
CACHED_STRING_PROP(ParamHairFront, Live2D::Cubism::Framework::DefaultParameterId::ParamHairFront)
CACHED_STRING_PROP(ParamHairSide, Live2D::Cubism::Framework::DefaultParameterId::ParamHairSide)
CACHED_STRING_PROP(ParamHairBack, Live2D::Cubism::Framework::DefaultParameterId::ParamHairBack)
CACHED_STRING_PROP(ParamHairFluffy, Live2D::Cubism::Framework::DefaultParameterId::ParamHairFluffy)
CACHED_STRING_PROP(ParamShoulderY, Live2D::Cubism::Framework::DefaultParameterId::ParamShoulderY)
CACHED_STRING_PROP(ParamBustX, Live2D::Cubism::Framework::DefaultParameterId::ParamBustX)
CACHED_STRING_PROP(ParamBustY, Live2D::Cubism::Framework::DefaultParameterId::ParamBustY)
CACHED_STRING_PROP(ParamBaseX, Live2D::Cubism::Framework::DefaultParameterId::ParamBaseX)
CACHED_STRING_PROP(ParamBaseY, Live2D::Cubism::Framework::DefaultParameterId::ParamBaseY)

} // namespace VTuberKitForNative
