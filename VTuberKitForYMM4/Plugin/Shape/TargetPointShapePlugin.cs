using System.ComponentModel;
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
        public string Name => Translate.Plugin_TargetPoint_Name;
        public bool IsExoShapeSupported => false;
        public bool IsExoMaskSupported => false;

        public IShapeParameter CreateShapeParameter(SharedDataStore? sharedData) => new TargetPointShapeParameter(sharedData);
    }

    internal class TargetPointShapeParameter : ShapeParameterBase
    {
        private InteractionTargetViewModel? targetCharacter;

        [Display(Name = nameof(Translate.Target_Character_Name), Description = nameof(Translate.Target_Character_Desc), ResourceType = typeof(Translate))]
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

        [Display(Name = nameof(Translate.Target_LinkId_Name), Description = nameof(Translate.Target_LinkId_Desc), ResourceType = typeof(Translate))]
        public string LinkId
        {
            get
            {
                var selectedLinkId = targetCharacter?.SelectedLinkId;
                var resolved = string.IsNullOrWhiteSpace(selectedLinkId)
                    ? ResolveLinkId(linkId)
                    : selectedLinkId;

                return resolved;
            }
            set
            {
                var normalized = value ?? string.Empty;
                Set(ref linkId, normalized);
                if (targetCharacter != null)
                {
                    targetCharacter.SelectedLinkId = normalized;
                }
                SyncTargetCharacterSelection(normalized);
            }
        }
        string linkId = string.Empty;


        [Display(Name = nameof(Translate.Target_IsHidden_Name), Description = nameof(Translate.Target_IsHidden_Desc), ResourceType = typeof(Translate))]
        [ToggleSlider]
        [DefaultValue(false)]
        public bool IsHidden { get => isHidden; set => Set(ref isHidden, value); }
        bool isHidden;

        [Display(Name = nameof(Translate.Target_X_Name), Description = nameof(Translate.Target_X_Desc), ResourceType = typeof(Translate))]
        [AnimatedHoldSlider("F1", "px", -500.0, 500.0)]
        public Animation X { get; } = new Animation(0, -100000, 100000);

        [Display(Name = nameof(Translate.Target_Y_Name), Description = nameof(Translate.Target_Y_Desc), ResourceType = typeof(Translate))]
        [AnimatedHoldSlider("F1", "px", -500.0, 500.0)]
        public Animation Y { get; } = new Animation(0, -100000, 100000);

        [Display(Name = nameof(Translate.Target_Size_Name), Description = nameof(Translate.Target_Size_Desc), ResourceType = typeof(Translate))]
        [AnimatedHoldSlider("F1", "px", 0.0, 500.0)]
        public Animation Size { get; } = new Animation(80, 0, 100000);

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
            IsHidden = data.IsHidden;
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

            OnPropertyChanged(nameof(TargetCharacter));
            OnPropertyChanged(nameof(LinkId));
        }

        private sealed class SharedData
        {
            public string LinkId { get; set; } = string.Empty;
            public Animation X { get; } = new Animation(0, -1, 1);
            public Animation Y { get; } = new Animation(0, -1, 1);
            public Animation Size { get; } = new Animation(0.08, 0.02, 0.5);
            public bool IsHidden { get; set; }

            public SharedData(TargetPointShapeParameter parameter)
            {
                LinkId = parameter.LinkId;
                X.CopyFrom(parameter.X);
                Y.CopyFrom(parameter.Y);
                Size.CopyFrom(parameter.Size);
                IsHidden = parameter.IsHidden;
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
        private float transformModelCenterX;
        private float transformModelCenterY;
        private float transformHitTestScaleX = 1.0f;
        private float transformHitTestScaleY = 1.0f;
        private float transformHitTestTranslateX;
        private float transformHitTestTranslateY;
        private bool isHidden;

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
            var newIsHidden = parameter.IsHidden;

            if (string.IsNullOrWhiteSpace(newLinkId))
            {
                Live2DInteractionStore.RemoveTargetPoint(sourceId);
            }
            else
            {
                Live2DInteractionStore.UpdateTargetPoint(sourceId, newLinkId, newX, newY, timelineItemSourceDescription.Layer);
            }

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
                transformRotationDegrees == transform.RotationDegrees &&
                transformModelCenterX == transform.ModelCenterX &&
                transformModelCenterY == transform.ModelCenterY &&
                transformHitTestScaleX == transform.HitTestScaleX &&
                transformHitTestScaleY == transform.HitTestScaleY &&
                transformHitTestTranslateX == transform.HitTestTranslateX &&
                transformHitTestTranslateY == transform.HitTestTranslateY &&
                isHidden == newIsHidden)
                return;

            var dc = devices.DeviceContext;
            commandList?.Dispose();
            commandList = dc.CreateCommandList();
            brush.Color = InteractionShapeColors.GetTargetPointColor(newLinkId);

            if (string.IsNullOrWhiteSpace(newLinkId))
            {
                dc.Target = commandList;
                dc.BeginDraw();
                dc.Clear(null);
                dc.EndDraw();
                dc.Target = null;
                commandList.Close();

                CacheTransformState(newX, newY, newSize, newScreenWidth, newScreenHeight, string.Empty, transform, newIsHidden);
                return;
            }

            dc.Target = commandList;
            dc.BeginDraw();
            dc.Clear(null);
            if (!newIsHidden)
            {
                var center = TransformPoint(newLinkId, newX, newY, newScreenWidth, newScreenHeight);
                var halfModel = Math.Max(4.0f / 500.0f, newSize * 0.6f);
                var horizontal = TransformVector(newLinkId, halfModel, 0.0f, newScreenWidth, newScreenHeight);
                var vertical = TransformVector(newLinkId, 0.0f, halfModel, newScreenWidth, newScreenHeight);
                var leftPixel = center - horizontal;
                var rightPixel = center + horizontal;
                var topPixel = center + vertical;
                var bottomPixel = center - vertical;
                dc.DrawLine(leftPixel, rightPixel, brush, 4.0f);
                dc.DrawLine(bottomPixel, topPixel, brush, 4.0f);
            }
            dc.EndDraw();
            dc.Target = null;
            commandList.Close();

            CacheTransformState(newX, newY, newSize, newScreenWidth, newScreenHeight, newLinkId, transform, newIsHidden);
        }

        private Vector2 TransformPoint(string linkId, float x, float y, int screenWidth, int screenHeight)
        {
            var state = GetTransformState(linkId);
            var localPoint = InteractionShapeTransform.PixelToLocal(new Vector2(x, y), screenWidth, screenHeight);
            return InteractionShapeTransform.TransformTargetPointToPixel(localPoint, state, screenWidth, screenHeight);
        }

        private Vector2 TransformVector(string linkId, float x, float y, int screenWidth, int screenHeight)
        {
            var state = GetTransformState(linkId);
            var localVector = InteractionShapeTransform.PixelToLocal(new Vector2(x, y), screenWidth, screenHeight);
            return InteractionShapeTransform.TransformLocalVectorToPixel(localVector, state, screenWidth, screenHeight);
        }

        private void CacheTransformState(
            float newX,
            float newY,
            float newSize,
            int newScreenWidth,
            int newScreenHeight,
            string newLinkId,
            Live2DInteractionStore.InteractionTransformState transform,
            bool newIsHidden)
        {
            x = newX;
            y = newY;
            size = newSize;
            screenWidth = newScreenWidth;
            screenHeight = newScreenHeight;
            currentLinkId = newLinkId;
            isHidden = newIsHidden;
            transformPositionX = transform.PositionX;
            transformPositionY = transform.PositionY;
            transformScale = transform.Scale;
            transformRotationDegrees = transform.RotationDegrees;
            transformModelCenterX = transform.ModelCenterX;
            transformModelCenterY = transform.ModelCenterY;
            transformHitTestScaleX = transform.HitTestScaleX;
            transformHitTestScaleY = transform.HitTestScaleY;
            transformHitTestTranslateX = transform.HitTestTranslateX;
            transformHitTestTranslateY = transform.HitTestTranslateY;
        }

        private static Live2DInteractionStore.InteractionTransformState GetTransformState(string linkId)
        {
            if (Live2DInteractionStore.TryGetInteractionTransform(linkId, out var state) && state is not null)
            {
                return state;
            }

            return new Live2DInteractionStore.InteractionTransformState(0, 0, 1, 0, 0, 0, 1, 1, 0, 0, 1920, 1080);
        }

        public void Dispose()
        {
            Live2DInteractionStore.RemoveTargetPoint(sourceId);
            commandList?.Dispose();
            brush.Dispose();
        }
    }
}
