#include "Live2DModel_Internal.h"
#include "Live2DPal.h"
#include <Rendering/D3D11/CubismRenderer_D3D11.hpp>
#include <CubismModelSettingJson.hpp>
#include <CubismDefaultParameterId.hpp>
#include <Id/CubismIdManager.hpp>
#include <d3d11.h>
#include <Shlwapi.h>
#include <map>
#include <vector>
#include <wincodec.h>
#include <wrl/client.h>
#include <Motion/CubismMotionQueueEntry.hpp>
#include <cmath>
#include <cwchar>
#include <mutex>
#include <algorithm>

#pragma comment(lib, "Windowscodecs.lib")
#pragma comment(lib, "Shlwapi.lib")

#pragma unmanaged

using namespace Live2D::Cubism::Framework::Rendering;
using namespace Live2D::Cubism::Framework::DefaultParameterId;

// Defined in Live2DManager.cpp
extern ID3D11Device* g_d3d11Device;
extern ID3D11DeviceContext* g_d3d11Context;
extern std::mutex g_d3d11Mutex;

namespace VTuberKitForNative {

namespace
{
    std::mutex g_nativeDrawMutex;

    std::string WideToUtf8(const std::wstring& value)
    {
        if (value.empty())
        {
            return std::string();
        }

        const int sizeNeeded = WideCharToMultiByte(CP_UTF8, 0, value.c_str(), -1, nullptr, 0, nullptr, nullptr);
        if (sizeNeeded <= 1)
        {
            return std::string();
        }

        std::string utf8(sizeNeeded, '\0');
        WideCharToMultiByte(CP_UTF8, 0, value.c_str(), -1, &utf8[0], sizeNeeded, nullptr, nullptr);
        utf8.resize(sizeNeeded - 1);
        return utf8;
    }

    std::wstring NormalizeAbsolutePath(const std::wstring& path)
    {
        if (path.empty())
        {
            return std::wstring();
        }

        const DWORD requiredLength = GetFullPathNameW(path.c_str(), 0, nullptr, nullptr);
        if (requiredLength == 0)
        {
            return std::wstring();
        }

        std::wstring normalized(requiredLength, L'\0');
        const DWORD writtenLength = GetFullPathNameW(path.c_str(), requiredLength, &normalized[0], nullptr);
        if (writtenLength == 0)
        {
            return std::wstring();
        }

        normalized.resize(wcslen(normalized.c_str()));
        return normalized;
    }

    bool TryResolveModelAssetPath(const csmString& modelHomeDir, const char* relativePath, std::string& resolvedPath, std::string& errorMessage)
    {
        if (!relativePath || relativePath[0] == '\0')
        {
            errorMessage = "asset path is empty";
            return false;
        }

        try
        {
            std::wstring baseDir = NormalizeAbsolutePath(Live2DPal::StringToWString(modelHomeDir.GetRawString()));
            if (baseDir.empty())
            {
                errorMessage = std::string("failed to normalize model directory: ") + modelHomeDir.GetRawString();
                return false;
            }

            const std::wstring assetRelativePath = Live2DPal::StringToWString(relativePath);
            if (assetRelativePath.empty())
            {
                errorMessage = std::string("asset path conversion failed: ") + relativePath;
                return false;
            }

            if (!PathIsRelativeW(assetRelativePath.c_str()))
            {
                errorMessage = std::string("absolute asset path is not allowed: ") + relativePath;
                return false;
            }

            if (!baseDir.empty() && baseDir.back() != L'\\' && baseDir.back() != L'/')
            {
                baseDir += L'\\';
            }

            const std::wstring combinedPath = baseDir + assetRelativePath;
            const std::wstring candidate = NormalizeAbsolutePath(combinedPath);
            if (candidate.empty())
            {
                errorMessage = std::string("failed to normalize asset path: ") + relativePath;
                return false;
            }

            if (_wcsnicmp(candidate.c_str(), baseDir.c_str(), baseDir.size()) != 0)
            {
                errorMessage = std::string("asset path escapes model directory: ") + relativePath;
                return false;
            }

            resolvedPath = WideToUtf8(candidate);
            if (resolvedPath.empty())
            {
                errorMessage = std::string("failed to normalize asset path: ") + relativePath;
                return false;
            }

            return true;
        }
        catch (const std::exception& ex)
        {
            errorMessage = std::string("asset path validation failed for '") + relativePath + "': " + ex.what();
            return false;
        }
    }

