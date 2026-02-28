using System;
using System.IO;
using Vortice.Direct2D1;
using Vortice.Direct3D11;
using Vortice.DXGI;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Tachie;

using VTuberKitForNative;

namespace VTuberKitForYMM4.Plugin
{
    public class Live2DTachieSource : ITachieSource2, IDisposable
    {
        private const int MinRenderTargetSize = 256;
        private const int DefaultMaxRenderTargetSize = 8192;
        private const int MinAllowedRenderTargetMaxSize = 2048;
        private const int MaxAllowedRenderTargetMaxSize = 8192;
        private const float MinInternalRenderScale = 1.0f;
        private const float MaxInternalRenderScale = 4.0f;
        private const float DefaultInternalRenderScale = 2.0f;

        private static readonly object _drawLock = new object();
        private readonly IGraphicsDevicesAndContext _devices;
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
        private ID2D1Effect? _transformEffect;
        private ID2D1Image? _outputImage;
        private double _lastItemFrame;
        private bool _hasLastItemFrame;
        private bool _needsInitialFrameUpdate = true;
        private string _appliedExpressionId = string.Empty;
        private DateTime _lastDebugLogAt = DateTime.MinValue;
        private bool _hasCachedCharacterSettings;
        private bool _cachedAutoEyeBlink;
        private float _cachedEyeBlinkInterval;
        private bool _cachedEnablePhysics;
        private bool _cachedEnableBreath;
        private bool _hasCachedItemSettings;
        private float _cachedOpacity = 1.0f;
        private float _cachedMultiplyR = 1.0f;
        private float _cachedMultiplyG = 1.0f;
        private float _cachedMultiplyB = 1.0f;
        private float _cachedMultiplyA = 1.0f;
        private int _cachedRenderTargetMaxSize = DefaultMaxRenderTargetSize;
        private float _cachedInternalRenderScale = DefaultInternalRenderScale;
        private bool _cachedEnableFxaa;
        private bool _cachedEnableMsaa = true;
        private int _cachedPreferredMsaaSampleCount = 4;
        private bool _canSetAffineInterpolationMode = true;

        public Live2DTachieSource(IGraphicsDevicesAndContext devices)
        {
            _devices = devices ?? throw new ArgumentNullException(nameof(devices));
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                var manager = Live2DManager.GetInstance();
                if (manager == null)
                {
                    Commons.ConsoleManager.Error("Live2DManager.GetInstance() returned null");
                    return;
                }

                manager.Initialize();
                _renderer = new Live2DRenderer();

                _transformEffect = (ID2D1Effect)_devices.DeviceContext.CreateEffect(EffectGuids.AffineTransform2D);
                _outputImage = _transformEffect.Output;
            }
            catch (Exception ex)
            {
                Commons.ConsoleManager.Error($"Live2D initialization error: {ex.Message}");
            }
        }

