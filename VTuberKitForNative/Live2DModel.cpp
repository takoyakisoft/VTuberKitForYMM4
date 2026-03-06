#include "Live2DModel.h"
#include "Live2DManager.h"
#include "Live2DModel_Internal.h"
#include "Live2DPal.h"
#include "ManagedStringUtils.h"
#include "Live2DRenderer.h"
#include <Model/CubismModel.hpp>
using namespace Live2D::Cubism::Framework;

namespace VTuberKitForNative {

Live2DModelWrapper::Live2DModelWrapper() {
    _nativeModel = new NativeModel();
    _isLoaded = false;
    _lastErrorMessage = nullptr;
}

Live2DModelWrapper::~Live2DModelWrapper() {
    this->!Live2DModelWrapper();
}

Live2DModelWrapper::!Live2DModelWrapper() {
    if (_nativeModel != nullptr) {
        delete _nativeModel;
        _nativeModel = nullptr;
    }

    Live2DManager::GetInstance()->ReleaseModel(this);
}

void Live2DModelWrapper::Initialize() {
    // Initialization logic if needed
}

void Live2DModelWrapper::Release() {
    if (_nativeModel != nullptr) {
        delete _nativeModel;
        _nativeModel = nullptr;
    }
}

bool Live2DModelWrapper::LoadModel(System::String^ modelPath) {
    _lastErrorMessage = nullptr;

    if (modelPath == nullptr || modelPath->Length == 0) {
        _isLoaded = false;
        _lastErrorMessage = "モデルパスが空です。";
        return false;
    }

    if (_nativeModel == nullptr) {
        _nativeModel = new NativeModel();
    }

    // model3.jsonのパスを解析
    std::string modelPathStr = ManagedStringToUtf8(modelPath);
    size_t lastSlash = modelPathStr.find_last_of("/\\");
    if (lastSlash == std::string::npos) {
        _isLoaded = false;
        _lastErrorMessage = "モデルパスの形式が不正です。";
        return false;
    }

    std::string dir = modelPathStr.substr(0, lastSlash + 1);
    std::string fileName = modelPathStr.substr(lastSlash + 1);

    const bool loadSucceeded = _nativeModel->LoadAssets(dir.c_str(), fileName.c_str());
    _isLoaded = loadSucceeded && (_nativeModel->GetModelSetting() != nullptr) && (_nativeModel->GetModel() != nullptr);

    if (!_isLoaded) {
        const std::string& nativeError = _nativeModel->GetLastErrorMessage();
        if (!nativeError.empty()) {
            _lastErrorMessage = Utf8ToManagedString(nativeError.c_str());
        } else {
            _lastErrorMessage = "モデルの読み込みに失敗しました。moc3とCubism Coreの互換性を確認してください。";
        }
    }

    _modelPath = modelPath;

    return _isLoaded;
}

System::String^ Live2DModelWrapper::LastErrorMessage::get() {
    return _lastErrorMessage;
}

void Live2DModelWrapper::ReloadRenderer() {
    if (_nativeModel != nullptr) {
        _nativeModel->ReloadRenderer();
    }
}

void Live2DModelWrapper::ResetAnimationState() {
    if (_nativeModel != nullptr) {
        _nativeModel->ResetAnimationState();
    }
}

void Live2DModelWrapper::Update(float deltaTime) {
    if (_nativeModel != nullptr && _isLoaded) {
        _nativeModel->Update(deltaTime);
    }
}

void Live2DModelWrapper::Draw(CubismMatrix44& matrix) {
    if (_nativeModel != nullptr && _isLoaded) {
        _nativeModel->Draw(matrix);
    }
}

void Live2DModelWrapper::Draw() {
    // デフォルトは正方形領域（1:1）で描画
    Draw(1, 1);
}

void Live2DModelWrapper::Draw(int screenWidth, int screenHeight) {
    if (_nativeModel != nullptr && _isLoaded && screenWidth > 0 && screenHeight > 0) {
        CubismMatrix44 matrix;
        matrix.LoadIdentity();

        const float screenAspect = static_cast<float>(screenWidth) / static_cast<float>(screenHeight);
        if (screenAspect > 1.0f) {
            matrix.Scale(1.0f / screenAspect, 1.0f);
        } else {
            matrix.Scale(1.0f, screenAspect);
        }
        _nativeModel->Draw(matrix);
    }
}

void Live2DModelWrapper::DrawWithFrame(System::IntPtr device, System::IntPtr context, int screenWidth, int screenHeight) {
    if (_nativeModel != nullptr && _isLoaded && screenWidth > 0 && screenHeight > 0) {
        CubismMatrix44 matrix;
        matrix.LoadIdentity();

        const float screenAspect = static_cast<float>(screenWidth) / static_cast<float>(screenHeight);
        if (screenAspect > 1.0f) {
            matrix.Scale(1.0f / screenAspect, 1.0f);
        } else {
            matrix.Scale(1.0f, screenAspect);
        }
        
        auto pDevice = static_cast<ID3D11Device*>(device.ToPointer());
        auto pContext = static_cast<ID3D11DeviceContext*>(context.ToPointer());
        _nativeModel->DrawWithFrame(pDevice, pContext, screenWidth, screenHeight, matrix);
    }
}


void Live2DModelWrapper::SetParameterValue(System::String^ paramId, float value) {
    if (_nativeModel != nullptr && paramId != nullptr) {
        const std::string id = ManagedStringToUtf8(paramId);
        _nativeModel->SetParameterValue(id.c_str(), value);
    }
}

void Live2DModelWrapper::SetParameterValue(System::String^ paramId, float value, float weight) {
    if (_nativeModel != nullptr && paramId != nullptr) {
        const std::string id = ManagedStringToUtf8(paramId);
        _nativeModel->SetParameterValue(id.c_str(), value, weight);
    }
}

void Live2DModelWrapper::AddParameterValue(System::String^ paramId, float value) {
    if (_nativeModel != nullptr && paramId != nullptr) {
        const std::string id = ManagedStringToUtf8(paramId);
        _nativeModel->AddParameterValue(id.c_str(), value);
    }
}

void Live2DModelWrapper::AddParameterValue(System::String^ paramId, float value, float weight) {
    if (_nativeModel != nullptr && paramId != nullptr) {
        const std::string id = ManagedStringToUtf8(paramId);
        _nativeModel->AddParameterValue(id.c_str(), value, weight);
    }
}

void Live2DModelWrapper::MultiplyParameterValue(System::String^ paramId, float value) {
    if (_nativeModel != nullptr && paramId != nullptr) {
        const std::string id = ManagedStringToUtf8(paramId);
        _nativeModel->MultiplyParameterValue(id.c_str(), value);
    }
}

float Live2DModelWrapper::GetParameterValue(System::String^ paramId) {
    if (_nativeModel != nullptr && paramId != nullptr) {
        const std::string id = ManagedStringToUtf8(paramId);
        return _nativeModel->GetParameterValue(id.c_str());
    }
    return 0.0f;
}

array<Live2DParameter^>^ Live2DModelWrapper::GetParameters() {
    if (_nativeModel == nullptr || _nativeModel->GetModelSetting() == nullptr) {
        return gcnew array<Live2DParameter^>(0);
    }

    CubismModel* model = _nativeModel->GetModel();
    if (model == nullptr) {
        return gcnew array<Live2DParameter^>(0);
    }

    int count = model->GetParameterCount();
    array<Live2DParameter^>^ result = gcnew array<Live2DParameter^>(count);

    for (int i = 0; i < count; ++i) {
        CubismIdHandle paramId = model->GetParameterId(i);
        const char* id = paramId->GetString().GetRawString();
        const char* name = id; // ICubismModelSetting does not provide names in SDK 5

        result[i] = gcnew Live2DParameter();
        result[i]->Id = Utf8ToManagedString(id);
        result[i]->Name = Utf8ToManagedString(name);
        result[i]->Value = model->GetParameterValue(i);
        result[i]->Default = model->GetParameterDefaultValue(i);
        result[i]->Min = model->GetParameterMinimumValue(i);
        result[i]->Max = model->GetParameterMaximumValue(i);
    }

    return result;
}

void Live2DModelWrapper::SetPartOpacity(System::String^ partId, float opacity) {
    if (_nativeModel != nullptr && partId != nullptr) {
        const std::string id = ManagedStringToUtf8(partId);
        _nativeModel->SetPartOpacity(id.c_str(), opacity);
    }
}

float Live2DModelWrapper::GetPartOpacity(System::String^ partId) {
    if (_nativeModel != nullptr && partId != nullptr) {
        const std::string id = ManagedStringToUtf8(partId);
        return _nativeModel->GetPartOpacity(id.c_str());
    }
    return 1.0f;
}

array<Live2DPart^>^ Live2DModelWrapper::GetParts() {
    if (_nativeModel == nullptr || _nativeModel->GetModelSetting() == nullptr) {
        return gcnew array<Live2DPart^>(0);
    }

    CubismModel* model = _nativeModel->GetModel();
    if (model == nullptr) {
        return gcnew array<Live2DPart^>(0);
    }

    int count = model->GetPartCount();
    array<Live2DPart^>^ result = gcnew array<Live2DPart^>(count);

    for (int i = 0; i < count; ++i) {
        CubismIdHandle partId = model->GetPartId(i);
        const char* id = partId->GetString().GetRawString();
        const char* name = id; // ICubismModelSetting does not provide names in SDK 5

        result[i] = gcnew Live2DPart();
        result[i]->Id = Utf8ToManagedString(id);
        result[i]->Name = Utf8ToManagedString(name);
        result[i]->Opacity = model->GetPartOpacity(i);
    }

    return result;
}

void Live2DModelWrapper::SetEyeBlinkEnabled(bool enabled) {
    if (_nativeModel != nullptr) {
        _nativeModel->SetEyeBlinkEnabled(enabled);
    }
}

bool Live2DModelWrapper::GetEyeBlinkEnabled() {
    if (_nativeModel != nullptr) {
        return _nativeModel->GetEyeBlinkEnabled();
    }
    return false;
}

void Live2DModelWrapper::SetEyeBlinkInterval(float interval) {
    if (_nativeModel != nullptr) {
        _nativeModel->SetEyeBlinkInterval(interval);
    }
}

void Live2DModelWrapper::SetEyeBlinkSettings(float closing, float closed, float opening) {
    if (_nativeModel != nullptr) {
        _nativeModel->SetEyeBlinkSettings(closing, closed, opening);
    }
}

void Live2DModelWrapper::SetLipSyncEnabled(bool enabled) {
    if (_nativeModel != nullptr) {
        _nativeModel->SetLipSyncEnabled(enabled);
    }
}

bool Live2DModelWrapper::GetLipSyncEnabled() {
    if (_nativeModel != nullptr) {
        return _nativeModel->GetLipSyncEnabled();
    }
    return false;
}

void Live2DModelWrapper::SetLipSyncValue(float value) {
    if (_nativeModel != nullptr) {
        _nativeModel->SetLipSyncValue(value);
    }
}

void Live2DModelWrapper::SetPhysicsEnabled(bool enabled) {
    if (_nativeModel != nullptr) {
        _nativeModel->SetPhysicsEnabled(enabled);
    }
}

bool Live2DModelWrapper::GetPhysicsEnabled() {
    if (_nativeModel != nullptr) {
        return _nativeModel->GetPhysicsEnabled();
    }
    return true;
}

void Live2DModelWrapper::SetBreathEnabled(bool enabled) {
    if (_nativeModel != nullptr) {
        _nativeModel->SetBreathEnabled(enabled);
    }
}

bool Live2DModelWrapper::GetBreathEnabled() {
    if (_nativeModel != nullptr) {
        return _nativeModel->GetBreathEnabled();
    }
    return true;
}

void Live2DModelWrapper::SetExpression(System::String^ expressionId) {
    if (_nativeModel != nullptr && expressionId != nullptr) {
        const std::string id = ManagedStringToUtf8(expressionId);
        _nativeModel->SetExpression(id.c_str());
    }
}

void Live2DModelWrapper::ClearExpression() {
    if (_nativeModel != nullptr) {
        _nativeModel->ClearExpression();
    }
}

void Live2DModelWrapper::SetRandomExpression() {
    if (_nativeModel != nullptr) {
        _nativeModel->SetRandomExpression();
    }
}

array<Live2DExpression^>^ Live2DModelWrapper::GetExpressions() {
    if (_nativeModel == nullptr || _nativeModel->GetModelSetting() == nullptr) {
        return gcnew array<Live2DExpression^>(0);
    }

    ICubismModelSetting* setting = _nativeModel->GetModelSetting();
    const csmInt32 count = setting->GetExpressionCount();
    array<Live2DExpression^>^ result = gcnew array<Live2DExpression^>(count);

    for (csmInt32 i = 0; i < count; ++i) {
        result[i] = gcnew Live2DExpression();
        result[i]->Id = Utf8ToManagedString(setting->GetExpressionName(i));
        result[i]->Name = Utf8ToManagedString(setting->GetExpressionName(i));
    }

    return result;
}

void Live2DModelWrapper::StartMotion(System::String^ group, int index, int priority) {
    if (_nativeModel != nullptr && group != nullptr) {
        const std::string groupStr = ManagedStringToUtf8(group);
        _nativeModel->StartMotion(groupStr.c_str(), index, priority);
    }
}

void Live2DModelWrapper::StartRandomMotion(System::String^ group, int priority) {
    if (_nativeModel != nullptr && group != nullptr) {
        const std::string groupStr = ManagedStringToUtf8(group);
        _nativeModel->StartRandomMotion(groupStr.c_str(), priority);
    }
}

void Live2DModelWrapper::EvaluateMotion(System::String^ group, int index, float timeSeconds, bool loop) {
    if (_nativeModel != nullptr && group != nullptr) {
        const std::string groupStr = ManagedStringToUtf8(group);
        _nativeModel->EvaluateMotion(groupStr.c_str(), index, timeSeconds, loop);
    }
}

void Live2DModelWrapper::StopAllMotions() {
    if (_nativeModel != nullptr) {
        _nativeModel->StopAllMotions();
    }
}

bool Live2DModelWrapper::IsMotionFinished() {
    if (_nativeModel != nullptr) {
        return _nativeModel->IsMotionFinished();
    }
    return true;
}

array<Live2DMotion^>^ Live2DModelWrapper::GetMotions() {
    // モーションリスト取得（実装はモデル設定から取得）
    if (_nativeModel == nullptr || _nativeModel->GetModelSetting() == nullptr) {
        return gcnew array<Live2DMotion^>(0);
    }

    ICubismModelSetting* setting = _nativeModel->GetModelSetting();
    csmInt32 groupCount = setting->GetMotionGroupCount();
    
    // モーション数をカウント
    csmInt32 motionCount = 0;
    for (csmInt32 i = 0; i < groupCount; ++i) {
        motionCount += setting->GetMotionCount(setting->GetMotionGroupName(i));
    }

    array<Live2DMotion^>^ motions = gcnew array<Live2DMotion^>(motionCount);
    csmInt32 idx = 0;
    for (csmInt32 g = 0; g < groupCount; ++g) {
        const char* group = setting->GetMotionGroupName(g);
        csmInt32 count = setting->GetMotionCount(group);
        for (csmInt32 m = 0; m < count; ++m, ++idx) {
            motions[idx] = gcnew Live2DMotion();
            motions[idx]->Group = Utf8ToManagedString(group);
            motions[idx]->Index = m;
            motions[idx]->Name = Utf8ToManagedString(setting->GetMotionFileName(group, m));
        }
    }

    return motions;
}

bool Live2DModelWrapper::HitTest(System::String^ hitAreaName, float x, float y) {
    if (_nativeModel != nullptr && hitAreaName != nullptr) {
        const std::string name = ManagedStringToUtf8(hitAreaName);
        return _nativeModel->HitTest(name.c_str(), x, y);
    }
    return false;
}

array<Live2DHitArea^>^ Live2DModelWrapper::GetHitAreas() {
    if (_nativeModel == nullptr || _nativeModel->GetModelSetting() == nullptr) {
        return gcnew array<Live2DHitArea^>(0);
    }

    ICubismModelSetting* setting = _nativeModel->GetModelSetting();
    csmInt32 count = setting->GetHitAreasCount();
    array<Live2DHitArea^>^ areas = gcnew array<Live2DHitArea^>(count);

    for (csmInt32 i = 0; i < count; ++i) {
        areas[i] = gcnew Live2DHitArea();
        areas[i]->Id = Utf8ToManagedString(setting->GetHitAreaId(i)->GetString().GetRawString());
        areas[i]->Name = Utf8ToManagedString(setting->GetHitAreaName(i));
        areas[i]->X = 0.0f;
        areas[i]->Y = 0.0f;
        areas[i]->Width = 0.0f;
        areas[i]->Height = 0.0f;
    }

    return areas;
}

void Live2DModelWrapper::SetMultiplyColor(float r, float g, float b, float a) {
    if (_nativeModel != nullptr) {
        _nativeModel->SetMultiplyColor(r, g, b, a);
    }
}

void Live2DModelWrapper::SetScreenColor(float r, float g, float b, float a) {
    if (_nativeModel != nullptr) {
        _nativeModel->SetScreenColor(r, g, b, a);
    }
}

void Live2DModelWrapper::SetOpacity(float opacity) {
    if (_nativeModel != nullptr) {
        _nativeModel->SetOpacity(opacity);
    }
}

float Live2DModelWrapper::GetOpacity() {
    if (_nativeModel != nullptr) {
        return _nativeModel->GetOpacity();
    }
    return 1.0f;
}

void Live2DModelWrapper::ApplyStandardParameters(
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
    if (_nativeModel != nullptr) {
        _nativeModel->ApplyStandardParameters(
            eyeLOpen,
            eyeROpen,
            mouthOpenY,
            mouthForm,
            angleX,
            angleY,
            angleZ,
            bodyAngleX,
            eyeBallX,
            eyeBallY,
            cheek,
            armLA,
            armRA);
    }
}

void Live2DModelWrapper::ApplyItemParameters(float opacity, float multiplyR, float multiplyG, float multiplyB, float multiplyA) {
    if (_nativeModel != nullptr) {
        _nativeModel->ApplyItemParameters(opacity, multiplyR, multiplyG, multiplyB, multiplyA);
    }
}

void Live2DModelWrapper::CommitParameters() {
    if (_nativeModel != nullptr) {
        _nativeModel->CommitParameters();
    }
}

void Live2DModelWrapper::SetPosition(float x, float y) {
    if (_nativeModel != nullptr) {
        _nativeModel->SetPosition(x, y);
    }
}

void Live2DModelWrapper::SetScale(float scale) {
    if (_nativeModel != nullptr) {
        _nativeModel->SetScale(scale);
    }
}

void Live2DModelWrapper::SetRotation(float rotation) {
    if (_nativeModel != nullptr) {
        _nativeModel->SetRotation(rotation);
    }
}

void Live2DModelWrapper::SetDragging(float x, float y) {
    if (_nativeModel != nullptr) {
        _nativeModel->SetDragging(x, y);
    }
}

float Live2DModelWrapper::GetCanvasWidth() {
    if (_nativeModel != nullptr) {
        return _nativeModel->GetCanvasWidth();
    }
    return 0.0f;
}

float Live2DModelWrapper::GetCanvasHeight() {
    if (_nativeModel != nullptr) {
        return _nativeModel->GetCanvasHeight();
    }
    return 0.0f;
}

Live2DSize Live2DModelWrapper::GetModelSize() {
    Live2DSize size;
    size.Width = static_cast<int>(GetCanvasWidth() * 100);
    size.Height = static_cast<int>(GetCanvasHeight() * 100);
    return size;
}

int Live2DModelWrapper::GetTextureWidth() {
    if (_nativeModel != nullptr) {
        return _nativeModel->GetTextureWidth();
    }
    return 0;
}

int Live2DModelWrapper::GetTextureHeight() {
    if (_nativeModel != nullptr) {
        return _nativeModel->GetTextureHeight();
    }
    return 0;
}

} // namespace VTuberKitForNative