    bool TryDrawModelWithSehGuard(CubismRenderer_D3D11* renderer)
    {
        __try
        {
            renderer->DrawModel();
            return true;
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            Live2DPal::PrintLogLn("Native Draw: DrawModel aborted by SEH. code=0x%08X", GetExceptionCode());
            return false;
        }
    }
}

NativeModel::NativeModel()
    : _modelSetting(nullptr)
    , _lastMotionPriority(0.0f) {
}

void NativeModel::AddLoadWarning(const std::string& warning) {
    if (warning.empty()) {
        return;
    }

    if (std::find(_loadWarnings.begin(), _loadWarnings.end(), warning) == _loadWarnings.end()) {
        _loadWarnings.push_back(warning);
    }
}

void NativeModel::InitializeBlinkAndBreath() {
    if (_eyeBlink) {
        CubismEyeBlink::Delete(_eyeBlink);
        _eyeBlink = nullptr;
    }
    if (_modelSetting && _modelSetting->GetEyeBlinkParameterCount() > 0) {
        _eyeBlink = CubismEyeBlink::Create(_modelSetting);
    }

    if (_breath) {
        CubismBreath::Delete(_breath);
        _breath = nullptr;
    }
    _breath = CubismBreath::Create();
    if (_breath) {
        csmVector<CubismBreath::BreathParameterData> breathParameters;
        breathParameters.PushBack(CubismBreath::BreathParameterData(CubismFramework::GetIdManager()->GetId(ParamAngleX), 0.0f, 15.0f, 6.5345f, 0.5f));
        breathParameters.PushBack(CubismBreath::BreathParameterData(CubismFramework::GetIdManager()->GetId(ParamAngleY), 0.0f, 8.0f, 3.5345f, 0.5f));
        breathParameters.PushBack(CubismBreath::BreathParameterData(CubismFramework::GetIdManager()->GetId(ParamAngleZ), 0.0f, 10.0f, 5.5345f, 0.5f));
        breathParameters.PushBack(CubismBreath::BreathParameterData(CubismFramework::GetIdManager()->GetId(ParamBodyAngleX), 0.0f, 4.0f, 15.5345f, 0.5f));
        breathParameters.PushBack(CubismBreath::BreathParameterData(CubismFramework::GetIdManager()->GetId(ParamBreath), 0.5f, 0.5f, 3.2345f, 0.5f));
        _breath->SetParameters(breathParameters);
    }
}

NativeModel::~NativeModel() {
    ReleaseTextures();
    StopAllMotions();

    for (auto& group : _motions) {
        for (auto& motion : group.second) {
            ACubismMotion::Delete(motion.second);
        }
    }
    _motions.clear();

    for (auto& expression : _expressions) {
        ACubismMotion::Delete(expression.second);
    }
    _expressions.clear();

    if (_eyeBlink) {
        CubismEyeBlink::Delete(_eyeBlink);
        _eyeBlink = nullptr;
    }

    if (_breath) {
        CubismBreath::Delete(_breath);
        _breath = nullptr;
    }

    DeleteRenderer();

    if (_modelSetting) {
        delete _modelSetting;
        _modelSetting = nullptr;
    }
}

void NativeModel::ReleaseTextures() {
    for (auto view : _textureViews) {
        if (view) {
            view->Release();
        }
    }
    _textureViews.clear();
}

bool NativeModel::LoadAssets(const char* dir, const char* fileName) {
    _lastErrorMessage.clear();
    _loadWarnings.clear();

    if (_modelSetting) {
        delete _modelSetting;
        _modelSetting = nullptr;
    }

    _modelHomeDir = dir;

    const csmString path = csmString(dir) + fileName;
    unsigned int rawSize = 0;
    csmByte* buffer = Live2DPal::LoadFileAsBytes(path.GetRawString(), &rawSize);

    if (!buffer) {
        Live2DPal::PrintLogLn("Failed to load model setting file: %s", path.GetRawString());
        _lastErrorMessage = std::string("モデル設定ファイルの読み込みに失敗しました: ") + path.GetRawString();
        return false;
    }

    ICubismModelSetting* setting = new CubismModelSettingJson(buffer, static_cast<csmSizeInt>(rawSize));
    Live2DPal::ReleaseBytes(buffer);

    return SetupModel(setting);
}

bool NativeModel::SetupModel(ICubismModelSetting* setting) {
    _updating = true;
    _initialized = false;
    _modelSetting = setting;
    _loadWarnings.clear();

    if (strcmp(setting->GetModelFileName(), "") != 0) {
        std::string path;
        std::string pathError;
        if (!TryResolveModelAssetPath(_modelHomeDir, setting->GetModelFileName(), path, pathError)) {
            _lastErrorMessage = std::string("モデル本体(moc3)のパスが不正です: ") + pathError;
            Live2DPal::PrintLogLn("%s", _lastErrorMessage.c_str());
            _updating = false;
            _initialized = false;
            return false;
        }
        unsigned int rawSize = 0;
        csmByte* buffer = Live2DPal::LoadFileAsBytes(path, &rawSize);
        if (buffer) {
            LoadModel(buffer, static_cast<csmSizeType>(rawSize));
            Live2DPal::ReleaseBytes(buffer);

            if (!_model) {
                _lastErrorMessage = std::string("モデル本体(moc3)の読み込みに失敗しました: ") + path
                    + "。moc3のバージョンとCubism Coreの互換性を確認してください。";
                Live2DPal::PrintLogLn("%s", _lastErrorMessage.c_str());
                _updating = false;
                _initialized = false;
                return false;
            }
        } else {
            Live2DPal::PrintLogLn("Failed to load model moc3: %s", path.c_str());
            _lastErrorMessage = std::string("モデル本体(moc3)ファイルが見つかりません: ") + path;
            _updating = false;
            _initialized = false;
            return false;
        }
    }

    for (auto& expression : _expressions) {
        ACubismMotion::Delete(expression.second);
    }
    _expressions.clear();

    if (setting->GetExpressionCount() > 0) {
        for (csmInt32 i = 0; i < setting->GetExpressionCount(); ++i) {
            csmString expressionName = setting->GetExpressionName(i);
            std::string expressionPath;
            std::string pathError;
            if (!TryResolveModelAssetPath(_modelHomeDir, setting->GetExpressionFileName(i), expressionPath, pathError)) {
                Live2DPal::PrintLogLn("Skipped expression '%s': %s", expressionName.GetRawString(), pathError.c_str());
                AddLoadWarning(std::string("Expression '") + expressionName.GetRawString() + "' の参照解決に失敗しました: " + pathError);
                continue;
            }
            unsigned int rawSize = 0;
            csmByte* buffer = Live2DPal::LoadFileAsBytes(expressionPath, &rawSize);
            if (!buffer) {
                AddLoadWarning(std::string("Expression '") + expressionName.GetRawString() + "' のファイルを読み込めません: " + expressionPath);
                continue;
            }

            ACubismMotion* expressionMotion = LoadExpression(buffer, static_cast<csmSizeInt>(rawSize), expressionName.GetRawString());
            Live2DPal::ReleaseBytes(buffer);
            if (expressionMotion) {
                _expressions[expressionName.GetRawString()] = expressionMotion;
            } else {
                AddLoadWarning(std::string("Expression '") + expressionName.GetRawString() + "' を生成できません: " + expressionPath);
            }
        }
    }

    if (strcmp(setting->GetPhysicsFileName(), "") != 0) {
        std::string path;
        std::string pathError;
        if (!TryResolveModelAssetPath(_modelHomeDir, setting->GetPhysicsFileName(), path, pathError)) {
            Live2DPal::PrintLogLn("Skipped physics file: %s", pathError.c_str());
            AddLoadWarning(std::string("Physics の参照解決に失敗しました: ") + pathError);
        } else {
        unsigned int rawSize = 0;
            csmByte* buffer = Live2DPal::LoadFileAsBytes(path, &rawSize);
            if (buffer) {
                LoadPhysics(buffer, static_cast<csmSizeInt>(rawSize));
                Live2DPal::ReleaseBytes(buffer);
            } else {
                AddLoadWarning(std::string("Physics ファイルを読み込めません: ") + path);
            }
        }
    }

    if (strcmp(setting->GetPoseFileName(), "") != 0) {
        std::string path;
        std::string pathError;
        if (!TryResolveModelAssetPath(_modelHomeDir, setting->GetPoseFileName(), path, pathError)) {
            Live2DPal::PrintLogLn("Skipped pose file: %s", pathError.c_str());
            AddLoadWarning(std::string("Pose の参照解決に失敗しました: ") + pathError);
        } else {
        unsigned int rawSize = 0;
            csmByte* buffer = Live2DPal::LoadFileAsBytes(path, &rawSize);
            if (buffer) {
                LoadPose(buffer, static_cast<csmSizeInt>(rawSize));
                Live2DPal::ReleaseBytes(buffer);
            } else {
                AddLoadWarning(std::string("Pose ファイルを読み込めません: ") + path);
            }
        }
    }

    if (strcmp(setting->GetUserDataFile(), "") != 0) {
        std::string path;
        std::string pathError;
        if (!TryResolveModelAssetPath(_modelHomeDir, setting->GetUserDataFile(), path, pathError)) {
            Live2DPal::PrintLogLn("Skipped user data file: %s", pathError.c_str());
            AddLoadWarning(std::string("UserData の参照解決に失敗しました: ") + pathError);
        } else {
        unsigned int rawSize = 0;
            csmByte* buffer = Live2DPal::LoadFileAsBytes(path, &rawSize);
            if (buffer) {
                LoadUserData(buffer, static_cast<csmSizeInt>(rawSize));
                Live2DPal::ReleaseBytes(buffer);
            } else {
                AddLoadWarning(std::string("UserData ファイルを読み込めません: ") + path);
            }
        }
    }

    _eyeBlinkIds.Clear();
    _lipSyncIds.Clear();
    for (csmInt32 i = 0; i < setting->GetEyeBlinkParameterCount(); ++i) {
        _eyeBlinkIds.PushBack(setting->GetEyeBlinkParameterId(i));
    }
    for (csmInt32 i = 0; i < setting->GetLipSyncParameterCount(); ++i) {
        _lipSyncIds.PushBack(setting->GetLipSyncParameterId(i));
    }

    InitializeBlinkAndBreath();

    csmMap<csmString, csmFloat32> layout;
    setting->GetLayoutMap(layout);
    if (_modelMatrix) {
        _modelMatrix->SetupFromLayout(layout);
    }

    if (_model) {
        _model->SaveParameters();
    }

    LoadMotions();
    ValidateHitAreaBindings();
    if (_motionManager) {
        _motionManager->StopAllMotions();
    }

    if (!_model)
    {
        _lastErrorMessage = "モデルの初期化に失敗したためレンダラーを作成できません。";
        _updating = false;
        _initialized = false;
        return false;
    }

    CreateRenderer();
    SetupTextures();

    _updating = false;
    _initialized = true;
    _lastErrorMessage.clear();
    return true;
}

void NativeModel::SetupTextures() {
    Microsoft::WRL::ComPtr<ID3D11Device> device;
    Microsoft::WRL::ComPtr<ID3D11DeviceContext> context;
    {
        std::lock_guard<std::mutex> deviceLock(g_d3d11Mutex);
        if (!g_d3d11Device || !g_d3d11Context) {
            Live2DPal::PrintLogLn("D3D11 Device not initialized. Skipping texture load.");
            return;
        }

        device = g_d3d11Device;
        context = g_d3d11Context;
    }

    if (!device || !context) {
        Live2DPal::PrintLogLn("D3D11 Device not initialized. Skipping texture load.");
        return;
    }

    const HRESULT coInitHr = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    const bool shouldUninitializeCom = SUCCEEDED(coInitHr);
    if (FAILED(coInitHr) && coInitHr != RPC_E_CHANGED_MODE)
    {
        Live2DPal::PrintLogLn("SetupTextures: CoInitializeEx failed: 0x%08X", coInitHr);
        return;
    }

    ReleaseTextures();

    Microsoft::WRL::ComPtr<IWICImagingFactory> wicFactory;
    HRESULT hr = CoCreateInstance(CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&wicFactory));
    if (FAILED(hr)) {
        Live2DPal::PrintLogLn("Failed to create WIC Imaging Factory: 0x%08X", hr);
        return;
    }

