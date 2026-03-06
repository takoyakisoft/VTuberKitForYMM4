#pragma once

#include <d3d11.h>
#include <CubismFramework.hpp>
#include <ICubismModelSetting.hpp>
#include <Model/CubismUserModel.hpp>
#include <Type/csmRectF.hpp>
#include <map>
#include <string>

using namespace Csm;

namespace VTuberKitForNative {

ref class Live2DRenderer;
ref class Live2DModelWrapper;
ref class Live2DManager;

public value struct Live2DSize {
    int Width;
    int Height;
};

public value struct Live2DColor {
    float R;
    float G;
    float B;
    float A;
};

public ref class Live2DParameter {
public:
    property System::String^ Id;
    property System::String^ Name;
    property float Value;
    property float Min;
    property float Max;
    property float Default;
};

public ref class Live2DPart {
public:
    property System::String^ Id;
    property System::String^ Name;
    property float Opacity;
};

public ref class Live2DExpression {
public:
    property System::String^ Id;
    property System::String^ Name;
};

public ref class Live2DMotion {
public:
    property System::String^ Group;
    property int Index;
    property System::String^ Name;
};

public ref class Live2DHitArea {
public:
    property System::String^ Id;
    property System::String^ Name;
    property float X;
    property float Y;
    property float Width;
    property float Height;
};

class NativeModel;

public ref class Live2DModelWrapper {
public:
    Live2DModelWrapper();
    ~Live2DModelWrapper();
    !Live2DModelWrapper();

    void Initialize();
    void Release();

    // モデルファイルロード
    bool LoadModel(System::String^ modelPath);
    property System::String^ LastErrorMessage
    {
        System::String^ get();
    }
    void ReloadRenderer();
    void ResetAnimationState();

    // 更新と描画
    void Update(float deltaTime);
    void Draw(CubismMatrix44& matrix);
    void Draw(); // デフォルト描画（画面にフィット）
    void Draw(int screenWidth, int screenHeight); // 画面サイズを考慮した描画（BeginFrame別途呼ぶ旧方式）
    bool DrawWithFrame(System::IntPtr device, System::IntPtr context, int screenWidth, int screenHeight); // StartFrame込みアトミック描画（推奨）

    // パラメータ制御
    void SetParameterValue(System::String^ paramId, float value);
    void SetParameterValue(System::String^ paramId, float value, float weight);
    void AddParameterValue(System::String^ paramId, float value);
    void AddParameterValue(System::String^ paramId, float value, float weight);
    void MultiplyParameterValue(System::String^ paramId, float value);
    float GetParameterValue(System::String^ paramId);
    array<Live2DParameter^>^ GetParameters();

    // パーツ制御
    void SetPartOpacity(System::String^ partId, float opacity);
    float GetPartOpacity(System::String^ partId);
    array<Live2DPart^>^ GetParts();

    // 目パチ制御
    void SetEyeBlinkEnabled(bool enabled);
    bool GetEyeBlinkEnabled();
    void SetEyeBlinkInterval(float interval);
    void SetEyeBlinkSettings(float closing, float closed, float opening);

    // 口パク制御
    void SetLipSyncEnabled(bool enabled);
    bool GetLipSyncEnabled();
    void SetLipSyncValue(float value);
    void SetPhysicsEnabled(bool enabled);
    bool GetPhysicsEnabled();
    void SetBreathEnabled(bool enabled);
    bool GetBreathEnabled();

    // 表情制御
    void SetExpression(System::String^ expressionId);
    void ClearExpression();
    void SetRandomExpression();
    array<Live2DExpression^>^ GetExpressions();

    // モーション制御
    void StartMotion(System::String^ group, int index, int priority);
    void StartRandomMotion(System::String^ group, int priority);
    void EvaluateMotion(System::String^ group, int index, float timeSeconds, bool loop);
    void StopAllMotions();
    bool IsMotionFinished();
    array<Live2DMotion^>^ GetMotions();

    // 当たり判定
    bool HitTest(System::String^ hitAreaName, float x, float y);
    array<Live2DHitArea^>^ GetHitAreas();

    // 描画設定
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

    // 位置・スケール制御
    void SetPosition(float x, float y);
    void SetScale(float scale);
    void SetRotation(float rotation);

    // ドラッグ
    void SetDragging(float x, float y);

    // モデル情報
    float GetCanvasWidth();
    float GetCanvasHeight();
    Live2DSize GetModelSize();
    
    // テクスチャサイズ（ピクセル単位）
    int GetTextureWidth();
    int GetTextureHeight();

private:
    NativeModel* _nativeModel;
    bool _isLoaded;
    System::String^ _modelPath;
    System::String^ _lastErrorMessage;
};

} // namespace VTuberKitForNative
