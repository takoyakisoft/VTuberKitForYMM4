using SharpGen.Runtime;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Numerics;
using System.Windows;
using Vortice.Direct2D1;
using Vortice.Direct3D11;
using Vortice.DXGI;
using VTuberKitForNative;
using VTuberKitForYMM4.Plugin.Shape;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Tachie;

namespace VTuberKitForYMM4.Plugin
{
    public class Live2DTachieSource : ITachieSource2, IDisposable
    {
        private readonly record struct StandardAnchorCandidate(string ParameterId, float Influence);
        private const int MinRenderTargetSize = 256;
        private const int DefaultMaxRenderTargetSize = 4096;
        private const int MinAllowedRenderTargetMaxSize = 2048;
        private const int MaxAllowedRenderTargetMaxSize = 8192;
        private const float MinInternalRenderScale = 1.0f;
        private const float MaxInternalRenderScale = 4.0f;
        private const float DefaultInternalRenderScale = 2.0f;
        private static readonly bool EnableDebugOverlay = false;
        private const int MaxConsecutiveRenderFailures = 3;
        private const int MaxReplaySteps = 600;
        private static readonly TimeSpan RenderRecoveryCooldown = TimeSpan.FromSeconds(2);

        private static readonly object _drawLock = new object();
        private static readonly object _sharedRendererLock = new object();
        private static Live2DRenderer? _sharedRenderer;
        private static IntPtr _sharedDevicePtr = IntPtr.Zero;
        private static IntPtr _sharedContextPtr = IntPtr.Zero;
        private static int _sharedDeviceGeneration;
        private static int _activeSourceCount;
        private static int _nextInstanceId;
        private readonly IGraphicsDevicesAndContext _devices;
        private readonly int _instanceId;
        private bool _disposed;
        private bool _d3d11Initialized;
        private string _currentModelPath = string.Empty;
        private Live2DModelWrapper? _model;
        private Live2DRenderer? _renderer;

        private ID3D11Texture2D? _d3dTexture;
        private ID3D11RenderTargetView? _rtv;
        private ID3D11Texture2D? _depthStencilTexture;
        private ID3D11DepthStencilView? _dsv;
        private ID3D11Texture2D? _msaaTexture;
        private ID3D11RenderTargetView? _msaaRtv;
        private ID3D11Texture2D? _msaaDepthStencilTexture;
        private ID3D11DepthStencilView? _msaaDsv;
        private int _msaaSampleCount = 1;
        private ID2D1Bitmap1? _d2dBitmap;
        private ID2D1Bitmap1? _outputBitmap;
        private ID2D1Bitmap1? _fallbackOutputBitmap;
        private ID2D1Effect? _transformEffect;
        private ID2D1Image? _outputImage;
        private double _lastItemFrame;
        private bool _hasLastItemFrame;
        private bool _needsInitialFrameUpdate = true;
        private string _appliedExpressionId = string.Empty;
        private bool _hasCachedCharacterSettings;
        private bool _cachedAutoEyeBlink;
        private float _cachedEyeBlinkInterval;
        private bool _cachedEnablePhysics;
        private bool _cachedEnableBreath;
        private bool _hasCachedItemSettings;
        private float _cachedMultiplyR = 1.0f;
        private float _cachedMultiplyG = 1.0f;
        private float _cachedMultiplyB = 1.0f;
        private float _cachedMultiplyA = 1.0f;
        private int _cachedRenderTargetMaxSize = DefaultMaxRenderTargetSize;
        private float _cachedInternalRenderScale = DefaultInternalRenderScale;
        private bool _cachedEnableMsaa = true;
        private int _cachedPreferredMsaaSampleCount = 4;
        private bool _needsWarmupRender = true;
        private int _consecutiveRenderFailures;
        private DateTime _renderDisabledUntilUtc = DateTime.MinValue;
        private int _localDeviceGeneration = -1;
        private readonly Dictionary<string, double> _hitAreaActiveSinceSeconds = [];
        private string _lastShownModelWarningKey = string.Empty;
        private string _lastShownModelLoadErrorKey = string.Empty;

        private readonly record struct NativeLogSnapshot(string Path, int LineCount);
        private static readonly StandardAnchorCandidate[] AnchorCandidatesX =
        [
            new(Live2DManager.ParamEyeBallX, 0.18f),
            new(Live2DManager.ParamAngleX, 0.14f),
            new(Live2DManager.ParamBodyAngleX, 0.10f),
            new(Live2DManager.ParamBaseX, 0.25f),
        ];
        private static readonly StandardAnchorCandidate[] AnchorCandidatesY =
        [
            new(Live2DManager.ParamEyeBallY, 0.16f),
            new(Live2DManager.ParamAngleY, 0.12f),
            new(Live2DManager.ParamBodyAngleY, 0.08f),
            new(Live2DManager.ParamBaseY, 0.22f),
        ];
        private static readonly Vector2 FixedLookOrigin = Vector2.Zero;