    _textureWidth = 0;
    _textureHeight = 0;

    csmInt32 textureCount = _modelSetting->GetTextureCount();
    for (csmInt32 i = 0; i < textureCount; i++) {
        const csmChar* fileName = _modelSetting->GetTextureFileName(i);
        std::string path;
        std::string pathError;
        if (!TryResolveModelAssetPath(_modelHomeDir, fileName, path, pathError)) {
            Live2DPal::PrintLogLn("Skipped texture %d: %s", i, pathError.c_str());
            continue;
        }
        std::wstring widePath = Live2DPal::StringToWString(path);

        Microsoft::WRL::ComPtr<IWICBitmapDecoder> decoder;
        hr = wicFactory->CreateDecoderFromFilename(widePath.c_str(), nullptr, GENERIC_READ, WICDecodeMetadataCacheOnDemand, &decoder);
        if (FAILED(hr)) {
            Live2DPal::PrintLogLn("Failed to create decoder for: %s, 0x%08X", path.c_str(), hr);
            continue;
        }

        Microsoft::WRL::ComPtr<IWICBitmapFrameDecode> frame;
        hr = decoder->GetFrame(0, &frame);
        if (FAILED(hr)) continue;

        Microsoft::WRL::ComPtr<IWICFormatConverter> converter;
        hr = wicFactory->CreateFormatConverter(&converter);
        if (FAILED(hr)) continue;

        hr = converter->Initialize(frame.Get(), GUID_WICPixelFormat32bppBGRA, WICBitmapDitherTypeNone, nullptr, 0.0f, WICBitmapPaletteTypeCustom);
        if (FAILED(hr)) continue;

        UINT width = 0;
        UINT height = 0;
        converter->GetSize(&width, &height);

        std::vector<uint8_t> buffer(width * height * 4);
        hr = converter->CopyPixels(nullptr, width * 4, static_cast<UINT>(buffer.size()), buffer.data());
        if (FAILED(hr)) continue;

        D3D11_TEXTURE2D_DESC desc = {};
        desc.Width = width;
        desc.Height = height;
        desc.MipLevels = 0;
        desc.ArraySize = 1;
        desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
        desc.SampleDesc.Count = 1;
        desc.Usage = D3D11_USAGE_DEFAULT;
        desc.BindFlags = D3D11_BIND_SHADER_RESOURCE | D3D11_BIND_RENDER_TARGET;
        desc.MiscFlags = D3D11_RESOURCE_MISC_GENERATE_MIPS;

        Microsoft::WRL::ComPtr<ID3D11Texture2D> tex2D;
        hr = device->CreateTexture2D(&desc, nullptr, &tex2D);
        if (SUCCEEDED(hr)) {
            ID3D11ShaderResourceView* textureView = nullptr;
            D3D11_SHADER_RESOURCE_VIEW_DESC srvDesc = {};
            srvDesc.Format = desc.Format;
            srvDesc.ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2D;
            srvDesc.Texture2D.MostDetailedMip = 0;
            srvDesc.Texture2D.MipLevels = static_cast<UINT>(-1);

            hr = device->CreateShaderResourceView(tex2D.Get(), &srvDesc, &textureView);
            if (SUCCEEDED(hr) && textureView) {
                context->UpdateSubresource(tex2D.Get(), 0, nullptr, buffer.data(), width * 4, 0);
                context->GenerateMips(textureView);

                CubismRenderer_D3D11* renderer = GetRenderer<CubismRenderer_D3D11>();
                if (renderer) {
                    renderer->BindTexture(i, textureView);
                    renderer->IsPremultipliedAlpha(false);
                    // Keep render state deterministic across fresh instances.
                    renderer->IsCulling(false);
                    renderer->SetModelColor(1.0f, 1.0f, 1.0f, 1.0f);
                }
                _textureViews.push_back(textureView);

                if (_textureWidth == 0 && _textureHeight == 0) {
                    _textureWidth = static_cast<int>(width);
                    _textureHeight = static_cast<int>(height);
                }
            }
        } else {
            Live2DPal::PrintLogLn("Failed to create D3D11 texture: 0x%08X", hr);
        }
    }

