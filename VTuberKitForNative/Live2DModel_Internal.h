#pragma once

#include <d3d11.h>
#include <CubismFramework.hpp>
#include <Model/CubismUserModel.hpp>
#include <CubismDefaultParameterId.hpp>
#include <Motion/CubismMotionManager.hpp>
#include <Motion/CubismMotion.hpp>
#include <Motion/CubismMotionQueueEntry.hpp>
#include <Effect/CubismBreath.hpp>
#include <Effect/CubismEyeBlink.hpp>
#include <Rendering/D3D11/CubismRenderer_D3D11.hpp>

#include <vector> // 追加
#include <map>
#include <string>

using namespace Live2D::Cubism::Framework;
using namespace Live2D::Cubism::Framework::DefaultParameterId;

namespace VTuberKitForNative {

// ネイティブモデル実装
class NativeModel : public CubismUserModel {
public:
    NativeModel();
    virtual ~NativeModel();

    bool LoadAssets(const char* dir, const char* fileName);
    void ReloadRenderer();
    void ResetAnimationState();
    void Update(float deltaTime);
    void UpdatePrePhysics(float deltaTime);
    void UpdatePostPhysics(float deltaTime);
    void EvaluateMotion(const char* group, int no, float timeSeconds, bool loop);
    void Draw(CubismMatrix44& matrix);                                     // 後方互換
    bool DrawWithFrame(ID3D11Device* device, ID3D11DeviceContext* context, int viewportWidth, int viewportHeight, CubismMatrix44& matrix); // StartFrame込み
    const std::string& GetLastErrorMessage() const { return _lastErrorMessage; }

    // パラメータ制御
    void SetParameterValue(const char* paramId, float value);
    void SetParameterValue(const char* paramId, float value, float weight);
    void AddParameterValue(const char* paramId, float value);
    void AddParameterValue(const char* paramId, float value, float weight);
    void MultiplyParameterValue(const char* paramId, float value);
    float GetParameterValue(const char* paramId);
    void SetPartOpacity(const char* partId, float opacity);
    float GetPartOpacity(const char* partId);
    void SetEyeBlinkEnabled(bool enabled);
    bool GetEyeBlinkEnabled();
    void SetEyeBlinkInterval(float interval);
    void SetEyeBlinkSettings(float closing, float closed, float opening);
    void SetLipSyncEnabled(bool enabled);
    bool GetLipSyncEnabled();
    void SetLipSyncValue(float value);
    void SetPhysicsEnabled(bool enabled);
    bool GetPhysicsEnabled();
    void SetBreathEnabled(bool enabled);
    bool GetBreathEnabled();
    void SetExpression(const char* expressionId);
    void ClearExpression();
    void SetRandomExpression();
    void StartMotion(const char* group, int no, int priority);
    void StartRandomMotion(const char* group, int priority);
    void StopAllMotions();
    bool IsMotionFinished();
    csmBool HitTest(const char* hitAreaName, csmFloat32 x, csmFloat32 y);
    void SetMultiplyColor(float r, float g, float b, float a);
    void SetScreenColor(float r, float g, float b, float a);
    void SetOpacity(float opacity);
    float GetOpacity();
    void ApplyStandardParameters(
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
        float armRA);
    void ApplyItemParameters(float opacity, float multiplyR, float multiplyG, float multiplyB, float multiplyA);
    void CommitParameters();
    void SetPosition(float x, float y);
    void SetScale(float scale);
    void SetRotation(float rotation);
    void SetDragging(float x, float y);
    float GetCanvasWidth();
    float GetCanvasHeight();
    bool TryGetModelBounds(float& centerX, float& centerY, float& width, float& height);
    bool TryGetHitAreaBounds(const char* hitAreaNameOrId, float& centerX, float& centerY, float& width, float& height);
    ICubismModelSetting* GetModelSetting() { return _modelSetting; }
    
    // テクスチャサイズ取得
    int GetTextureWidth() const { return _textureWidth; }
    int GetTextureHeight() const { return _textureHeight; }

protected:
    void DoDraw();

private:
    void InitializeBlinkAndBreath();
    bool SetupModel(ICubismModelSetting* setting);
    void SetupTextures();
    void ReleaseTextures(); // 追加
    void LoadMotions(); // モーション読み込み

    ICubismModelSetting* _modelSetting;
    csmString _modelHomeDir;

    std::vector<ID3D11ShaderResourceView*> _textureViews;
    
    // テクスチャサイズを記録
    int _textureWidth = 0;
    int _textureHeight = 0;
    
    // モーション管理
    std::map<std::string, std::map<int, ACubismMotion*>> _motions;
    std::map<std::string, ACubismMotion*> _expressions;
    float _lastMotionPriority;

    csmVector<CubismIdHandle> _eyeBlinkIds;
    csmVector<CubismIdHandle> _lipSyncIds;
    CubismBreath* _breath = nullptr;
    CubismEyeBlink* _eyeBlink = nullptr;
    bool _eyeBlinkEnabled = true;
    bool _lipSyncEnabled = false;
    bool _physicsEnabled = true;
    bool _breathEnabled = true;
    float _lipSyncValue = 0.0f;
    float _viewPositionX = 0.0f;
    float _viewPositionY = 0.0f;
    float _viewScale = 1.0f;
    float _viewRotationDegrees = 0.0f;
    bool _manualMotionActive = false;
    CubismMotion* _manualMotion = nullptr;
    float _manualMotionTimeSeconds = 0.0f;
    CubismMotionQueueEntry _manualMotionQueueEntry;
    std::string _manualMotionGroup;
    int _manualMotionIndex = -1;
    bool _manualMotionLoop = false;
    std::string _lastErrorMessage;
};

} // namespace VTuberKitForNative