        public Live2DTachieSource(IGraphicsDevicesAndContext devices)
        {
            _devices = devices ?? throw new ArgumentNullException(nameof(devices));
            _instanceId = System.Threading.Interlocked.Increment(ref _nextInstanceId);
            System.Threading.Interlocked.Increment(ref _activeSourceCount);
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                var manager = Live2DManager.GetInstance();
                if (manager == null)
                {
                    Commons.ConsoleManager.Error(Translate.Log_Live2DManagerNull);
                    return;
                }

                manager.Initialize();
                lock (_sharedRendererLock)
                {
                    _sharedRenderer ??= new Live2DRenderer();
                    _renderer = _sharedRenderer;
                }

                EnsureFallbackOutputBitmap();
                _transformEffect = (ID2D1Effect)_devices.DeviceContext.CreateEffect(EffectGuids.AffineTransform2D);
                _outputImage = _transformEffect.Output;
                ClearTransformEffectInput();
            }
            catch (Exception ex)
            {
                Commons.ConsoleManager.Error(string.Format(Translate.Log_Live2DInitializationError, ex.Message));
            }
        }
        private bool TryInitializeD3D11Device()
        {
            try
            {
                var d3d11Device = _devices.D3D.Device;
                var d3d11Context = _devices.D3D.DeviceContext;
                var devicePtr = d3d11Device.NativePointer;
                var contextPtr = d3d11Context.NativePointer;

                if (_d3d11Initialized &&
                    _localDeviceGeneration == _sharedDeviceGeneration &&
                    _sharedDevicePtr == devicePtr &&
                    _sharedContextPtr == contextPtr &&
                    _renderer != null)
                {
                    return true;
                }

                // QueryInterface<ID3D11Multithread>() raises a first-chance SharpGenException
                // on some environments even when it is handled successfully. YMM4 owns the D3D11
                // device/context, so avoid probing this optional interface here to keep startup noise down.

                lock (_drawLock)
                {
                    if (_sharedDevicePtr != devicePtr || _sharedContextPtr != contextPtr)
                    {
                        ResetSharedNativeResources();
                        _sharedDevicePtr = devicePtr;
                        _sharedContextPtr = contextPtr;
                        _sharedDeviceGeneration++;
                    }

                    if (_localDeviceGeneration != _sharedDeviceGeneration)
                    {
                        ResetRenderResources();
                        _model?.Dispose();
                        _model = null;
                        _currentModelPath = string.Empty;
                        _localDeviceGeneration = _sharedDeviceGeneration;
                    }

                    Live2DManager.GetInstance().SetD3D11Device(devicePtr, contextPtr);
                    lock (_sharedRendererLock)
                    {
                        _sharedRenderer ??= new Live2DRenderer();
                        _sharedRenderer.Initialize(devicePtr, contextPtr);
                        _renderer = _sharedRenderer;
                    }
                }

                _d3d11Initialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Commons.ConsoleManager.Error(string.Format(Translate.Log_D3D11InitializationFailed, ex.Message));
                return false;
            }
        }
        public void Update(TachieSourceDescription desc)
        {
            if (_disposed) return;

            try
            {
                if (!TryInitializeD3D11Device())
                {
                    return;
                }

                var charParam = desc.Tachie?.CharacterParameter as Live2DCharacterParameter;
                var itemParam = desc.Tachie?.ItemParameter as Live2DItemParameter;
                var currentModelFile = charParam?.File ?? string.Empty;
                var facesSnapshot = SnapshotFacesOnUiThread(desc);
                var activeFace = GetActiveFace(facesSnapshot, desc.ItemPosition);
                var faceParam = activeFace.Face;

                SynchronizeModelFileOnUiThread(itemParam, facesSnapshot, currentModelFile);

                lock (_drawLock)
                {
                    Live2DInteractionStore.UpdateInteractionTarget(charParam?.InteractionLinkId, charParam?.InteractionDisplayName, currentModelFile);

                    if (charParam != null && !string.IsNullOrEmpty(charParam.File))
                    {
                        if (_currentModelPath != charParam.File)
                        {
                            if (File.Exists(charParam.File))
                            {
                                _model?.Dispose();
                                _model = null;
                                _currentModelPath = charParam.File;
                                var loadedModel = Live2DManager.GetInstance().CreateModel();
                                var nativeLogSnapshot = CaptureNativeLogSnapshot();
                                if (!loadedModel.LoadModel(_currentModelPath))
                                {
                                    var detail = loadedModel.LastErrorMessage;
                                    if (string.IsNullOrWhiteSpace(detail))
                                    {
                                        detail = Translate.Error_ModelFilesMayBeInvalid;
                                    }

                                    ShowModelLoadErrorIfNeeded(_currentModelPath, detail);

                                    loadedModel.Dispose();
                                    _currentModelPath = string.Empty;
                                }
                                else
                                {
                                    loadedModel.ResetAnimationState();
                                    loadedModel.Update(1.0f / 60.0f);
                                    loadedModel.CommitParameters();
                                    _model = loadedModel;
                                    ModelMetadataCatalog.UpdateFromModelPath(_currentModelPath);
                                    ShowModelWarningsIfNeeded(_currentModelPath, ExtractLoadWarningsFromNativeLog(nativeLogSnapshot));
                                    _lastShownModelLoadErrorKey = string.Empty;
                                    ClearInteractionState(charParam?.InteractionLinkId);
                                }
                                _hasLastItemFrame = false;
                                _needsInitialFrameUpdate = true;
                                _needsWarmupRender = true;
                                _appliedExpressionId = string.Empty;
                                _hasCachedCharacterSettings = false;
                                _hasCachedItemSettings = false;
                            }
                            else
                            {
                                _model?.Dispose();
                                _model = null;
                                _currentModelPath = string.Empty;
                                ClearInteractionState(charParam?.InteractionLinkId);
                                var missingModelPath = charParam?.File ?? string.Empty;
                                ShowModelLoadErrorIfNeeded(missingModelPath, Translate.Error_ModelPathCheck);
                            }
                        }
                    }
                    if (_model != null)
                    {
                        var deltaSeconds = GetDeltaSeconds(desc, out var requiresReplay);
                        var itemTimeSeconds = Math.Max(0.0, desc.ItemPosition.Time.TotalSeconds);
                        if (requiresReplay)
                        {
                            _model.ResetAnimationState();
                            _model.ClearExpression();
                            ClearInteractionState(charParam?.InteractionLinkId);
                            _appliedExpressionId = string.Empty;
                            _hasCachedCharacterSettings = false;
                            _hasCachedItemSettings = false;
                        }

                        if (charParam != null)
                        {
                            var autoEyeBlink = charParam.AutoEyeBlink;
                            var eyeBlinkInterval = (float)charParam.EyeBlinkInterval;
                            var enablePhysics = charParam.EnablePhysics;
                            var enableBreath = charParam.EnableBreath;

                            if (!_hasCachedCharacterSettings || _cachedAutoEyeBlink != autoEyeBlink)
                            {
                                _model.SetEyeBlinkEnabled(autoEyeBlink);
                                _cachedAutoEyeBlink = autoEyeBlink;
                            }
                            if (!_hasCachedCharacterSettings || !NearlyEqual(_cachedEyeBlinkInterval, eyeBlinkInterval))
                            {
                                _model.SetEyeBlinkInterval(eyeBlinkInterval);
                                _cachedEyeBlinkInterval = eyeBlinkInterval;
                            }
                            _model.SetLipSyncEnabled(true);
                            if (!_hasCachedCharacterSettings || _cachedEnablePhysics != enablePhysics)
                            {
                                _model.SetPhysicsEnabled(enablePhysics);
                                _cachedEnablePhysics = enablePhysics;
                            }
                            if (!_hasCachedCharacterSettings || _cachedEnableBreath != enableBreath)
                            {
                                _model.SetBreathEnabled(enableBreath);
                                _cachedEnableBreath = enableBreath;
                            }

                            _cachedRenderTargetMaxSize = Math.Clamp(
                                charParam.RenderTargetMaxSize,
                                MinAllowedRenderTargetMaxSize,
                                MaxAllowedRenderTargetMaxSize);
                            _cachedInternalRenderScale = (float)Math.Clamp(
                                charParam.InternalRenderScale,
                                MinInternalRenderScale,
                                MaxInternalRenderScale);
                            _cachedEnableMsaa = charParam.EnableMsaa;
                            _cachedPreferredMsaaSampleCount = charParam.MsaaSamplePreset switch
                            {
                                Live2DMsaaSamplePreset.X2 => 2,
                                Live2DMsaaSamplePreset.X4 => 4,
                                _ => 4,
                            };

                            _hasCachedCharacterSettings = true;
                        }
                        else
                        {
                            _cachedRenderTargetMaxSize = DefaultMaxRenderTargetSize;
                            _cachedInternalRenderScale = DefaultInternalRenderScale;
                            _cachedEnableMsaa = true;
                            _cachedPreferredMsaaSampleCount = 4;
                        }

                        var activeHitArea = GetActiveHitAreaReaction(charParam?.InteractionLinkId, itemTimeSeconds, out var interactionMotionTimeSeconds);
                        var activeHitAreaForPhysics = GetActiveHitAreaPhysicsSource(charParam?.InteractionLinkId);
                        var resolvedExpressionId = ResolveExpressionId(
                            itemParam,
                            faceParam,
                            activeHitArea?.ExpressionId);
                        if (!string.IsNullOrWhiteSpace(resolvedExpressionId))
                        {
                            if (!string.Equals(_appliedExpressionId, resolvedExpressionId, StringComparison.Ordinal))
                            {
                                _model.SetExpression(resolvedExpressionId);
                                _appliedExpressionId = resolvedExpressionId;
                            }
                        }
                        else if (!string.IsNullOrEmpty(_appliedExpressionId))
                        {
                            _model.ClearExpression();
                            _appliedExpressionId = string.Empty;
                        }

                        TachieMotionEvaluator.UpdateMotionToCurrentTime(
                            _model,
                            desc,
                            _currentModelPath,
                            faceParam,
                            itemParam,
                            Math.Max(activeFace.RelativeTimeSeconds, (float)itemTimeSeconds),
                            (float)(charParam?.LipSyncGain ?? 1.0),
                            charParam?.LipSyncVowelsOnly ?? false,
                            activeHitArea?.MotionGroup,
                            activeHitArea?.MotionIndex ?? -1,
                            interactionMotionTimeSeconds);

                        var transformPositionX = 0.0f;
                        var transformPositionY = 0.0f;
                        var transformScale = 1.0f;
                        var transformRotation = 0.0f;
                        var screenWidth = Math.Max(1, desc.ScreenSize.Width);
                        var screenHeight = Math.Max(1, desc.ScreenSize.Height);

                        var finalVisible = true;
                        const float finalOpacity = 1.0f;
                        if (itemParam != null)
                        {
                            var frame = desc.ItemPosition.Frame;
                            var length = desc.ItemDuration.Frame;
                            var fps = desc.FPS;
                            var multiplyR = itemParam.MultiplyR.GetValue(frame, length, fps);
                            var multiplyG = itemParam.MultiplyG.GetValue(frame, length, fps);
                            var multiplyB = itemParam.MultiplyB.GetValue(frame, length, fps);
                            var multiplyA = itemParam.MultiplyA.GetValue(frame, length, fps);
                            finalVisible = !itemParam.IsHidden && !(faceParam?.IsHidden ?? false);
                            var mR = (float)multiplyR / 100.0f;
                            var mG = (float)multiplyG / 100.0f;
                            var mB = (float)multiplyB / 100.0f;
                            var mA = (float)multiplyA / 100.0f;

                            if (!_hasCachedItemSettings ||
                                !NearlyEqual(_cachedMultiplyR, mR) ||
                                !NearlyEqual(_cachedMultiplyG, mG) ||
                                !NearlyEqual(_cachedMultiplyB, mB) ||
                                !NearlyEqual(_cachedMultiplyA, mA))
                            {
                                _model.ApplyItemParameters(finalOpacity, mR, mG, mB, mA);
                                _cachedMultiplyR = mR;
                                _cachedMultiplyG = mG;
                                _cachedMultiplyB = mB;
                                _cachedMultiplyA = mA;
                                _hasCachedItemSettings = true;
                            }

                            var itemPosition = InteractionShapeTransform.PixelToTranslation(
                                new Vector2(
                                    (float)itemParam.PositionX.GetValue(frame, length, fps),
                                    (float)itemParam.PositionY.GetValue(frame, length, fps)),
                                screenWidth,
                                screenHeight);
                            transformPositionX = itemPosition.X;
                            transformPositionY = itemPosition.Y;
                            transformScale = (float)itemParam.Scale.GetValue(frame, length, fps) / 100.0f;
                            transformRotation = (float)itemParam.Rotation.GetValue(frame, length, fps);
                        }

                        if (faceParam != null)
                        {
                            var faceFrame = Math.Max(0L, (long)Math.Round(activeFace.LocalFrame));
                            var faceLength = Math.Max(1L, (long)Math.Round(activeFace.DurationFrame));
                            var fpsForFace = desc.FPS;
                            if (itemParam == null)
                            {
                                finalVisible = !faceParam.IsHidden;
                            }

                            var faceOffset = InteractionShapeTransform.PixelToTranslation(
                                new Vector2(
                                    (float)faceParam.OffsetPositionX.GetValue(faceFrame, faceLength, fpsForFace),
                                    (float)faceParam.OffsetPositionY.GetValue(faceFrame, faceLength, fpsForFace)),
                                screenWidth,
                                screenHeight);
                            transformPositionX += faceOffset.X;
                            transformPositionY += faceOffset.Y;
                            transformScale *= (float)faceParam.OffsetScale.GetValue(faceFrame, faceLength, fpsForFace) / 100.0f;
                            transformRotation += (float)faceParam.OffsetRotation.GetValue(faceFrame, faceLength, fpsForFace);
                        }

                        _model.SetPosition(transformPositionX, transformPositionY);
                        _model.SetScale(Math.Max(0.01f, transformScale));
                        _model.SetRotation(transformRotation);
                        var hitTestTransform = ModelMetadataCatalog.GetHitTestTransform(
                            _currentModelPath,
                            _model.GetCanvasWidth(),
                            _model.GetCanvasHeight());
                        Live2DInteractionStore.UpdateInteractionTransform(
                            charParam?.InteractionLinkId,
                            transformPositionX,
                            transformPositionY,
                            Math.Max(0.01f, transformScale),
                            transformRotation,
                            0.0f,
                            0.0f,
                            hitTestTransform.ScaleX,
                            hitTestTransform.ScaleY,
                            hitTestTransform.TranslateX,
                            hitTestTransform.TranslateY,
                            screenWidth,
                            screenHeight);
                        Live2DInteractionStore.UpdateInteractionHitAreas(
                            charParam?.InteractionLinkId,
                            _model.GetHitAreas().Select(x => new Live2DInteractionStore.InteractionHitAreaState(
                                x?.Id ?? string.Empty,
                                x?.Name ?? string.Empty,
                                x?.X ?? 0.0f,
                                x?.Y ?? 0.0f,
                                x?.Width ?? 0.0f,
                                x?.Height ?? 0.0f)));

                        ApplyInteractionPhysicsInput(_model, charParam, activeHitAreaForPhysics, (float)itemTimeSeconds, deltaSeconds);

                        if (requiresReplay)
                        {
                            ReplayModelToCurrentTime(
                                _model,
                                desc,
                                _currentModelPath,
                                faceParam,
                                itemParam,
                                Math.Max(activeFace.RelativeTimeSeconds, (float)Math.Max(0.0, desc.ItemPosition.Time.TotalSeconds)),
                                activeFace.DurationFrame,
                                (float)(charParam?.LipSyncGain ?? 1.0));
                        }
                        else
                        {
                            _model.UpdatePrePhysics(deltaSeconds);
                            _model.UpdatePostPhysics(deltaSeconds);
                        }
                        if (itemParam != null)
                        {
                            var itemFrame = Math.Max(0L, Convert.ToInt64(desc.ItemPosition.Frame));
                            var itemLength = Math.Max(1L, Convert.ToInt64(desc.ItemDuration.Frame));
                            TachieMotionEvaluator.ApplyDynamicItemParameters(
                                _model,
                                itemParam,
                                itemFrame,
                                itemLength,
                                desc.FPS);
                            TachieMotionEvaluator.ApplyDynamicItemParts(
                                _model,
                                itemParam,
                                itemFrame,
                                itemLength,
                                desc.FPS);
                        }
                        if (faceParam != null)
                        {
                            var faceFrame = Math.Max(0L, (long)Math.Round(activeFace.LocalFrame));
                            var faceLength = Math.Max(1L, (long)Math.Round(activeFace.DurationFrame));
                            TachieMotionEvaluator.ApplyFaceAndLipSync(
                                _model,
                                desc,
                                _currentModelPath,
                                faceParam,
                                faceFrame,
                                faceLength,
                                (float)(charParam?.LipSyncGain ?? 1.0),
                                charParam?.LipSyncVowelsOnly ?? false);
                            TachieMotionEvaluator.ApplyDynamicFaceParts(
                                _model,
                                faceParam,
                                faceFrame,
                                faceLength,
                                desc.FPS);
                        }
                        else
                        {
                            TachieMotionEvaluator.ApplyItemLipSyncPostPhysics(
                                _model,
                                desc,
                                _currentModelPath,
                                (float)(charParam?.LipSyncGain ?? 1.0),
                                charParam?.LipSyncVowelsOnly ?? false);
                        }
                        ApplyHitAreaDynamicOverrides(_model, activeHitArea);
                        _model.CommitParameters();
                        UpdateHitAreaResults(_model, charParam?.InteractionLinkId, itemTimeSeconds);

                        var canDraw = desc.ScreenSize.Width > 0 &&
                                      desc.ScreenSize.Height > 0 &&
                                      finalVisible;
                        if (Render(
                            desc.ScreenSize.Width,
                            desc.ScreenSize.Height,
                            canDraw,
                            _cachedRenderTargetMaxSize,
                            _cachedInternalRenderScale,
                            _cachedEnableMsaa,
                            _cachedPreferredMsaaSampleCount))
                        {
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Commons.ConsoleManager.Error(string.Format(Translate.Log_Live2DUpdateError, ex.Message));
            }
        }
        internal static (Live2DFaceParameter? Face, float RelativeTimeSeconds, double LocalFrame, double DurationFrame) ResolveActiveFace(
            IEnumerable<TachieFaceDescription> faces,
            YukkuriMovieMaker.Player.Video.FrameTime currentPosition)
        {
            Live2DFaceParameter? activeFace = null;
            int activeLayer = int.MinValue;
            TimeSpan activeLocalTime = TimeSpan.Zero;
            double activeLocalFrame = 0.0;
            double activeDurationFrame = 1.0;

            foreach (var face in faces)
            {
                var durationFrame = Math.Max(1.0, (double)face.ItemDuration.Frame);
                var localTime = face.ItemPosition.Time;
                var localFrame = Math.Clamp((double)face.ItemPosition.Frame, 0.0, durationFrame);
                if ((face.Layer > activeLayer || (face.Layer == activeLayer && localTime >= activeLocalTime)))
                {
                    activeFace = face.FaceParameter as Live2DFaceParameter;
                    activeLayer = face.Layer;
                    activeLocalTime = localTime;
                    activeLocalFrame = localFrame;
                    activeDurationFrame = durationFrame;
                }
            }

            if (activeFace != null)
            {
                return (activeFace, (float)Math.Max(0.0, activeLocalTime.TotalSeconds), activeLocalFrame, activeDurationFrame);
            }

            return (null, 0.0f, 0.0, 1.0);
        }

        private static (Live2DFaceParameter? Face, float RelativeTimeSeconds, double LocalFrame, double DurationFrame) GetActiveFace(
            IReadOnlyList<TachieFaceDescription> faces,
            YukkuriMovieMaker.Player.Video.FrameTime currentPosition)
        {
            if (faces.Count == 0)
            {
                return (null, 0.0f, 0.0, 1.0);
            }

            return ResolveActiveFace(faces, currentPosition);
        }

        private static TachieFaceDescription[] SnapshotFacesOnUiThread(TachieSourceDescription desc)
        {
            if (desc.Tachie?.Faces is not { } faces)
            {
                return [];
            }

            return InvokeOnUiThread(() => faces.ToArray());
        }

        private static void SynchronizeModelFileOnUiThread(
            Live2DItemParameter? itemParam,
            IReadOnlyList<TachieFaceDescription> faces,
            string currentModelFile)
        {
            var shouldSyncItem = itemParam != null && !string.Equals(itemParam.ModelFile, currentModelFile, StringComparison.OrdinalIgnoreCase);
            var shouldSyncFaces = faces.Any(face =>
                face.FaceParameter is Live2DFaceParameter live2DFace &&
                !string.Equals(live2DFace.ModelFile, currentModelFile, StringComparison.OrdinalIgnoreCase));

            if (!shouldSyncItem && !shouldSyncFaces)
            {
                return;
            }

            InvokeOnUiThread(() =>
            {
                if (itemParam != null && !string.Equals(itemParam.ModelFile, currentModelFile, StringComparison.OrdinalIgnoreCase))
                {
                    itemParam.ModelFile = currentModelFile;
                }

                foreach (var face in faces)
                {
                    if (face.FaceParameter is Live2DFaceParameter live2DFace &&
                        !string.Equals(live2DFace.ModelFile, currentModelFile, StringComparison.OrdinalIgnoreCase))
                    {
                        live2DFace.ModelFile = currentModelFile;
                    }
                }
            });
        }

        private static T InvokeOnUiThread<T>(Func<T> action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                return action();
            }

            return dispatcher.Invoke(action);
        }

        private static void InvokeOnUiThread(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.Invoke(action);
        }

        private static string ResolveExpressionId(
            Live2DItemParameter? itemParam,
            Live2DFaceParameter? faceParam,
            string? interactionExpressionId)
        {
            if (!string.IsNullOrWhiteSpace(interactionExpressionId))
            {
                return interactionExpressionId;
            }

            if (!string.IsNullOrWhiteSpace(faceParam?.ExpressionId))
            {
                return faceParam.ExpressionId;
            }

            if (!string.IsNullOrWhiteSpace(itemParam?.ExpressionId))
            {
                return itemParam.ExpressionId;
            }

            return string.Empty;
        }

        private static void ApplyHitAreaDynamicOverrides(
            Live2DModelWrapper model,
            Live2DInteractionStore.HitAreaRectState? activeHitArea)
        {
            if (activeHitArea == null)
            {
                return;
            }

            foreach (var row in activeHitArea.ParameterOverrides)
            {
                if (string.IsNullOrWhiteSpace(row.Id))
                {
                    continue;
                }

                model.SetParameterValue(row.Id, row.Value);
            }

            foreach (var row in activeHitArea.PartOverrides)
            {
                if (string.IsNullOrWhiteSpace(row.Id))
                {
                    continue;
                }

                model.SetPartOpacity(row.Id, Math.Clamp(row.Opacity, 0.0f, 1.0f));
            }
        }

        private float GetDeltaSeconds(TachieSourceDescription desc, out bool rewound)
        {
            rewound = false;

            var safeFps = Math.Max(desc.FPS, 1.0);
            var targetFrame = (double)desc.ItemPosition.Frame;

            if (!_hasLastItemFrame)
            {
                _lastItemFrame = targetFrame;
                _hasLastItemFrame = true;
                _needsInitialFrameUpdate = false;
                return 0.0f;
            }

            var deltaFrames = targetFrame - _lastItemFrame;
            _lastItemFrame = targetFrame;

            if (RequiresReplayForFrameJump(deltaFrames))
            {
                rewound = true;
                _needsInitialFrameUpdate = false;
                return 0.0f;
            }

            if (deltaFrames == 0.0 && _needsInitialFrameUpdate)
            {
                _needsInitialFrameUpdate = false;
                return 0.0f;
            }

            var deltaSeconds = deltaFrames / safeFps;
            if (deltaSeconds <= 0.0)
            {
                return 0.0f;
            }

            return (float)Math.Min(deltaSeconds, 0.25);
        }

        internal static bool RequiresReplayForFrameJump(double deltaFrames)
        {
            return deltaFrames < 0.0 || deltaFrames > 1.0;
        }

        private void ApplyInteractionPhysicsInput(
            Live2DModelWrapper model,
            Live2DCharacterParameter? charParam,
            Live2DInteractionStore.HitAreaRectState? activeHitArea,
            float timeSeconds,
            float deltaTime)
        {
            var physicsScale = ConvertPhysicsStrengthToScale(charParam?.PhysicsStrength ?? 50.0);
            model.SetPhysicsOutputScale(physicsScale);

            var wind = ResolvePhysicsWindVector(charParam?.WindStrength ?? 0.0, timeSeconds);
            model.SetPhysicsWind(wind.X, wind.Y);

            var drag = ResolveInteractionDrag(charParam, activeHitArea);
            model.SetDragging(drag.X, drag.Y);
        }

        private void ShowModelWarningsIfNeeded(string? modelPath, IEnumerable<string>? warnings)
        {
            var warningList = warnings?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? [];
            if (warningList.Length == 0)
            {
                _lastShownModelWarningKey = string.Empty;
                return;
            }

            var warningKey = $"{modelPath}|{string.Join("\n", warningList)}";
            if (string.Equals(_lastShownModelWarningKey, warningKey, StringComparison.Ordinal))
            {
                return;
            }

            _lastShownModelWarningKey = warningKey;
            Commons.ConsoleManager.Error(
                $"{Translate.Error_ModelLoadWarnings_Title}\n\n{Translate.Error_TargetPath_Label}: {modelPath}\n\n- {string.Join("\n- ", warningList)}");
        }

        private void ShowModelLoadErrorIfNeeded(string? modelPath, string detail)
        {
            var safePath = modelPath ?? string.Empty;
            var safeDetail = string.IsNullOrWhiteSpace(detail)
                ? Translate.Error_CheckModelFiles
                : detail.Trim();
            if (ModelMetadataCatalog.WasSelectionIssueShown(safePath, safeDetail))
            {
                _lastShownModelLoadErrorKey = $"{safePath}|{safeDetail}";
                return;
            }

            var errorKey = $"{safePath}|{safeDetail}";
            if (string.Equals(_lastShownModelLoadErrorKey, errorKey, StringComparison.Ordinal))
            {
                return;
            }

            _lastShownModelLoadErrorKey = errorKey;
            Commons.ConsoleManager.Error(
                $"{Translate.Error_ModelLoadFailed_Title}\n\n{Translate.Error_TargetPath_Label}: {safePath}\n\n{safeDetail}\n\n{Translate.Error_CheckModelFiles}");
        }

        private static NativeLogSnapshot CaptureNativeLogSnapshot()
        {
            foreach (var path in GetNativeLogPathCandidates())
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    return new NativeLogSnapshot(path, File.ReadLines(path).Count());
                }
                catch
                {
                }
            }

            var fallback = GetNativeLogPathCandidates().FirstOrDefault() ?? Path.Combine(AppContext.BaseDirectory, "VTuberKitForNative.log");
            return new NativeLogSnapshot(fallback, 0);
        }