    CubismRenderer_D3D11* renderer = GetRenderer<CubismRenderer_D3D11>();
    if (renderer) {
        renderer->IsCulling(false);
        renderer->SetModelColor(1.0f, 1.0f, 1.0f, 1.0f);
        renderer->UseHighPrecisionMask(true);
    }

    if (shouldUninitializeCom)
    {
        CoUninitialize();
    }
}

void NativeModel::ReloadRenderer() {
    DeleteRenderer();
    CreateRenderer();
    SetupTextures();
}

void NativeModel::ResetAnimationState() {
    if (!_model) {
        return;
    }

    StopAllMotions();

    if (_expressionManager) {
        _expressionManager->StopAllMotions();
    }

    if (_physics) {
        _physics->Reset();
        const auto& options = _physics->GetOptions();
        _defaultPhysicsGravity = options.Gravity;
        _defaultPhysicsWind = options.Wind;
    }

    if (_pose) {
        _pose->Reset(_model);
    }

    InitializeBlinkAndBreath();

    _lipSyncValue = 0.0f;
    _dragX = 0.0f;
    _dragY = 0.0f;

    _model->LoadParameters();
    _model->Update();
    _model->SaveParameters();
}

void NativeModel::LoadMotions() {
    if (!_modelSetting) return;

    for (auto& group : _motions) {
        for (auto& motion : group.second) {
            ACubismMotion::Delete(motion.second);
        }
    }
    _motions.clear();

    _lastMotionPriority = 0;

    csmInt32 motionGroupCount = _modelSetting->GetMotionGroupCount();

    for (csmInt32 i = 0; i < motionGroupCount; i++) {
        const csmChar* groupName = _modelSetting->GetMotionGroupName(i);
        csmInt32 motionCount = _modelSetting->GetMotionCount(groupName);

        for (csmInt32 j = 0; j < motionCount; j++) {
            csmString fileName = _modelSetting->GetMotionFileName(groupName, j);
            if (strcmp(fileName.GetRawString(), "") == 0) {
                continue;
            }

            std::string path;
            std::string pathError;
            if (!TryResolveModelAssetPath(_modelHomeDir, fileName.GetRawString(), path, pathError)) {
                Live2DPal::PrintLogLn("Skipped motion '%s'[%d]: %s", groupName, j, pathError.c_str());
                AddLoadWarning(std::string("Motion '") + groupName + "[" + std::to_string(j) + "]' の参照解決に失敗しました: " + pathError);
                continue;
            }
            unsigned int rawSize = 0;
            csmByte* buffer = Live2DPal::LoadFileAsBytes(path, &rawSize);

            if (buffer) {
                ACubismMotion* motion = LoadMotion(
                    buffer,
                    static_cast<csmSizeInt>(rawSize),
                    nullptr,
                    nullptr,
                    nullptr,
                    _modelSetting,
                    groupName,
                    j);
                Live2DPal::ReleaseBytes(buffer);

                if (motion) {
                    CubismMotion* cubismMotion = static_cast<CubismMotion*>(motion);
                    if (cubismMotion) {
                        cubismMotion->SetEffectIds(_eyeBlinkIds, _lipSyncIds);
                    }
                    _motions[groupName][j] = motion;
                } else {
                    Live2DPal::PrintLogLn("Failed to create motion: %s", path.c_str());
                    AddLoadWarning(std::string("Motion '") + groupName + "[" + std::to_string(j) + "]' を生成できません: " + path);
                }
            } else {
                Live2DPal::PrintLogLn("Failed to load motion file: %s", path.c_str());
                AddLoadWarning(std::string("Motion '") + groupName + "[" + std::to_string(j) + "]' のファイルを読み込めません: " + path);
            }
        }
    }
}

