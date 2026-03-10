using System.ComponentModel.DataAnnotations;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using VTuberKitForYMM4.Commons.CustomPropertyEditor;
using VTuberKitForYMM4.Plugin.CustomPropertyEditor;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace VTuberKitForYMM4.Plugin.Shape
{
    public class TargetPointShapePlugin : IShapePlugin
    {
        public string Name => "Live2Dターゲットポイント";
        public bool IsExoShapeSupported => false;
        public bool IsExoMaskSupported => false;

        public IShapeParameter CreateShapeParameter(SharedDataStore? sharedData) => new TargetPointShapeParameter(sharedData);
    }

    internal class TargetPointShapeParameter : ShapeParameterBase
    {
        private InteractionTargetViewModel? targetCharacter;

        [Display(GroupName = "連携", Name = "対象キャラクター", Description = "連携する Live2D キャラクターを選択します")]
        [CustomComboBox]
        public InteractionTargetViewModel TargetCharacter
        {
            get
            {
                targetCharacter ??= CreateTargetCharacter();
                SyncTargetCharacterSelection();
                return targetCharacter;
            }
            set
            {
                if (ReferenceEquals(targetCharacter, value))
                {
                    return;
                }

                if (targetCharacter != null)
                {
                    targetCharacter.PropertyChanged -= TargetCharacter_PropertyChanged;
                }

                targetCharacter = value ?? CreateTargetCharacter();
                targetCharacter.PropertyChanged += TargetCharacter_PropertyChanged;
                SyncTargetCharacterSelection();
            }
        }

        [Display(GroupName = "連携", Name = "リンクID", Description = "内部識別子。通常は対象キャラクター選択で自動設定されます")]
        public string LinkId
        {
            get
            {
                var selectedLinkId = TargetCharacter?.SelectedLinkId;
                var resolved = string.IsNullOrWhiteSpace(selectedLinkId)
                    ? ResolveLinkId(linkId)
                    : selectedLinkId;
                SyncTargetCharacterSelection(resolved);

                return resolved;
            }
            set
            {
                var normalized = value ?? string.Empty;
                Set(ref linkId, normalized);
                if (TargetCharacter != null)
                {
                    TargetCharacter.SelectedLinkId = normalized;
                }
            }
        }
        string linkId = string.Empty;

        [Display(Name = "X", Description = "モデル空間のターゲットX")]
        [AnimatedHoldSlider("F2", "", -1.0, 1.0)]
        public Animation X { get; } = new Animation(0, -1, 1);

        [Display(Name = "Y", Description = "モデル空間のターゲットY")]
        [AnimatedHoldSlider("F2", "", -1.0, 1.0)]
        public Animation Y { get; } = new Animation(0, -1, 1);

        [Display(Name = "サイズ", Description = "ガイド表示サイズ")]
        [AnimatedHoldSlider("F2", "", 0.02, 0.5)]
        public Animation Size { get; } = new Animation(0.08, 0.02, 0.5);

        public TargetPointShapeParameter(SharedDataStore? sharedData) : base(sharedData)
        {
            TargetCharacter = CreateTargetCharacter();
        }

        public TargetPointShapeParameter() : this(null)
        {
        }

        public override IEnumerable<string> CreateMaskExoFilter(int keyFrameIndex, ExoOutputDescription desc, ShapeMaskExoOutputDescription shapeMaskDesc) => [];

        public override IEnumerable<string> CreateShapeItemExoFilter(int keyFrameIndex, ExoOutputDescription desc) => [];

        public override IShapeSource CreateShapeSource(IGraphicsDevicesAndContext devices) => new TargetPointShapeSource(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [X, Y, Size];

        protected override void LoadSharedData(SharedDataStore store)
        {
            if (store.Load<SharedData>() is not { } data)
                return;

            LinkId = data.LinkId;
            X.CopyFrom(data.X);
            Y.CopyFrom(data.Y);
            Size.CopyFrom(data.Size);
        }

        protected override void SaveSharedData(SharedDataStore store)
        {
            store.Save(new SharedData(this));
        }

        private static string ResolveLinkId(string? currentLinkId)
        {
            if (!string.IsNullOrWhiteSpace(currentLinkId) &&
                !string.Equals(currentLinkId, Live2DInteractionDefaults.DefaultLinkId, StringComparison.OrdinalIgnoreCase))
            {
                return currentLinkId;
            }

            var targets = Live2DInteractionStore.GetInteractionTargets();
            return targets.Count == 1 ? targets[0].LinkId : string.Empty;
        }

        private InteractionTargetViewModel CreateTargetCharacter()
        {
            return new InteractionTargetViewModel(string.Empty, () => linkId);
        }

        private void SyncTargetCharacterSelection(string? resolved = null)
        {
            if (targetCharacter == null)
            {
                return;
            }

            resolved ??= ResolveLinkId(linkId);
            if (!string.IsNullOrWhiteSpace(resolved) &&
                !string.Equals(targetCharacter.SelectedLinkId, resolved, StringComparison.Ordinal))
            {
                targetCharacter.SelectedLinkId = resolved;
            }
        }

        private void TargetCharacter_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(InteractionTargetViewModel.SelectedLinkId) || sender is not InteractionTargetViewModel viewModel)
            {
                return;
            }

            var selected = viewModel.SelectedLinkId ?? string.Empty;
            if (!string.Equals(linkId, selected, StringComparison.Ordinal))
            {
                Set(ref linkId, selected, nameof(LinkId));
            }
        }

        private sealed class SharedData
        {
            public string LinkId { get; set; } = string.Empty;
            public Animation X { get; } = new Animation(0, -1, 1);
            public Animation Y { get; } = new Animation(0, -1, 1);
            public Animation Size { get; } = new Animation(0.08, 0.02, 0.5);

            public SharedData(TargetPointShapeParameter parameter)
            {
                LinkId = parameter.LinkId;
                X.CopyFrom(parameter.X);
                Y.CopyFrom(parameter.Y);
                Size.CopyFrom(parameter.Size);
            }
        }
    }

    internal sealed class TargetPointShapeSource : IShapeSource
    {
        private readonly string sourceId = Guid.NewGuid().ToString("N");
        private readonly IGraphicsDevicesAndContext devices;
        private readonly TargetPointShapeParameter parameter;
        private readonly ID2D1SolidColorBrush brush;
        private ID2D1CommandList? commandList;
        private string currentLinkId = string.Empty;
        private float x;
        private float y;
        private float size;
        private int screenWidth;
        private int screenHeight;
        private float transformPositionX;
        private float transformPositionY;
        private float transformScale = 1.0f;
        private float transformRotationDegrees;

        public ID2D1Image Output => commandList ?? throw new InvalidOperationException($"{nameof(commandList)} is null.");

        public TargetPointShapeSource(IGraphicsDevicesAndContext devices, TargetPointShapeParameter parameter)
        {
            this.devices = devices;
            this.parameter = parameter;
            brush = devices.DeviceContext.CreateSolidColorBrush(InteractionShapeColors.GetTargetPointColor(parameter.LinkId));
            currentLinkId = parameter.LinkId;
        }

        public void Update(TimelineItemSourceDescription timelineItemSourceDescription)
        {
            var fps = timelineItemSourceDescription.FPS;
            var frame = timelineItemSourceDescription.ItemPosition.Frame;
            var length = timelineItemSourceDescription.ItemDuration.Frame;

            var newX = (float)parameter.X.GetValue(frame, length, fps);
            var newY = (float)parameter.Y.GetValue(frame, length, fps);
            var newSize = (float)parameter.Size.GetValue(frame, length, fps);
            var newLinkId = parameter.LinkId;
            var newScreenWidth = Math.Max(1, timelineItemSourceDescription.ScreenSize.Width);
            var newScreenHeight = Math.Max(1, timelineItemSourceDescription.ScreenSize.Height);
            var transform = GetTransformState(newLinkId);

            Live2DInteractionStore.UpdateTargetPoint(sourceId, newLinkId, newX, newY, timelineItemSourceDescription.Layer);
            if (commandList != null &&
                currentLinkId == newLinkId &&
                x == newX &&
                y == newY &&
                size == newSize &&
                screenWidth == newScreenWidth &&
                screenHeight == newScreenHeight &&
                transformPositionX == transform.PositionX &&
                transformPositionY == transform.PositionY &&
                transformScale == transform.Scale &&
                transformRotationDegrees == transform.RotationDegrees)
                return;

            var dc = devices.DeviceContext;
            commandList?.Dispose();
            commandList = dc.CreateCommandList();
            brush.Color = InteractionShapeColors.GetTargetPointColor(newLinkId);

            var center = TransformPoint(newLinkId, newX, newY, newScreenWidth, newScreenHeight);
            var halfModel = Math.Max(4.0f / 500.0f, newSize * 0.6f);
            var horizontal = TransformVector(newLinkId, halfModel, 0.0f, newScreenWidth, newScreenHeight);
            var vertical = TransformVector(newLinkId, 0.0f, halfModel, newScreenWidth, newScreenHeight);
            var leftPixel = center - horizontal;
            var rightPixel = center + horizontal;
            var topPixel = center + vertical;
            var bottomPixel = center - vertical;

            dc.Target = commandList;
            dc.BeginDraw();
            dc.Clear(null);
            dc.DrawLine(leftPixel, rightPixel, brush, 4.0f);
            dc.DrawLine(bottomPixel, topPixel, brush, 4.0f);
            dc.EndDraw();
            dc.Target = null;
            commandList.Close();

            x = newX;
            y = newY;
            size = newSize;
            screenWidth = newScreenWidth;
            screenHeight = newScreenHeight;
            currentLinkId = newLinkId;
            transformPositionX = transform.PositionX;
            transformPositionY = transform.PositionY;
            transformScale = transform.Scale;
            transformRotationDegrees = transform.RotationDegrees;
        }

        private Vector2 TransformPoint(string linkId, float x, float y, int screenWidth, int screenHeight)
        {
            Live2DInteractionStore.TryGetInteractionTransform(linkId, out var state);
            return InteractionShapeTransform.TransformTargetPointToPixel(new Vector2(x, y), state, screenWidth, screenHeight);
        }

        private Vector2 TransformVector(string linkId, float x, float y, int screenWidth, int screenHeight)
        {
            Live2DInteractionStore.TryGetInteractionTransform(linkId, out var state);
            return InteractionShapeTransform.TransformLocalVectorToPixel(new Vector2(x, y), state, screenWidth, screenHeight);
        }

        private static (float PositionX, float PositionY, float Scale, float RotationDegrees) GetTransformState(string linkId)
        {
            if (Live2DInteractionStore.TryGetInteractionTransform(linkId, out var state) && state is not null)
            {
                return (state.PositionX, state.PositionY, state.Scale, state.RotationDegrees);
            }

            return default;
        }

        public void Dispose()
        {
            Live2DInteractionStore.RemoveTargetPoint(sourceId);
            commandList?.Dispose();
            brush.Dispose();
        }
    }
}