        private bool TryInitializeD3D11Device()
        {
            if (_d3d11Initialized)
                return true;

            try
            {
                var d3d11Device = _devices.D3D.Device;
                var d3d11Context = d3d11Device.ImmediateContext;
                
                Live2DManager.GetInstance().SetD3D11Device(d3d11Device.NativePointer, d3d11Context.NativePointer);
                _renderer?.Initialize(d3d11Device.NativePointer, d3d11Context.NativePointer);

                _d3d11Initialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Commons.ConsoleManager.Error($"D3D11 device initialization failed: {ex.Message}");
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
                var activeFace = GetActiveFace(desc);
                var faceParam = activeFace.Face;

                if (charParam != null && !string.IsNullOrEmpty(charParam.File))
                {
                    if (_currentModelPath != charParam.File)
                    {
                        if (File.Exists(charParam.File))
                        {
                            _currentModelPath = charParam.File;
                            _model?.Dispose();
                            _model = Live2DManager.GetInstance().CreateModel();
                            if (!_model.LoadModel(_currentModelPath))
                            {
                                Commons.ConsoleManager.Error($"Failed to load model: {_currentModelPath}");
                            }
                            else
                            {
                                Commons.ConsoleManager.Debug($"Model loaded: {_currentModelPath}");
                                _hasLastItemFrame = false;
                                _needsInitialFrameUpdate = true;
                                _appliedExpressionId = string.Empty;
                                _hasCachedCharacterSettings = false;
                                _hasCachedItemSettings = false;
                            }
                        }
                        else
                        {
                            Commons.ConsoleManager.Error($"Model file not found: {charParam.File}");
                        }
                    }
                }

                if (_model != null)
                {
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
                        _cachedEnableFxaa = charParam.EnableFxaa;
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
                        _cachedEnableFxaa = true;
                        _cachedEnableMsaa = true;
                        _cachedPreferredMsaaSampleCount = 4;
                    }

                    if (faceParam != null)
                    {
                        if (!string.IsNullOrWhiteSpace(faceParam.ExpressionId))
                        {
                            if (!string.Equals(_appliedExpressionId, faceParam.ExpressionId, StringComparison.Ordinal))
                            {
                                _model.SetExpression(faceParam.ExpressionId);
                                _appliedExpressionId = faceParam.ExpressionId;
                            }
                        }
                        else if (!string.IsNullOrEmpty(_appliedExpressionId))
                        {
                            _model.ClearExpression();
                            _appliedExpressionId = string.Empty;
                        }
                    }

                    TachieMotionEvaluator.UpdateMotionToCurrentTime(
                        _model,
                        desc,
                        faceParam,
                        itemParam,
                        Math.Max(activeFace.RelativeTimeSeconds, (float)Math.Max(0.0, desc.ItemPosition.Time.TotalSeconds)),
                        (float)(charParam?.LipSyncGain ?? 1.0),
                        true);

                    var transformPositionX = 0.0f;
                    var transformPositionY = 0.0f;
                    var transformScale = 1.0f;
                    var transformRotation = 0.0f;

                    float finalOpacity = 1.0f;
                    if (itemParam != null)
                    {
                        var frame = desc.ItemPosition.Frame;
                        var length = desc.ItemDuration.Frame;
                        var fps = desc.FPS;
                        var opacity = itemParam.Opacity.GetValue(frame, length, fps);
                        var multiplyR = itemParam.MultiplyR.GetValue(frame, length, fps);
                        var multiplyG = itemParam.MultiplyG.GetValue(frame, length, fps);
                        var multiplyB = itemParam.MultiplyB.GetValue(frame, length, fps);
                        var multiplyA = itemParam.MultiplyA.GetValue(frame, length, fps);
                        var faceOpacity = faceParam?.Opacity?.GetValue(frame, length, fps) ?? 1.0;
                        finalOpacity = (float)Math.Clamp(opacity * faceOpacity, 0.0, 1.0);
                        var mR = (float)multiplyR;
                        var mG = (float)multiplyG;
                        var mB = (float)multiplyB;
                        var mA = (float)multiplyA;

                        if (!_hasCachedItemSettings ||
                            !NearlyEqual(_cachedOpacity, finalOpacity) ||
                            !NearlyEqual(_cachedMultiplyR, mR) ||
                            !NearlyEqual(_cachedMultiplyG, mG) ||
                            !NearlyEqual(_cachedMultiplyB, mB) ||
                            !NearlyEqual(_cachedMultiplyA, mA))
                        {
                            _model.ApplyItemParameters(finalOpacity, mR, mG, mB, mA);
                            _cachedOpacity = finalOpacity;
                            _cachedMultiplyR = mR;
                            _cachedMultiplyG = mG;
                            _cachedMultiplyB = mB;
                            _cachedMultiplyA = mA;
                            _hasCachedItemSettings = true;
                        }

                        transformPositionX = (float)itemParam.PositionX.GetValue(frame, length, fps);
                        transformPositionY = (float)itemParam.PositionY.GetValue(frame, length, fps);
                        transformScale = (float)itemParam.Scale.GetValue(frame, length, fps);
                        transformRotation = (float)itemParam.Rotation.GetValue(frame, length, fps);
                    }

                    if (faceParam != null)
                    {
                        var faceFrame = Math.Max(0L, (long)Math.Round(activeFace.LocalFrame));
                        var faceLength = Math.Max(1L, (long)Math.Round(activeFace.DurationFrame));
                        var fpsForFace = desc.FPS;

                        transformPositionX += (float)faceParam.OffsetPositionX.GetValue(faceFrame, faceLength, fpsForFace);
                        transformPositionY += (float)faceParam.OffsetPositionY.GetValue(faceFrame, faceLength, fpsForFace);
                        transformScale += (float)faceParam.OffsetScale.GetValue(faceFrame, faceLength, fpsForFace);
                        transformRotation += (float)faceParam.OffsetRotation.GetValue(faceFrame, faceLength, fpsForFace);
                    }

                    _model.SetPosition(transformPositionX, transformPositionY);
                    _model.SetScale(Math.Max(0.01f, transformScale));
                    _model.SetRotation(transformRotation);

                    var deltaSeconds = GetDeltaSeconds(desc, out var rewound);
                    if (rewound)
                    {
                        _model.StopAllMotions();
                        _appliedExpressionId = string.Empty;
                        _hasCachedItemSettings = false;
                    }
                    if (charParam != null)
                    {
                        var windStrength = Math.Max(0.0, charParam.WindStrength);
                        if (windStrength > 0.0)
                        {
                            var t = (float)desc.ItemPosition.Time.TotalSeconds;
                            var dragX = (float)(Math.Sin(t * 0.7f) * windStrength * 0.6f);
                            var dragY = (float)(Math.Cos(t * 0.45f) * windStrength * 0.15f);
                            _model.SetDragging(dragX, dragY);
                        }
                        else
                        {
                            _model.SetDragging(0.0f, 0.0f);
                        }
                    }
                    _model.Update(deltaSeconds);
                    if (faceParam != null)
                    {
                        var applyManualFaceParameters = TachieMotionEvaluator.ShouldApplyManualFaceParameters(faceParam);
                        TachieMotionEvaluator.ApplyFaceAndLipSync(
                            _model,
                            desc,
                            faceParam,
                            activeFace.LocalFrame,
                            activeFace.DurationFrame,
                            (float)(charParam?.LipSyncGain ?? 1.0),
                            applyManualFaceParameters);
                        _model.CommitParameters();
                    }

                    if ((DateTime.UtcNow - _lastDebugLogAt).TotalSeconds >= 1.0)
                    {
                        _lastDebugLogAt = DateTime.UtcNow;
                        var canvasW = _model.GetCanvasWidth();
                        var canvasH = _model.GetCanvasHeight();
                        var texW = _model.GetTextureWidth();
                        var texH = _model.GetTextureHeight();
                        var rtSize = CalculateRenderTargetSize(
                            desc.ScreenSize.Width,
                            desc.ScreenSize.Height,
                            _cachedRenderTargetMaxSize,
                            _cachedInternalRenderScale);
                        Commons.ConsoleManager.Debug(
                            $"delta={deltaSeconds:F4}, itemTime={desc.ItemPosition.Time.TotalSeconds:F3}, face={(faceParam != null ? $"{faceParam.MotionGroup}[{faceParam.MotionIndex}] use={faceParam.UseMotion}" : "null")}, mouthShape={desc.MouthShape}, voiceVolume={desc.VoiceVolume:F4}, screen={desc.ScreenSize.Width}x{desc.ScreenSize.Height}, rt={rtSize.Width}x{rtSize.Height}, rtMax={_cachedRenderTargetMaxSize}, scale={_cachedInternalRenderScale:F2}, fxaa={_cachedEnableFxaa}, msaa={_cachedEnableMsaa}, msaaPref={_cachedPreferredMsaaSampleCount}x, canvas={canvasW:F3}x{canvasH:F3}, tex={texW}x{texH}");
                    }

                    var canDraw = desc.ScreenSize.Width > 0 &&
                                  desc.ScreenSize.Height > 0 &&
                                  finalOpacity > 0.001f;
                    Render(
                        desc.ScreenSize.Width,
                        desc.ScreenSize.Height,
                        canDraw,
                        _cachedRenderTargetMaxSize,
                        _cachedInternalRenderScale,
                        _cachedEnableFxaa,
                        _cachedEnableMsaa,
                        _cachedPreferredMsaaSampleCount);
                }
            }
            catch (Exception ex)
            {
                Commons.ConsoleManager.Error($"Live2D update error: {ex.Message}");
            }
        }

        private static (Live2DFaceParameter? Face, float RelativeTimeSeconds, double LocalFrame, double DurationFrame) GetActiveFace(TachieSourceDescription desc)
        {
            if (desc.Tachie?.Faces is not { } faces)
            {
                return (null, 0.0f, 0.0, 1.0);
            }

            var hasFace = false;
            foreach (var _ in faces)
            {
                hasFace = true;
                break;
            }
            if (!hasFace)
            {
                return (null, 0.0f, 0.0, 1.0);
            }

            var currentTime = desc.ItemPosition.Time;
            var currentFrame = (double)desc.ItemPosition.Frame;
            Live2DFaceParameter? activeFace = null;
            TimeSpan activeStart = TimeSpan.MinValue;
            double activeStartFrame = 0.0;
            double activeDurationFrame = 1.0;
            Live2DFaceParameter? latestStartedFace = null;
            TimeSpan latestStartedAt = TimeSpan.MinValue;
            double latestStartedFrame = 0.0;
            double latestDurationFrame = 1.0;
            Live2DFaceParameter? earliestFace = null;
            TimeSpan earliestAt = TimeSpan.MaxValue;
            double earliestDurationFrame = 1.0;

            foreach (var face in faces)
            {
                var start = face.ItemPosition.Time;
                var end = start + face.ItemDuration.Time;
                var startFrame = (double)face.ItemPosition.Frame;
                var durationFrame = Math.Max(1.0, (double)face.ItemDuration.Frame);

                if (start < earliestAt)
                {
                    earliestAt = start;
                    earliestFace = face.FaceParameter as Live2DFaceParameter;
                    earliestDurationFrame = durationFrame;
                }

                if (start <= currentTime && start >= latestStartedAt)
                {
                    latestStartedFace = face.FaceParameter as Live2DFaceParameter;
                    latestStartedAt = start;
                    latestStartedFrame = startFrame;
                    latestDurationFrame = durationFrame;
                }
                if (currentTime >= start && currentTime < end && start >= activeStart)
                {
                    activeFace = face.FaceParameter as Live2DFaceParameter;
                    activeStart = start;
                    activeStartFrame = startFrame;
                    activeDurationFrame = durationFrame;
                }
            }

            if (activeFace != null)
            {
                var relative = currentTime - activeStart;
                var localFrame = Math.Clamp(currentFrame - activeStartFrame, 0.0, activeDurationFrame);
                return (activeFace, (float)Math.Max(0.0, relative.TotalSeconds), localFrame, activeDurationFrame);
            }

            if (latestStartedFace != null)
            {
                var relative = currentTime - latestStartedAt;
                var localFrame = Math.Clamp(currentFrame - latestStartedFrame, 0.0, latestDurationFrame);
                return (latestStartedFace, (float)Math.Max(0.0, relative.TotalSeconds), localFrame, latestDurationFrame);
            }

            return (earliestFace, 0.0f, 0.0, earliestDurationFrame);
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

            if (deltaFrames < 0.0)
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

        private static bool NearlyEqual(float a, float b, float epsilon = 0.0001f)
        {
            return Math.Abs(a - b) <= epsilon;
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

        private void Render(int screenWidth, int screenHeight, bool drawModel, int maxRenderTargetSize, float internalRenderScale, bool enableFxaa, bool enableMsaa, int preferredMsaaSampleCount)
        {
            if (_renderer == null || _model == null || _transformEffect == null)
            {
                return;
            }

            var (renderWidth, renderHeight) = CalculateRenderTargetSize(screenWidth, screenHeight, maxRenderTargetSize, internalRenderScale);
            lock (_drawLock)
            {
                try
                {
                    if (_d3dTexture == null || _d3dTexture.Description.Width != renderWidth || _d3dTexture.Description.Height != renderHeight)
                    {
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
                            catch (Exception ex)
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

                                Commons.ConsoleManager.Debug($"RT allocation retry: {tryWidth}x{tryHeight} failed ({ex.Message})");

                                if (tryWidth == MinRenderTargetSize && tryHeight == MinRenderTargetSize)
                                {
                                    break;
                                }

                                tryWidth = Math.Max(MinRenderTargetSize, tryWidth / 2);
                                tryHeight = Math.Max(MinRenderTargetSize, tryHeight / 2);
                            }
                        }

                        if (!allocated || newTexture == null || newRtv == null || newBitmap == null)
                        {
                            return;
                        }

                        if (newMsaaSampleCount <= 1 && (newDepthTexture == null || newDsv == null))
                        {
                            return;
                        }

                        if (newMsaaSampleCount > 1 && (newMsaaTexture == null || newMsaaRtv == null || newMsaaDepthTexture == null || newMsaaDsv == null))
                        {
                            return;
                        }

                        _d3dTexture?.Dispose();
                        _rtv?.Dispose();
                        _depthStencilTexture?.Dispose();
                        _dsv?.Dispose();
                        _msaaTexture?.Dispose();
                        _msaaRtv?.Dispose();
                        _msaaDepthStencilTexture?.Dispose();
                        _msaaDsv?.Dispose();
                        _d2dBitmap?.Dispose();

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

                        renderWidth = tryWidth;
                        renderHeight = tryHeight;

                        Commons.ConsoleManager.Debug($"Render target allocated: {renderWidth}x{renderHeight}, msaa={_msaaSampleCount}x");
                    }

                    if (_d2dBitmap == null)
                    {
                        return;
                    }

                    _transformEffect.SetInput(0, _d2dBitmap, true);
                    if (_canSetAffineInterpolationMode)
                    {
                        try
                        {
                            _transformEffect.SetValue(
                                (int)AffineTransform2DProperties.InterpolationMode,
                                (int)(enableFxaa ? AffineTransform2DInterpolationMode.HighQualityCubic : AffineTransform2DInterpolationMode.Linear));
                        }
                        catch (Exception ex)
                        {
                            _canSetAffineInterpolationMode = false;
                            Commons.ConsoleManager.Debug($"AffineTransform2D.InterpolationMode unsupported on this environment: {ex.Message}");
                        }
                    }
                    var outputWidth = Math.Max(1, screenWidth);
                    var outputHeight = Math.Max(1, screenHeight);
                    var scaleX = (float)outputWidth / renderWidth;
                    var scaleY = (float)outputHeight / renderHeight;
                    var transformMatrix =
                        System.Numerics.Matrix3x2.CreateScale(scaleX, scaleY) *
                        System.Numerics.Matrix3x2.CreateTranslation(-outputWidth / 2f, -outputHeight / 2f);
                    _transformEffect.SetValue((int)AffineTransform2DProperties.TransformMatrix, transformMatrix);

                    var context = _devices.D3D.Device.ImmediateContext;
                    var oldRTVs = new ID3D11RenderTargetView[1];
                    context.OMGetRenderTargets(1, oldRTVs, out ID3D11DepthStencilView? oldDSV);
                    var oldRTV = oldRTVs[0];
                    
                    try
                    {
                        var drawRtv = _msaaRtv ?? _rtv;
                        var drawDsv = _msaaDsv ?? _dsv;

                        if (drawRtv != null)
                        {
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

                        // モデルのサイズをそのまま使用
                        _renderer.BeginFrame(renderWidth, renderHeight);
                        _renderer.SetViewport(0, 0, renderWidth, renderHeight);
                        if (drawModel)
                        {
                            _model.Draw(renderWidth, renderHeight);
                        }

                        _renderer.EndFrame();

                        if (_msaaSampleCount > 1 && _msaaTexture != null && _d3dTexture != null)
                        {
                            context.ResolveSubresource(_d3dTexture, 0, _msaaTexture, 0, Format.R8G8B8A8_UNorm);
                        }

                        context.OMSetRenderTargets(oldRTV, oldDSV);
                    }
                    finally
                    {
                        oldRTV?.Dispose();
                        oldDSV?.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Commons.ConsoleManager.Error($"Render error: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _model?.Dispose();
                _model = null;
                _renderer?.Dispose();
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
                _outputImage?.Dispose();
                _outputImage = null;
                _transformEffect?.Dispose();
                _transformEffect = null;
                
                Live2DManager.GetInstance().Release();
                
                _disposed = true;
            }
        }

        // ITachieSource2実装
        // DeviceContext.Targetを変更せずに描画結果を返すため、エフェクトの出力を提供する
        public ID2D1Image Output => _outputImage ?? _transformEffect!.Output;
    }
}