void NativeModel::ValidateHitAreaBindings() {
    if (!_modelSetting || !_model) {
        return;
    }

    const csmInt32 count = _modelSetting->GetHitAreasCount();
    for (csmInt32 i = 0; i < count; i++) {
        const char* currentName = _modelSetting->GetHitAreaName(i);
        const auto* hitAreaId = _modelSetting->GetHitAreaId(i);
        const char* currentId = hitAreaId ? hitAreaId->GetString().GetRawString() : nullptr;
        const char* lookupId = (currentId && currentId[0] != '\0') ? currentId : currentName;
        float centerX = 0.0f;
        float centerY = 0.0f;
        float width = 0.0f;
        float height = 0.0f;
        if (!lookupId || !TryGetHitAreaBounds(lookupId, centerX, centerY, width, height)) {
            const std::string label =
                (currentName && currentName[0] != '\0') ? currentName :
                (currentId && currentId[0] != '\0') ? currentId : "<unknown>";
            AddLoadWarning(std::string("HitArea '") + label + "' の drawable / ID 解決に失敗しました。");
        }
    }
}

void NativeModel::StartMotion(const char* group, int no, int priority) {
    if (!_modelSetting || !_motionManager) return;

    _manualMotionActive = false;

    if (priority == 3) {
        _motionManager->SetReservePriority(priority);
    } else if (!_motionManager->ReserveMotion(priority)) {
        return;
    }

    auto groupIt = _motions.find(group);
    if (groupIt == _motions.end()) return;

    auto motionIt = groupIt->second.find(no);
    if (motionIt == groupIt->second.end()) return;

    ACubismMotion* motion = motionIt->second;
    if (!motion) return;

    _lastMotionPriority = static_cast<float>(priority);
    _motionManager->StartMotionPriority(motion, false, priority);
}

void NativeModel::StartRandomMotion(const char* group, int priority) {
    if (!_modelSetting) return;

    csmInt32 count = _modelSetting->GetMotionCount(group);
    if (count <= 0) return;

    int no = rand() % count;
    StartMotion(group, no, priority);
}

void NativeModel::StopAllMotions() {
    if (_motionManager) {
        _motionManager->StopAllMotions();
    }
    _manualMotionActive = false;
    _manualMotion = nullptr;
    _manualMotionGroup.clear();
    _manualMotionIndex = -1;
    _manualMotionLoop = false;
    _manualMotionQueueEntry = CubismMotionQueueEntry();
}

bool NativeModel::IsMotionFinished() {
    if (_manualMotionActive) {
        return false;
    }
    if (!_motionManager) return true;
    return _motionManager->IsFinished();
}

void NativeModel::Update(float deltaTime) {
    UpdatePrePhysics(deltaTime);
    UpdatePostPhysics(deltaTime);
}

void NativeModel::UpdatePrePhysics(float deltaTime) {
    if (!_model) {
        return;
    }

    if (deltaTime < 0.0f) {
        deltaTime = 0.0f;
    }

    csmBool motionUpdated = false;

    _model->LoadParameters();

    if (_manualMotionActive && _manualMotion) {
        _manualMotion->UpdateParameters(_model, &_manualMotionQueueEntry, _manualMotionTimeSeconds);
        motionUpdated = true;
    } else if (_motionManager) {
        motionUpdated = _motionManager->UpdateMotion(_model, deltaTime);
    }

    _model->SaveParameters();

    if (_expressionManager) {
        _expressionManager->UpdateMotion(_model, deltaTime);
    }

    if (_breathEnabled && _breath) {
        _breath->UpdateParameters(_model, deltaTime);
    }

    if (!motionUpdated && _eyeBlinkEnabled && _eyeBlink) {
        _eyeBlink->UpdateParameters(_model, deltaTime);
    }

    if (_dragX != 0.0f || _dragY != 0.0f) {
        _model->AddParameterValue(CubismFramework::GetIdManager()->GetId(ParamAngleX), _dragX * 30.0f);
        _model->AddParameterValue(CubismFramework::GetIdManager()->GetId(ParamAngleY), _dragY * 30.0f);
        _model->AddParameterValue(CubismFramework::GetIdManager()->GetId(ParamAngleZ), _dragX * _dragY * -30.0f);
        _model->AddParameterValue(CubismFramework::GetIdManager()->GetId(ParamBodyAngleX), _dragX * 10.0f);
        _model->AddParameterValue(CubismFramework::GetIdManager()->GetId(ParamEyeBallX), _dragX);
        _model->AddParameterValue(CubismFramework::GetIdManager()->GetId(ParamEyeBallY), _dragY);
    }

    if (_lipSyncEnabled && _lipSyncIds.GetSize() > 0) {
        for (csmUint32 i = 0; i < _lipSyncIds.GetSize(); ++i) {
            _model->SetParameterValue(_lipSyncIds[i], _lipSyncValue);
        }
    }
}