        private static IReadOnlyList<string> ExtractLoadWarningsFromNativeLog(NativeLogSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(snapshot.Path) || !File.Exists(snapshot.Path))
            {
                return [];
            }

            try
            {
                var addedLines = File.ReadLines(snapshot.Path)
                    .Skip(Math.Max(0, snapshot.LineCount))
                    .Select(x =>
                    {
                        var closingBracket = x.IndexOf("] ", StringComparison.Ordinal);
                        return closingBracket >= 0 ? x[(closingBracket + 2)..] : x;
                    })
                    .Where(IsLoadWarningLine)
                    .ToArray();

                return addedLines;
            }
            catch
            {
                return [];
            }
        }

        private static bool IsLoadWarningLine(string line)
        {
            return line.StartsWith("Skipped expression ", StringComparison.Ordinal) ||
                   line.StartsWith("Skipped motion ", StringComparison.Ordinal) ||
                   line.StartsWith("Skipped physics file", StringComparison.Ordinal) ||
                   line.StartsWith("Skipped pose file", StringComparison.Ordinal) ||
                   line.StartsWith("Skipped user data file", StringComparison.Ordinal) ||
                   line.StartsWith("Failed to create motion: ", StringComparison.Ordinal) ||
                   line.StartsWith("Failed to load motion file: ", StringComparison.Ordinal) ||
                   line.StartsWith("Failed to create decoder for: ", StringComparison.Ordinal);
        }

        private static IEnumerable<string> GetNativeLogPathCandidates()
        {
            yield return Path.Combine(Environment.CurrentDirectory, "VTuberKitForNative.log");

            var appBase = AppContext.BaseDirectory;
            if (!string.IsNullOrWhiteSpace(appBase))
            {
                yield return Path.Combine(appBase, "VTuberKitForNative.log");
            }
        }
        private void ReplayModelToCurrentTime(
            Live2DModelWrapper model,
            TachieSourceDescription desc,
            string? modelPath,
            Live2DFaceParameter? faceParam,
            Live2DItemParameter? itemParam,
            float activeFaceTimeSeconds,
            double activeFaceDurationFrame,
            float lipSyncGain)
        {
            var targetSeconds = (float)Math.Max(0.0, desc.ItemPosition.Time.TotalSeconds);
            if (targetSeconds <= 0.0f)
            {
                return;
            }

            var replayStep = Math.Max(
                (float)(1.0 / Math.Max(60.0, desc.FPS)),
                targetSeconds / MaxReplaySteps);
            var elapsed = 0.0f;

            while (elapsed < targetSeconds)
            {
                var next = Math.Min(targetSeconds, elapsed + replayStep);
                var delta = next - elapsed;

                TachieMotionEvaluator.UpdateMotionToCurrentTime(
                    model,
                    desc,
                    modelPath,
                    faceParam,
                    itemParam,
                    Math.Min(activeFaceTimeSeconds, next),
                    lipSyncGain,
                    (desc.Tachie?.CharacterParameter as Live2DCharacterParameter)?.LipSyncVowelsOnly ?? false,
                    null,
                    -1,
                    0.0f,
                    next);

                ApplyInteractionPhysicsInput(
                    model,
                    desc.Tachie?.CharacterParameter as Live2DCharacterParameter,
                    null,
                    next,
                    delta);
                model.UpdatePrePhysics(delta);
                model.UpdatePostPhysics(delta);
                if (itemParam != null)
                {
                    var itemFrame = Math.Clamp((long)Math.Round(next * desc.FPS), 0L, Math.Max(1L, desc.ItemDuration.Frame));
                    var itemLength = Math.Max(1L, desc.ItemDuration.Frame);
                    TachieMotionEvaluator.ApplyDynamicItemParameters(
                        model,
                        itemParam,
                        itemFrame,
                        itemLength,
                        desc.FPS);
                    TachieMotionEvaluator.ApplyDynamicItemParts(
                        model,
                        itemParam,
                        itemFrame,
                        itemLength,
                        desc.FPS);
                }
                if (faceParam != null)
                {
                    var faceFrame = Math.Clamp((long)Math.Round(Math.Min(activeFaceTimeSeconds, next) * desc.FPS), 0L, Math.Max(1L, (long)Math.Round(activeFaceDurationFrame)));
                    var faceLength = Math.Max(1L, (long)Math.Round(activeFaceDurationFrame));
                    TachieMotionEvaluator.ApplyFaceAndLipSync(
                        model,
                        desc,
                        modelPath,
                        faceParam,
                        faceFrame,
                        faceLength,
                        lipSyncGain,
                        (desc.Tachie?.CharacterParameter as Live2DCharacterParameter)?.LipSyncVowelsOnly ?? false);
                    TachieMotionEvaluator.ApplyDynamicFaceParts(
                        model,
                        faceParam,
                        faceFrame,
                        faceLength,
                        desc.FPS);
                }
                else
                {
                    TachieMotionEvaluator.ApplyItemLipSyncPostPhysics(
                        model,
                        desc,
                        modelPath,
                        lipSyncGain,
                        (desc.Tachie?.CharacterParameter as Live2DCharacterParameter)?.LipSyncVowelsOnly ?? false);
                }
                elapsed = next;
            }
        }

        private static bool NearlyEqual(float a, float b, float epsilon = 0.0001f)
        {
            return Math.Abs(a - b) <= epsilon;
        }

        private void UpdateHitAreaResults(Live2DModelWrapper model, string? linkId, double itemTimeSeconds)
        {
            foreach (var hitArea in Live2DInteractionStore.GetHitAreaRects(linkId))
            {
                if (string.IsNullOrWhiteSpace(hitArea.HitAreaName))
                {
                    Live2DInteractionStore.SetHitAreaResult(hitArea.SourceId, false);
                    continue;
                }

                var centerX = hitArea.X;
                var centerY = hitArea.Y;
                var left = centerX - hitArea.Width / 2.0f;
                var right = centerX + hitArea.Width / 2.0f;
                var top = centerY + hitArea.Height / 2.0f;
                var bottom = centerY - hitArea.Height / 2.0f;

                var center = TransformHitTestPoint(linkId, centerX, centerY);
                var topLeft = TransformHitTestPoint(linkId, left, top);
                var topRight = TransformHitTestPoint(linkId, right, top);
                var bottomLeft = TransformHitTestPoint(linkId, left, bottom);
                var bottomRight = TransformHitTestPoint(linkId, right, bottom);

                var isHit =
                    model.HitTest(hitArea.HitAreaName, center.X, center.Y) ||
                    model.HitTest(hitArea.HitAreaName, topLeft.X, topLeft.Y) ||
                    model.HitTest(hitArea.HitAreaName, topRight.X, topRight.Y) ||
                    model.HitTest(hitArea.HitAreaName, bottomLeft.X, bottomLeft.Y) ||
                    model.HitTest(hitArea.HitAreaName, bottomRight.X, bottomRight.Y);

                var wasHit = hitArea.IsHit;
                Live2DInteractionStore.SetHitAreaResult(hitArea.SourceId, isHit);
                if (!wasHit && isHit)
                {
                    _hitAreaActiveSinceSeconds[hitArea.SourceId] = itemTimeSeconds;
                }
                else if (isHit && !_hitAreaActiveSinceSeconds.ContainsKey(hitArea.SourceId))
                {
                    _hitAreaActiveSinceSeconds[hitArea.SourceId] = itemTimeSeconds;
                }
                else if (wasHit && !isHit)
                {
                    _hitAreaActiveSinceSeconds.Remove(hitArea.SourceId);
                }
            }
        }

        private Live2DInteractionStore.HitAreaRectState? GetActiveHitAreaReaction(string? linkId, double itemTimeSeconds, out float motionTimeSeconds)
        {
            motionTimeSeconds = 0.0f;
            PruneInactiveHitAreaState(linkId);
            var activeHitArea = Live2DInteractionStore.GetPreferredHitAreaReaction(
                linkId,
                sourceId => _hitAreaActiveSinceSeconds.TryGetValue(sourceId, out var activeSinceSeconds)
                    ? activeSinceSeconds
                    : null);

            if (activeHitArea is null)
            {
                return null;
            }

            if (_hitAreaActiveSinceSeconds.TryGetValue(activeHitArea.SourceId, out var activeSinceSeconds))
            {
                motionTimeSeconds = (float)Math.Max(0.0, itemTimeSeconds - activeSinceSeconds);
            }

            return activeHitArea;
        }

        private Live2DInteractionStore.HitAreaRectState? GetActiveHitAreaPhysicsSource(string? linkId)
        {
            PruneInactiveHitAreaState(linkId);

            return Live2DInteractionStore.GetHitAreaRects(linkId)
                .Where(x => x.IsHit)
                .Select(x => new
                {
                    HitArea = x,
                    ActiveSinceSeconds = _hitAreaActiveSinceSeconds.TryGetValue(x.SourceId, out var activeSinceSeconds)
                        ? activeSinceSeconds
                        : double.MinValue
                })
                .OrderByDescending(x => x.HitArea.Layer)
                .ThenByDescending(x => x.ActiveSinceSeconds)
                .ThenBy(x => x.HitArea.SourceId, StringComparer.Ordinal)
                .Select(x => x.HitArea)
                .FirstOrDefault();
        }

        private static Vector2 ResolveInteractionDrag(
            Live2DCharacterParameter? charParam,
            Live2DInteractionStore.HitAreaRectState? activeHitArea)
        {
            var rawTarget = FixedLookOrigin;
            Live2DInteractionStore.InteractionTransformState? transformState = null;
            if (charParam != null)
            {
                Live2DInteractionStore.TryGetInteractionTransform(charParam.InteractionLinkId, out transformState);
            }
            else if (activeHitArea != null)
            {
                Live2DInteractionStore.TryGetInteractionTransform(activeHitArea.LinkId, out transformState);
            }

            if (charParam != null &&
                Live2DInteractionStore.TryGetTargetPoint(charParam.InteractionLinkId, out var targetX, out var targetY))
            {
                rawTarget += ConvertPixelOffsetToDragUnits(transformState, targetX, targetY);
            }

            var hitAreaStrength = (float)Math.Clamp((charParam?.HitAreaPhysicsStrength ?? 0.0) / 100.0, 0.0, 1.0);
            if (activeHitArea != null && hitAreaStrength > 0.0f)
            {
                rawTarget += ConvertPixelOffsetToDragUnits(transformState, activeHitArea.X, activeHitArea.Y) * hitAreaStrength;
            }

            rawTarget = new Vector2(
                Math.Clamp(rawTarget.X - FixedLookOrigin.X, -1.0f, 1.0f),
                Math.Clamp(rawTarget.Y - FixedLookOrigin.Y, -1.0f, 1.0f));
            return rawTarget;
        }

        private static float ConvertPhysicsStrengthToScale(double physicsStrength)
        {
            return (float)Math.Clamp(physicsStrength / 50.0, 0.0, 2.0);
        }

        private static Vector2 ResolvePhysicsWindVector(double windStrength, float timeSeconds)
        {
            var normalizedStrength = (float)Math.Clamp(windStrength / 100.0, 0.0, 1.0);
            if (normalizedStrength <= 0.0f)
            {
                return Vector2.Zero;
            }

            var x =
                MathF.Sin(timeSeconds * 0.83f) * 0.55f +
                MathF.Sin(timeSeconds * 1.73f + 1.1f) * 0.30f;
            var y =
                MathF.Cos(timeSeconds * 0.41f + 0.35f) * 0.12f +
                MathF.Sin(timeSeconds * 1.07f + 2.3f) * 0.08f;

            return new Vector2(x, y) * normalizedStrength;
        }

        private static System.Numerics.Vector2 TransformHitTestPoint(string? linkId, float x, float y)
        {
            if (Live2DInteractionStore.TryGetInteractionTransform(linkId, out var state) && state is not null)
            {
                var localPoint = InteractionShapeTransform.PixelToLocal(new Vector2(x, y), state.ScreenWidth, state.ScreenHeight);
                return new Vector2(
                    (localPoint.X + state.ModelCenterX) * state.HitTestScaleX + state.HitTestTranslateX,
                    (localPoint.Y + state.ModelCenterY) * state.HitTestScaleY + state.HitTestTranslateY);
            }

            return Vector2.Zero;
        }

        private static Vector2 ConvertPixelOffsetToDragUnits(
            Live2DInteractionStore.InteractionTransformState? state,
            float x,
            float y)
        {
            if (state is null)
            {
                return Vector2.Zero;
            }

            return InteractionShapeTransform.PixelToLocal(new Vector2(x, y), state.ScreenWidth, state.ScreenHeight);
        }

        internal static Vector2 ResolveInteractionTrackingCenter(Live2DModelWrapper model, Live2DBounds modelBounds)
        {
            if (model == null)
            {
                return new Vector2(modelBounds.X, modelBounds.Y);
            }

            return ResolveInteractionTrackingCenter(
                model.GetParameters()
                    .Where(x => x != null)
                    .Cast<Live2DParameter>(),
                modelBounds);
        }

        internal static Vector2 ResolveInteractionTrackingCenter(IEnumerable<Live2DParameter> sourceParameters, Live2DBounds modelBounds)
        {
            var parameters = sourceParameters
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Id))
                .ToDictionary(x => x.Id!, StringComparer.OrdinalIgnoreCase);

            var offsetX = ResolveTrackingAxisOffset(parameters, AnchorCandidatesX, modelBounds.Width);
            var offsetY = ResolveTrackingAxisOffset(parameters, AnchorCandidatesY, modelBounds.Height);
            return new Vector2(modelBounds.X + offsetX, modelBounds.Y + offsetY);
        }

        private static float ResolveTrackingAxisOffset(
            IReadOnlyDictionary<string, Live2DParameter> parameters,
            IReadOnlyList<StandardAnchorCandidate> candidates,
            float extent)
        {
            var safeExtent = Math.Max(0.0f, extent);
            foreach (var candidate in candidates)
            {
                if (TryGetNormalizedParameterValue(parameters, candidate.ParameterId, out var normalized))
                {
                    return normalized * safeExtent * candidate.Influence;
                }
            }

            return 0.0f;
        }

        private static bool TryGetNormalizedParameterValue(
            IReadOnlyDictionary<string, Live2DParameter> parameters,
            string parameterId,
            out float normalized)
        {
            normalized = 0.0f;
            if (!parameters.TryGetValue(parameterId, out var parameter))
            {
                return false;
            }

            var positiveRange = parameter.Max - parameter.Default;
            var negativeRange = parameter.Default - parameter.Min;
            var current = parameter.Value - parameter.Default;
            var divisor = current >= 0.0f ? positiveRange : negativeRange;
            if (Math.Abs(divisor) < 1e-5f)
            {
                return false;
            }

            normalized = Math.Clamp(current / divisor, -1.0f, 1.0f);
            return true;
        }

        private void ClearInteractionState(string? linkId)
        {
            _hitAreaActiveSinceSeconds.Clear();
            Live2DInteractionStore.ClearHitAreaResults(linkId);
        }
        private void PruneInactiveHitAreaState(string? linkId)
        {
            if (string.IsNullOrWhiteSpace(linkId))
            {
                _hitAreaActiveSinceSeconds.Clear();
                return;
            }

            var activeSourceIds = Live2DInteractionStore.GetHitAreaRects(linkId)
                .Select(x => x.SourceId)
                .ToHashSet(StringComparer.Ordinal);

            if (activeSourceIds.Count == 0)
            {
                _hitAreaActiveSinceSeconds.Clear();
                return;
            }

            foreach (var sourceId in _hitAreaActiveSinceSeconds.Keys.ToArray())
            {
                if (!activeSourceIds.Contains(sourceId))
                {
                    _hitAreaActiveSinceSeconds.Remove(sourceId);
                }
            }
        }

        private (int Width, int Height) CalculateRenderTargetSize(int screenWidth, int screenHeight, int maxRenderTargetSize, float internalRenderScale)
        {
            var safeMax = Math.Clamp(maxRenderTargetSize, MinAllowedRenderTargetMaxSize, MaxAllowedRenderTargetMaxSize);
            var safeScale = Math.Clamp(internalRenderScale, MinInternalRenderScale, MaxInternalRenderScale);
            var scaledWidth = (int)Math.Ceiling(Math.Max(1, screenWidth) * safeScale);
            var scaledHeight = (int)Math.Ceiling(Math.Max(1, screenHeight) * safeScale);
            var renderWidth = Math.Clamp(scaledWidth, MinRenderTargetSize, safeMax);
            var renderHeight = Math.Clamp(scaledHeight, MinRenderTargetSize, safeMax);
            return (renderWidth, renderHeight);
        }

        private bool Render(int screenWidth, int screenHeight, bool drawModel, int maxRenderTargetSize, float internalRenderScale, bool enableMsaa, int preferredMsaaSampleCount)
        {
            if (_renderer == null || _model == null || _transformEffect == null)
            {
                return true;
            }

            if (screenWidth <= 0 || screenHeight <= 0)
            {
                return true;
            }

            if (!CanAttemptRender())
            {
                return true;
            }

            var (renderWidth, renderHeight) = CalculateRenderTargetSize(screenWidth, screenHeight, maxRenderTargetSize, internalRenderScale);
            lock (_drawLock)
            {
                var renderPhase = "begin";
                try
                {
                    if (_d3dTexture == null || _d3dTexture.Description.Width != renderWidth || _d3dTexture.Description.Height != renderHeight)
                    {
                        renderPhase = "allocate-render-target";
                        var tryWidth = renderWidth;
                        var tryHeight = renderHeight;
                        var allocated = false;

                        ID3D11Texture2D? newTexture = null;
                        ID3D11RenderTargetView? newRtv = null;
                        ID3D11Texture2D? newDepthTexture = null;
                        ID3D11DepthStencilView? newDsv = null;
                        ID3D11Texture2D? newMsaaTexture = null;
                        ID3D11RenderTargetView? newMsaaRtv = null;
                        ID3D11Texture2D? newMsaaDepthTexture = null;
                        ID3D11DepthStencilView? newMsaaDsv = null;
                        var newMsaaSampleCount = 1;
                        ID2D1Bitmap1? newBitmap = null;
                        ID2D1Bitmap1? newOutputBitmap = null;

                        while (tryWidth >= MinRenderTargetSize && tryHeight >= MinRenderTargetSize)
                        {
                            try
                            {
                                var resolvedPreferredMsaaSampleCount = preferredMsaaSampleCount >= 4 ? 4 : 2;
                                var sampleCounts = enableMsaa
                                    ? (resolvedPreferredMsaaSampleCount >= 4 ? new[] { 4, 2, 1 } : new[] { 2, 1 })
                                    : new[] { 1 };
                                foreach (var sampleCount in sampleCounts)
                                {
                                    try
                                    {
                                        var desc = new Texture2DDescription
                                        {
                                            Width = tryWidth,
                                            Height = tryHeight,
                                            MipLevels = 1,
                                            ArraySize = 1,
                                            Format = Format.R8G8B8A8_UNorm,
                                            SampleDescription = new SampleDescription(1, 0),
                                            Usage = ResourceUsage.Default,
                                            BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                                            CPUAccessFlags = CpuAccessFlags.None,
                                            MiscFlags = ResourceOptionFlags.None
                                        };

                                        newTexture = _devices.D3D.Device.CreateTexture2D(desc);
                                        newRtv = _devices.D3D.Device.CreateRenderTargetView(newTexture);

                                        using var dxgiSurface = newTexture.QueryInterface<Vortice.DXGI.IDXGISurface>();
                                        var props = new BitmapProperties1(
                                            new Vortice.DCommon.PixelFormat(Format.R8G8B8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                                            96, 96,
                                            BitmapOptions.Target);
                                        newBitmap = _devices.DeviceContext.CreateBitmapFromDxgiSurface(dxgiSurface, props);
                                        var outputProps = new BitmapProperties1(
                                            new Vortice.DCommon.PixelFormat(Format.R8G8B8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                                            96, 96,
                                            BitmapOptions.Target);
                                        newOutputBitmap = _devices.DeviceContext.CreateBitmap(
                                            new Vortice.Mathematics.SizeI(tryWidth, tryHeight),
                                            IntPtr.Zero,
                                            0,
                                            outputProps);

                                        var depthSampleCount = sampleCount;
                                        if (sampleCount > 1)
                                        {
                                            var msaaDesc = new Texture2DDescription
                                            {
                                                Width = tryWidth,
                                                Height = tryHeight,
                                                MipLevels = 1,
                                                ArraySize = 1,
                                                Format = Format.R8G8B8A8_UNorm,
                                                SampleDescription = new SampleDescription(sampleCount, 0),
                                                Usage = ResourceUsage.Default,
                                                BindFlags = BindFlags.RenderTarget,
                                                CPUAccessFlags = CpuAccessFlags.None,
                                                MiscFlags = ResourceOptionFlags.None
                                            };
                                            newMsaaTexture = _devices.D3D.Device.CreateTexture2D(msaaDesc);
                                            newMsaaRtv = _devices.D3D.Device.CreateRenderTargetView(newMsaaTexture);
                                        }

                                        var depthDesc = new Texture2DDescription
                                        {
                                            Width = tryWidth,
                                            Height = tryHeight,
                                            MipLevels = 1,
                                            ArraySize = 1,
                                            Format = Format.D24_UNorm_S8_UInt,
                                            SampleDescription = new SampleDescription(depthSampleCount, 0),
                                            Usage = ResourceUsage.Default,
                                            BindFlags = BindFlags.DepthStencil,
                                            CPUAccessFlags = CpuAccessFlags.None,
                                            MiscFlags = ResourceOptionFlags.None
                                        };
                                        if (sampleCount > 1)
                                        {
                                            newMsaaDepthTexture = _devices.D3D.Device.CreateTexture2D(depthDesc);
                                            newMsaaDsv = _devices.D3D.Device.CreateDepthStencilView(newMsaaDepthTexture);
                                        }
                                        else
                                        {
                                            newDepthTexture = _devices.D3D.Device.CreateTexture2D(depthDesc);
                                            newDsv = _devices.D3D.Device.CreateDepthStencilView(newDepthTexture);
                                        }

                                        newMsaaSampleCount = sampleCount;
                                        break;
                                    }
                                    catch
                                    {
                                        newTexture?.Dispose();
                                        newTexture = null;
                                        newRtv?.Dispose();
                                        newRtv = null;
                                        newBitmap?.Dispose();
                                        newBitmap = null;
                                        newOutputBitmap?.Dispose();
                                        newOutputBitmap = null;
                                        newMsaaTexture?.Dispose();
                                        newMsaaTexture = null;
                                        newMsaaRtv?.Dispose();
                                        newMsaaRtv = null;
                                        newMsaaDepthTexture?.Dispose();
                                        newMsaaDepthTexture = null;
                                        newMsaaDsv?.Dispose();
                                        newMsaaDsv = null;
                                        newDepthTexture?.Dispose();
                                        newDepthTexture = null;
                                        newDsv?.Dispose();
                                        newDsv = null;
                                    }
                                }

                                if (newMsaaSampleCount <= 1 && (newDepthTexture == null || newDsv == null))
                                {
                                    throw new InvalidOperationException("Failed to create depth resources for non-MSAA path.");
                                }

                                if (newMsaaSampleCount > 1 && (newMsaaTexture == null || newMsaaRtv == null || newMsaaDepthTexture == null || newMsaaDsv == null))
                                {
                                    throw new InvalidOperationException("Failed to create MSAA resources.");
                                }

                                allocated = true;
                                break;
                            }
                            catch (Exception)
                            {
                                newTexture?.Dispose();
                                newTexture = null;
                                newRtv?.Dispose();
                                newRtv = null;
                                newDepthTexture?.Dispose();
                                newDepthTexture = null;
                                newDsv?.Dispose();
                                newDsv = null;
                                newMsaaTexture?.Dispose();
                                newMsaaTexture = null;
                                newMsaaRtv?.Dispose();
                                newMsaaRtv = null;
                                newMsaaDepthTexture?.Dispose();
                                newMsaaDepthTexture = null;
                                newMsaaDsv?.Dispose();
                                newMsaaDsv = null;
                                newBitmap?.Dispose();
                                newBitmap = null;
                                newOutputBitmap?.Dispose();
                                newOutputBitmap = null;

                                if (tryWidth == MinRenderTargetSize && tryHeight == MinRenderTargetSize)
                                {
                                    break;
                                }

                                tryWidth = Math.Max(MinRenderTargetSize, tryWidth / 2);
                                tryHeight = Math.Max(MinRenderTargetSize, tryHeight / 2);
                            }
                        }

                        if (!allocated || newTexture == null || newRtv == null || newBitmap == null || newOutputBitmap == null)
                        {
                            return true;
                        }

                        if (newMsaaSampleCount <= 1 && (newDepthTexture == null || newDsv == null))
                        {
                            return true;
                        }

                        if (newMsaaSampleCount > 1 && (newMsaaTexture == null || newMsaaRtv == null || newMsaaDepthTexture == null || newMsaaDsv == null))
                        {
                            return true;
                        }

                        ClearTransformEffectInput();

                        _d3dTexture?.Dispose();
                        _rtv?.Dispose();
                        _depthStencilTexture?.Dispose();
                        _dsv?.Dispose();
                        _msaaTexture?.Dispose();
                        _msaaRtv?.Dispose();
                        _msaaDepthStencilTexture?.Dispose();
                        _msaaDsv?.Dispose();
                        _d2dBitmap?.Dispose();
                        _outputBitmap?.Dispose();

                        _d3dTexture = newTexture;
                        _rtv = newRtv;
                        _depthStencilTexture = newDepthTexture;
                        _dsv = newDsv;
                        _msaaTexture = newMsaaTexture;
                        _msaaRtv = newMsaaRtv;
                        _msaaDepthStencilTexture = newMsaaDepthTexture;
                        _msaaDsv = newMsaaDsv;
                        _msaaSampleCount = newMsaaSampleCount;
                        _d2dBitmap = newBitmap;
                        _outputBitmap = newOutputBitmap;
                        _needsWarmupRender = true;

                        renderWidth = tryWidth;
                        renderHeight = tryHeight;

                    }

                    if (_d2dBitmap == null)
                    {
                        return true;
                    }

                    var outputWidth = Math.Max(1, screenWidth);
                    var outputHeight = Math.Max(1, screenHeight);
                    var scaleX = (float)outputWidth / renderWidth;
                    var scaleY = (float)outputHeight / renderHeight;
                    var transformMatrix =
                        System.Numerics.Matrix3x2.CreateScale(scaleX, scaleY) *
                        System.Numerics.Matrix3x2.CreateTranslation(-outputWidth / 2f, -outputHeight / 2f);
                    renderPhase = "set-transform-matrix";
                    _transformEffect.SetValue((int)AffineTransform2DProperties.TransformMatrix, transformMatrix);

                    var context = _devices.D3D.DeviceContext;
                    var oldRTVs = new ID3D11RenderTargetView[1];
                    renderPhase = "capture-old-render-target";
                    context.OMGetRenderTargets(1, oldRTVs, out ID3D11DepthStencilView? oldDSV);
                    var oldRTV = oldRTVs[0];

                    try
                    {
                        var drawRtv = _msaaRtv ?? _rtv;
                        var drawDsv = _msaaDsv ?? _dsv;
                        if (drawRtv != null)
                        {
                            renderPhase = "clear-d3d-render-target";
                            context.ClearRenderTargetView(drawRtv, new Vortice.Mathematics.Color4(0, 0, 0, 0));
                            if (drawDsv != null)
                            {
                                renderPhase = "clear-depth-stencil";
                                context.ClearDepthStencilView(drawDsv, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
                                renderPhase = "bind-render-target-depth";
                                context.OMSetRenderTargets(drawRtv, drawDsv);
                            }
                            else
                            {
                                renderPhase = "bind-render-target";
                                context.OMSetRenderTargets(drawRtv, (ID3D11DepthStencilView?)null);
                            }
                        }

                        var renderFrameCount = _needsWarmupRender ? 2 : 1;
                        for (var frameIndex = 0; frameIndex < renderFrameCount; frameIndex++)
                        {
                            if (drawRtv != null)
                            {
                                renderPhase = $"prepare-frame-{frameIndex}";
                                context.ClearRenderTargetView(drawRtv, new Vortice.Mathematics.Color4(0, 0, 0, 0));
                                if (drawDsv != null)
                                {
                                    context.ClearDepthStencilView(drawDsv, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
                                    context.OMSetRenderTargets(drawRtv, drawDsv);
                                }
                                else
                                {
                                    context.OMSetRenderTargets(drawRtv, (ID3D11DepthStencilView?)null);
                                }
                            }

                            renderPhase = $"draw-model-frame-{frameIndex}";
                            if (!TryDrawModelFrame(renderWidth, renderHeight, drawModel, drawRtv, drawDsv))
                            {
                                return true;
                            }
                            // Re-bind our RT: Cubism's OffscreenSurface.EndDraw() restores
                            // whatever RT was captured at BeginDraw time via the static s_context.
                            // Force our RT back to guarantee subsequent readback hits the right surface.
                            if (drawRtv != null)
                            {
                                if (drawDsv != null)
                                {
                                    renderPhase = $"rebind-render-target-depth-{frameIndex}";
                                    context.OMSetRenderTargets(drawRtv, drawDsv);
                                }
                                else
                                {
                                    renderPhase = $"rebind-render-target-{frameIndex}";
                                    context.OMSetRenderTargets(drawRtv, (ID3D11DepthStencilView?)null);
                                }
                            }
                            renderPhase = $"flush-frame-{frameIndex}";
                            context.Flush();
                        }
                        _needsWarmupRender = false;

                        if (_msaaSampleCount > 1 && _msaaTexture != null && _d3dTexture != null)
                        {
                            renderPhase = "resolve-msaa";
                            context.ResolveSubresource(_d3dTexture, 0, _msaaTexture, 0, Format.R8G8B8A8_UNorm);
                        }

                        renderPhase = "flush-after-draw";
                        context.Flush();

                        if (_outputBitmap == null)
                        {
                            return true;
                        }

                        var dc = _devices.DeviceContext;
                        var oldTarget = dc.Target;
                        try
                        {
                            renderPhase = "set-d2d-target";
                            dc.Target = _outputBitmap;
                            renderPhase = "d2d-begin-draw";
                            dc.BeginDraw();
                            renderPhase = "d2d-clear";
                            dc.Clear(null);
                            renderPhase = "d2d-draw-image";
                            dc.DrawImage(_d2dBitmap);
                            if (EnableDebugOverlay)
                            {
                                using var brush = dc.CreateSolidColorBrush(new Vortice.Mathematics.Color4(1.0f, 0.0f, 0.0f, 1.0f));
                                dc.FillRectangle(new Vortice.RawRectF(32, 32, 160, 160), brush);
                                dc.DrawRectangle(new Vortice.RawRectF(24, 24, 200, 200), brush, 8.0f);
                            }
                            renderPhase = "d2d-end-draw";
                            dc.EndDraw();
                        }
                        finally
                        {
                            renderPhase = "restore-d2d-target";
                            dc.Target = oldTarget;
                            oldTarget?.Dispose();
                        }

                        HandleRenderSuccess();
                        renderPhase = "set-transform-input";
                        _transformEffect.SetInput(0, _outputBitmap, true);
                        return false;
                    }
                    finally
                    {
                        renderPhase = "restore-old-render-target";
                        context.OMSetRenderTargets(oldRTV, oldDSV);
                        oldRTV?.Dispose();
                        oldDSV?.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    HandleRenderFailure(ex);
                    Commons.ConsoleManager.Error($"[{GetLogPrefix()}] Render error at phase '{renderPhase}': {FormatRenderException(ex)}");
                    return true;
                }
            }
        }

        private bool TryDrawModelFrame(int renderWidth, int renderHeight, bool drawModel,
            ID3D11RenderTargetView? targetRtv = null, ID3D11DepthStencilView? targetDsv = null)
        {
            if (_renderer == null || _model == null)
            {
                return false;
            }

            var context = _devices.D3D.DeviceContext;

            try
            {
                // Re-bind our RT before anything touches the device context.
                // This ensures Cubism's OffscreenSurface::BeginDraw captures OUR rt as the
                // backup, so EndDraw returns to OUR rt (not some other instance's rt).
                if (targetRtv != null)
                {
                    if (targetDsv != null)
                        context.OMSetRenderTargets(targetRtv, targetDsv);
                    else
                        context.OMSetRenderTargets(targetRtv, (ID3D11DepthStencilView?)null);
                }

                if (drawModel)
                {
                    // DrawWithFrame calls CubismRenderer_D3D11::StartFrame() inside the
                    // native g_nativeDrawMutex, then immediately calls DrawModel().
                    // This ensures StartFrame's static writes (s_device, s_context,
                    // s_viewportWidth/Height) are consumed by the same DrawModel call
                    // without any other instance interleaving between them.
                    return _model.DrawWithFrame(_devices.D3D.Device.NativePointer, _devices.D3D.DeviceContext.NativePointer, renderWidth, renderHeight);
                }
                else
                {
                    // Nothing to draw, but still balance the renderer state.
                    _renderer.BeginFrame(renderWidth, renderHeight);
                    _renderer.EndFrame();
                }

                return true;
            }
            catch (SharpGenException ex)
            {
                HandleRenderFailure(ex);
                Commons.ConsoleManager.Error($"[{GetLogPrefix()}] Native draw failed (SharpGen): {FormatRenderException(ex)}");
                return false;
            }
            catch (SEHException ex)
            {
                HandleRenderFailure(ex);
                Commons.ConsoleManager.Error($"[{GetLogPrefix()}] Native draw failed (SEH): {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                HandleRenderFailure(ex);
                Commons.ConsoleManager.Error($"[{GetLogPrefix()}] Native draw failed: {FormatRenderException(ex)}");
                return false;
            }
        }

        private static string FormatRenderException(Exception ex)
        {
            if (ex is SharpGenException sharpGen)
            {
                return $"{sharpGen.Message} (HRESULT=0x{sharpGen.HResult:X8})";
            }

            return $"{ex.Message} (HRESULT=0x{ex.HResult:X8})";
        }


        private bool CanAttemptRender()
        {
            return DateTime.UtcNow >= _renderDisabledUntilUtc;
        }

        private void HandleRenderSuccess()
        {
            _consecutiveRenderFailures = 0;
            _renderDisabledUntilUtc = DateTime.MinValue;
        }

        private void HandleRenderFailure(Exception ex)
        {
            _consecutiveRenderFailures++;

            ResetRenderResources();

            if (_consecutiveRenderFailures >= MaxConsecutiveRenderFailures)
            {
                _renderDisabledUntilUtc = DateTime.UtcNow + RenderRecoveryCooldown;
                _consecutiveRenderFailures = 0;
                Commons.ConsoleManager.Error(
                    $"[{GetLogPrefix()}] Render disabled temporarily after repeated native failures. cooldown={RenderRecoveryCooldown.TotalMilliseconds}ms, reason={ex.GetType().Name}");
            }

        }

        private static void ResetSharedNativeResources()
        {
            lock (_sharedRendererLock)
            {
                _sharedRenderer?.Dispose();
                _sharedRenderer = null;
            }
        }

        private void EnsureFallbackOutputBitmap()
        {
            if (_fallbackOutputBitmap != null)
            {
                return;
            }

            var props = new BitmapProperties1(
                new Vortice.DCommon.PixelFormat(Format.R8G8B8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                96,
                96,
                BitmapOptions.None);
            _fallbackOutputBitmap = _devices.DeviceContext.CreateBitmap(
                new Vortice.Mathematics.SizeI(1, 1),
                IntPtr.Zero,
                0,
                props);
        }

        private void ClearTransformEffectInput()
        {
            if (_transformEffect == null)
            {
                return;
            }

            try
            {
                EnsureFallbackOutputBitmap();
                _transformEffect.SetInput(0, _fallbackOutputBitmap, true);
            }
            catch (Exception)
            {
            }
        }

        private void ResetRenderResources()
        {
            ClearTransformEffectInput();

            _d3dTexture?.Dispose();
            _d3dTexture = null;
            _rtv?.Dispose();
            _rtv = null;
            _depthStencilTexture?.Dispose();
            _depthStencilTexture = null;
            _dsv?.Dispose();
            _dsv = null;
            _msaaTexture?.Dispose();
            _msaaTexture = null;
            _msaaRtv?.Dispose();
            _msaaRtv = null;
            _msaaDepthStencilTexture?.Dispose();
            _msaaDepthStencilTexture = null;
            _msaaDsv?.Dispose();
            _msaaDsv = null;
            _d2dBitmap?.Dispose();
            _d2dBitmap = null;
            _outputBitmap?.Dispose();
            _outputBitmap = null;
            _msaaSampleCount = 1;
            _needsWarmupRender = true;
            _d3d11Initialized = false;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _model?.Dispose();
                _model = null;
                _renderer = null;

                _d3dTexture?.Dispose();
                _rtv?.Dispose();
                _depthStencilTexture?.Dispose();
                _dsv?.Dispose();
                _msaaTexture?.Dispose();
                _msaaRtv?.Dispose();
                _msaaDepthStencilTexture?.Dispose();
                _msaaDsv?.Dispose();
                _d2dBitmap?.Dispose();
                _outputBitmap?.Dispose();
                ClearTransformEffectInput();
                _outputImage?.Dispose();
                _outputImage = null;
                _fallbackOutputBitmap?.Dispose();
                _fallbackOutputBitmap = null;
                _transformEffect?.Dispose();
                _transformEffect = null;

                Live2DManager.GetInstance().Release();

                if (System.Threading.Interlocked.Decrement(ref _activeSourceCount) == 0)
                {
                    ResetSharedNativeResources();
                    _sharedDevicePtr = IntPtr.Zero;
                    _sharedContextPtr = IntPtr.Zero;
                }

                _disposed = true;
            }
        }

        // ITachieSource2実装
        public ID2D1Image Output => _outputImage ?? _transformEffect!.Output;

        private string GetLogPrefix()
        {
            return $"src#{_instanceId}/thr{Environment.CurrentManagedThreadId}";
        }

    }
}