void NativeModel::UpdatePostPhysics(float deltaTime) {
    if (!_model) {
        return;
    }

    if (deltaTime < 0.0f) {
        deltaTime = 0.0f;
    }

    if (_physicsEnabled && _physics) {
        const csmInt32 parameterCount = _model->GetParameterCount();
        std::vector<csmFloat32> prePhysicsValues;
        prePhysicsValues.reserve(parameterCount);
        for (csmInt32 i = 0; i < parameterCount; ++i) {
            prePhysicsValues.push_back(_model->GetParameterValue(i));
        }

        auto options = _physics->GetOptions();
        options.Gravity = _defaultPhysicsGravity;
        options.Wind = CubismVector2(
            _defaultPhysicsWind.X + _physicsWindX,
            _defaultPhysicsWind.Y + _physicsWindY);
        _physics->SetOptions(options);

        _physics->Evaluate(_model, deltaTime);

        if (_physicsOutputScale != 1.0f) {
            for (csmInt32 i = 0; i < parameterCount; ++i) {
                const auto before = prePhysicsValues[static_cast<size_t>(i)];
                const auto after = _model->GetParameterValue(i);
                const auto blended = before + ((after - before) * _physicsOutputScale);
                const auto minimum = _model->GetParameterMinimumValue(i);
                const auto maximum = _model->GetParameterMaximumValue(i);
                const auto clamped = blended < minimum ? minimum : (blended > maximum ? maximum : blended);
                _model->SetParameterValue(i, clamped);
            }
        }
    }

    if (_pose) {
        _pose->UpdateParameters(_model, deltaTime);
    }

    _model->Update();
}

void NativeModel::EvaluateMotion(const char* group, int no, float timeSeconds, bool loop) {
    if (!group) {
        return;
    }

    auto groupIt = _motions.find(group);
    if (groupIt == _motions.end()) {
        Live2DPal::PrintLogLn("EvaluateMotion: group not found '%s'", group);
        return;
    }

    auto motionIt = groupIt->second.find(no);
    if (motionIt == groupIt->second.end()) {
        Live2DPal::PrintLogLn("EvaluateMotion: motion index not found group='%s' index=%d", group, no);
        return;
    }

    _manualMotion = static_cast<CubismMotion*>(motionIt->second);
    if (!_manualMotion) {
        return;
    }

    _manualMotion->SetLoop(loop);
    _manualMotionTimeSeconds = timeSeconds < 0.0f ? 0.0f : timeSeconds;
    _manualMotionActive = true;

    if (_manualMotionActive) {
        const bool changed = (_manualMotionGroup != group) || (_manualMotionIndex != no) || (_manualMotionLoop != loop);
        if (changed) {
            _manualMotionQueueEntry = CubismMotionQueueEntry();
            _manualMotionGroup = group;
            _manualMotionIndex = no;
            _manualMotionLoop = loop;
        }
    }
}

void NativeModel::Draw(CubismMatrix44& matrix) {
    std::lock_guard<std::mutex> lock(g_nativeDrawMutex);

    if (_model == nullptr) return;
    if (_textureViews.empty()) {
        Live2DPal::PrintLogLn("Native Draw: skipped because no textures are bound.");
        return;
    }
    if (!g_d3d11Device || !g_d3d11Context) {
        Live2DPal::PrintLogLn("Native Draw: skipped because D3D11 device/context is null.");
        return;
    }

    CubismRenderer_D3D11* renderer = GetRenderer<CubismRenderer_D3D11>();
    if (!renderer) return;

    matrix.MultiplyByMatrix(_modelMatrix);

    const float rad = _viewRotationDegrees * 3.14159265359f / 180.0f;
    const float cosValue = std::cos(rad) * _viewScale;
    const float sinValue = std::sin(rad) * _viewScale;
    csmFloat32 tr[16] = {
        cosValue,  sinValue,  0.0f, 0.0f,
       -sinValue,  cosValue,  0.0f, 0.0f,
        0.0f,      0.0f,      1.0f, 0.0f,
        _viewPositionX, _viewPositionY, 0.0f, 1.0f
    };
    CubismMatrix44 transformMatrix;
    transformMatrix.SetMatrix(tr);
    matrix.MultiplyByMatrix(&transformMatrix);

    renderer->SetMvpMatrix(&matrix);
    if (!TryDrawModelWithSehGuard(renderer)) {
        return;
    }
}

bool NativeModel::DrawWithFrame(ID3D11Device* device, ID3D11DeviceContext* context, int viewportWidth, int viewportHeight, CubismMatrix44& matrix) {
    // Acquire the global native draw mutex BEFORE calling StartFrame so that:
    //  - s_device / s_context / s_viewportWidth / s_viewportHeight are set atomically
    //    relative to the actual DrawModel call (no other instance can overwrite them
    //    between our StartFrame and our DrawModel).
    std::lock_guard<std::mutex> lock(g_nativeDrawMutex);

    if (_model == nullptr) return false;
    if (_textureViews.empty()) {
        Live2DPal::PrintLogLn("DrawWithFrame: skipped because no textures are bound.");
        return false;
    }
    if (!device || !context) {
        Live2DPal::PrintLogLn("DrawWithFrame: skipped because D3D11 device/context is null.");
        return false;
    }

    CubismRenderer_D3D11* renderer = GetRenderer<CubismRenderer_D3D11>();
    if (!renderer) return false;

    // Call StartFrame here, under the lock, to atomically update the Cubism statics.
    CubismRenderer_D3D11::StartFrame(
        device,
        context,
        static_cast<csmUint32>(viewportWidth),
        static_cast<csmUint32>(viewportHeight));

    matrix.MultiplyByMatrix(_modelMatrix);

    const float rad = _viewRotationDegrees * 3.14159265359f / 180.0f;
    const float cosValue = std::cos(rad) * _viewScale;
    const float sinValue = std::sin(rad) * _viewScale;
    csmFloat32 tr[16] = {
        cosValue,  sinValue,  0.0f, 0.0f,
       -sinValue,  cosValue,  0.0f, 0.0f,
        0.0f,      0.0f,      1.0f, 0.0f,
        _viewPositionX, _viewPositionY, 0.0f, 1.0f
    };
    CubismMatrix44 transformMatrix;
    transformMatrix.SetMatrix(tr);
    matrix.MultiplyByMatrix(&transformMatrix);

    renderer->SetMvpMatrix(&matrix);
    if (!TryDrawModelWithSehGuard(renderer)) {
        return false;
    }

    return true;
}


void NativeModel::DoDraw() {
}

void NativeModel::SetParameterValue(const char* paramId, float value) {
    if (_model) _model->SetParameterValue(CubismFramework::GetIdManager()->GetId(paramId), value);
}

void NativeModel::SetParameterValue(const char* paramId, float value, float weight) {
    if (_model) _model->SetParameterValue(CubismFramework::GetIdManager()->GetId(paramId), value, weight);
}

void NativeModel::AddParameterValue(const char* paramId, float value) {
    if (_model) _model->AddParameterValue(CubismFramework::GetIdManager()->GetId(paramId), value);
}

void NativeModel::AddParameterValue(const char* paramId, float value, float weight) {
    if (_model) _model->AddParameterValue(CubismFramework::GetIdManager()->GetId(paramId), value, weight);
}

void NativeModel::MultiplyParameterValue(const char* paramId, float value) {
    if (_model) _model->MultiplyParameterValue(CubismFramework::GetIdManager()->GetId(paramId), value);
}

float NativeModel::GetParameterValue(const char* paramId) {
    if (_model) return _model->GetParameterValue(CubismFramework::GetIdManager()->GetId(paramId));
    return 0.0f;
}

void NativeModel::SetPartOpacity(const char* partId, float opacity) {
    if (_model) _model->SetPartOpacity(CubismFramework::GetIdManager()->GetId(partId), opacity);
}

float NativeModel::GetPartOpacity(const char* partId) {
    if (_model) return _model->GetPartOpacity(CubismFramework::GetIdManager()->GetId(partId));
    return 1.0f;
}

void NativeModel::SetEyeBlinkEnabled(bool enabled) { _eyeBlinkEnabled = enabled; }
bool NativeModel::GetEyeBlinkEnabled() { return _eyeBlinkEnabled; }
void NativeModel::SetEyeBlinkInterval(float interval) {
    if (_eyeBlink) {
        _eyeBlink->SetBlinkingInterval(interval);
    }
}
void NativeModel::SetEyeBlinkSettings(float closing, float closed, float opening) {
    if (_eyeBlink) {
        _eyeBlink->SetBlinkingSettings(closing, closed, opening);
    }
}

void NativeModel::SetLipSyncEnabled(bool enabled) { _lipSyncEnabled = enabled; }
bool NativeModel::GetLipSyncEnabled() { return _lipSyncEnabled; }
void NativeModel::SetLipSyncValue(float value) { _lipSyncValue = value; }
void NativeModel::SetPhysicsEnabled(bool enabled) { _physicsEnabled = enabled; }
bool NativeModel::GetPhysicsEnabled() { return _physicsEnabled; }
void NativeModel::SetPhysicsOutputScale(float scale) { _physicsOutputScale = scale < 0.0f ? 0.0f : scale; }
void NativeModel::SetPhysicsWind(float x, float y) { _physicsWindX = x; _physicsWindY = y; }
void NativeModel::SetBreathEnabled(bool enabled) { _breathEnabled = enabled; }
bool NativeModel::GetBreathEnabled() { return _breathEnabled; }

void NativeModel::SetExpression(const char* expressionId) {
    if (!_expressionManager || !expressionId) {
        return;
    }

    auto expressionIt = _expressions.find(expressionId);
    if (expressionIt == _expressions.end() || !expressionIt->second) {
        return;
    }

    _expressionManager->StartMotion(expressionIt->second, false);
}

void NativeModel::ClearExpression() {
    if (_expressionManager) {
        _expressionManager->StopAllMotions();
    }
}

void NativeModel::SetRandomExpression() {
    if (_expressions.empty()) {
        return;
    }

    const int randomIndex = rand() % static_cast<int>(_expressions.size());
    int current = 0;
    for (auto& expression : _expressions) {
        if (current == randomIndex) {
            SetExpression(expression.first.c_str());
            return;
        }
        ++current;
    }
}

csmBool NativeModel::HitTest(const char* hitAreaName, csmFloat32 x, csmFloat32 y) {
    if (!_modelSetting || !hitAreaName) {
        return false;
    }

    const csmInt32 count = _modelSetting->GetHitAreasCount();
    for (csmInt32 i = 0; i < count; i++) {
        const char* currentName = _modelSetting->GetHitAreaName(i);
        const char* currentId = _modelSetting->GetHitAreaId(i)->GetString().GetRawString();
        const bool matchesName = currentName && strcmp(currentName, hitAreaName) == 0;
        const bool matchesId = currentId && strcmp(currentId, hitAreaName) == 0;
        if (matchesName || matchesId) {
            return IsHit(_modelSetting->GetHitAreaId(i), x, y);
        }
    }
    return false;
}

bool NativeModel::TryGetHitAreaBounds(const char* hitAreaNameOrId, float& centerX, float& centerY, float& width, float& height) {
    centerX = 0.0f;
    centerY = 0.0f;
    width = 0.0f;
    height = 0.0f;

    if (!_modelSetting || !_model || !hitAreaNameOrId) {
        return false;
    }

    const csmInt32 count = _modelSetting->GetHitAreasCount();
    for (csmInt32 i = 0; i < count; i++) {
        const char* currentName = _modelSetting->GetHitAreaName(i);
        const char* currentId = _modelSetting->GetHitAreaId(i)->GetString().GetRawString();
        const bool matchesName = currentName && strcmp(currentName, hitAreaNameOrId) == 0;
        const bool matchesId = currentId && strcmp(currentId, hitAreaNameOrId) == 0;
        if (!matchesName && !matchesId) {
            continue;
        }

        const csmInt32 drawIndex = _model->GetDrawableIndex(_modelSetting->GetHitAreaId(i));
        if (drawIndex < 0) {
            return false;
        }

        const csmInt32 vertexCount = _model->GetDrawableVertexCount(drawIndex);
        const csmFloat32* vertices = _model->GetDrawableVertices(drawIndex);
        if (vertexCount <= 0 || vertices == nullptr) {
            return false;
        }

        csmFloat32 left = vertices[Constant::VertexOffset];
        csmFloat32 right = vertices[Constant::VertexOffset];
        csmFloat32 top = vertices[Constant::VertexOffset + 1];
        csmFloat32 bottom = vertices[Constant::VertexOffset + 1];

        for (csmInt32 j = 1; j < vertexCount; ++j) {
            const csmFloat32 vertexX = vertices[Constant::VertexOffset + j * Constant::VertexStep];
            const csmFloat32 vertexY = vertices[Constant::VertexOffset + j * Constant::VertexStep + 1];

            if (vertexX < left) left = vertexX;
            if (vertexX > right) right = vertexX;
            if (vertexY < bottom) bottom = vertexY;
            if (vertexY > top) top = vertexY;
        }

        centerX = (left + right) * 0.5f;
        centerY = (top + bottom) * 0.5f;
        width = right - left;
        height = top - bottom;
        return true;
    }

    return false;
}

bool NativeModel::TryGetModelBounds(float& centerX, float& centerY, float& width, float& height) {
    if (!_model) {
        return false;
    }

    const csmInt32 drawableCount = _model->GetDrawableCount();
    if (drawableCount <= 0) {
        return false;
    }

    bool hasAnyVertex = false;
    csmFloat32 left = 0.0f;
    csmFloat32 right = 0.0f;
    csmFloat32 top = 0.0f;
    csmFloat32 bottom = 0.0f;

    for (csmInt32 drawIndex = 0; drawIndex < drawableCount; ++drawIndex) {
        const csmInt32 vertexCount = _model->GetDrawableVertexCount(drawIndex);
        const csmFloat32* vertices = _model->GetDrawableVertices(drawIndex);
        if (vertexCount <= 0 || vertices == nullptr) {
            continue;
        }

        for (csmInt32 j = 0; j < vertexCount; ++j) {
            const csmFloat32 vertexX = vertices[Constant::VertexOffset + j * Constant::VertexStep];
            const csmFloat32 vertexY = vertices[Constant::VertexOffset + j * Constant::VertexStep + 1];

            if (!hasAnyVertex) {
                left = right = vertexX;
                top = bottom = vertexY;
                hasAnyVertex = true;
                continue;
            }

            if (vertexX < left) left = vertexX;
            if (vertexX > right) right = vertexX;
            if (vertexY < bottom) bottom = vertexY;
            if (vertexY > top) top = vertexY;
        }
    }

    if (!hasAnyVertex) {
        return false;
    }

    centerX = (left + right) * 0.5f;
    centerY = (top + bottom) * 0.5f;
    width = right - left;
    height = top - bottom;
    return true;
}

void NativeModel::SetMultiplyColor(float r, float g, float b, float a) {
    CubismRenderer_D3D11* renderer = GetRenderer<CubismRenderer_D3D11>();
    if (renderer) {
        renderer->SetModelColor(r, g, b, a);
    }
}
void NativeModel::SetScreenColor(float r, float g, float b, float a) {}
void NativeModel::SetOpacity(float opacity) {
    _opacity = opacity;
}
float NativeModel::GetOpacity() { return _opacity; }
void NativeModel::ApplyStandardParameters(
    float eyeLOpen,
    float eyeROpen,
    float mouthOpenY,
    float mouthForm,
    float angleX,
    float angleY,
    float angleZ,
    float bodyAngleX,
    float eyeBallX,
    float eyeBallY,
    float cheek,
    float armLA,
    float armRA) {
    if (!_model) {
        return;
    }

    _model->AddParameterValue(CubismFramework::GetIdManager()->GetId(ParamEyeLOpen), eyeLOpen);
    _model->AddParameterValue(CubismFramework::GetIdManager()->GetId(ParamEyeROpen), eyeROpen);
    _model->AddParameterValue(CubismFramework::GetIdManager()->GetId(ParamMouthOpenY), mouthOpenY);
    _model->AddParameterValue(CubismFramework::GetIdManager()->GetId(ParamMouthForm), mouthForm);
    _model->AddParameterValue(CubismFramework::GetIdManager()->GetId(ParamAngleX), angleX);
    _model->AddParameterValue(CubismFramework::GetIdManager()->GetId(ParamAngleY), angleY);
    _model->AddParameterValue(CubismFramework::GetIdManager()->GetId(ParamAngleZ), angleZ);
    _model->AddParameterValue(CubismFramework::GetIdManager()->GetId(ParamBodyAngleX), bodyAngleX);
    _model->AddParameterValue(CubismFramework::GetIdManager()->GetId(ParamEyeBallX), eyeBallX);
    _model->AddParameterValue(CubismFramework::GetIdManager()->GetId(ParamEyeBallY), eyeBallY);
    _model->AddParameterValue(CubismFramework::GetIdManager()->GetId(ParamCheek), cheek);
    _model->AddParameterValue(CubismFramework::GetIdManager()->GetId(ParamArmLA), armLA);
    _model->AddParameterValue(CubismFramework::GetIdManager()->GetId(ParamArmRA), armRA);
}

void NativeModel::ApplyItemParameters(float opacity, float multiplyR, float multiplyG, float multiplyB, float multiplyA) {
    SetOpacity(opacity);
    SetMultiplyColor(multiplyR, multiplyG, multiplyB, multiplyA);
}

void NativeModel::CommitParameters() {
    if (_model) {
        _model->Update();
    }
}

void NativeModel::SetPosition(float x, float y) {
    _viewPositionX = x;
    _viewPositionY = y;
}
void NativeModel::SetScale(float scale) {
    _viewScale = scale > 0.0f ? scale : 0.01f;
}
void NativeModel::SetRotation(float rotation) {
    _viewRotationDegrees = rotation;
}
void NativeModel::SetDragging(float x, float y) { _dragX = x; _dragY = y; }

float NativeModel::GetCanvasWidth() { return _model ? _model->GetCanvasWidth() : 0.0f; }
float NativeModel::GetCanvasHeight() { return _model ? _model->GetCanvasHeight() : 0.0f; }

} // namespace VTuberKitForNative
