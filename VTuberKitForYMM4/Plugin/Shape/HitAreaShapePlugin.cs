using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
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
    public class HitAreaShapePlugin : IShapePlugin
    {
        public string Name => Translate.Plugin_HitArea_Name;
        public bool IsExoShapeSupported => false;
        public bool IsExoMaskSupported => false;

        public IShapeParameter CreateShapeParameter(SharedDataStore? sharedData) => new HitAreaShapeParameter(sharedData);
    }

    internal class HitAreaShapeParameter : ShapeParameterBase
    {
        private InteractionTargetViewModel? targetCharacter;

        [Display(Name = nameof(Translate.Hit_Character_Name), Description = nameof(Translate.Hit_Character_Desc), ResourceType = typeof(Translate))]
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

        [Display(Name = nameof(Translate.Hit_LinkId_Name), Description = nameof(Translate.Hit_LinkId_Desc), ResourceType = typeof(Translate))]
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

        [Display(Name = nameof(Translate.Hit_HitArea_Name), Description = nameof(Translate.Hit_HitArea_Desc), ResourceType = typeof(Translate))]
        [CustomComboBox]
        public HitAreaIdViewModel HitArea { get; set; }

        [Browsable(false)]
        public string HitAreaName
        {
            get
            {
                var selectedId = HitArea?.SelectedId;
                return string.IsNullOrWhiteSpace(selectedId) ? hitAreaName : selectedId;
            }
            set
            {
                var normalized = value ?? string.Empty;
                Set(ref hitAreaName, normalized);
                if (HitArea != null)
                {
                    HitArea.SelectedId = normalized;
                }
            }
        }
        string hitAreaName = string.Empty;

        [Display(Name = nameof(Translate.Hit_Motion_Name), Description = nameof(Translate.Hit_Motion_Desc), ResourceType = typeof(Translate))]
        [CustomComboBox]
        public MotionViewModel Motion { get; set; }

        [Browsable(false)]
        public string MotionGroup
        {
            get
            {
                var selected = Motion?.SelectedValue as MotionItem;
                if (selected == null)
                {
                    return motionGroup;
                }

                return selected.IsNone ? string.Empty : selected.Group;
            }
            set
            {
                var normalized = value ?? string.Empty;
                Set(ref motionGroup, normalized);
                SyncMotionSelection();
            }
        }
        string motionGroup = string.Empty;

        [Browsable(false)]
        public int MotionIndex
        {
            get
            {
                var selected = Motion?.SelectedValue as MotionItem;
                if (selected == null)
                {
                    return motionIndex;
                }

                return selected.IsNone ? -1 : selected.Index;
            }
            set
            {
                Set(ref motionIndex, value);
                SyncMotionSelection();
            }
        }
        int motionIndex = -1;

        [Display(Name = nameof(Translate.Hit_Expression_Name), Description = nameof(Translate.Hit_Expression_Desc), ResourceType = typeof(Translate))]
        [CustomComboBox]
        public ExpressionViewModel Expression { get; set; }

        [Browsable(false)]
        public string ExpressionId
        {
            get
            {
                var selectedId = Expression?.SelectedExpressionId;
                return string.IsNullOrWhiteSpace(selectedId) ? expressionId : selectedId;
            }
            set
            {
                var normalized = value ?? string.Empty;
                Set(ref expressionId, normalized);
                if (Expression != null)
                {
                    Expression.SelectedExpressionId = normalized;
                }
            }
        }
        string expressionId = string.Empty;

        [Display(Name = nameof(Translate.Hit_IsHidden_Name), Description = nameof(Translate.Hit_IsHidden_Desc), ResourceType = typeof(Translate))]
        [ToggleSlider]
        [DefaultValue(false)]
        public bool IsHidden { get => isHidden; set => Set(ref isHidden, value); }
        bool isHidden;

        [Display(Name = nameof(Translate.Hit_X_Name), Description = nameof(Translate.Hit_X_Desc), ResourceType = typeof(Translate))]
        [AnimatedHoldSlider("F1", "px", -500.0, 500.0)]
        public Animation X { get; } = new Animation(0, -100000, 100000);

        [Display(Name = nameof(Translate.Hit_Y_Name), Description = nameof(Translate.Hit_Y_Desc), ResourceType = typeof(Translate))]
        [AnimatedHoldSlider("F1", "px", -500.0, 500.0)]
        public Animation Y { get; } = new Animation(0, -100000, 100000);

        [Display(Name = nameof(Translate.Hit_Width_Name), Description = nameof(Translate.Hit_Width_Desc), ResourceType = typeof(Translate))]
        [AnimatedHoldSlider("F1", "px", 0.0, 500.0)]
        public Animation Width { get; } = new Animation(200, 0, 100000);

        [Display(Name = nameof(Translate.Hit_Height_Name), Description = nameof(Translate.Hit_Height_Desc), ResourceType = typeof(Translate))]
        [AnimatedHoldSlider("F1", "px", 0.0, 500.0)]
        public Animation Height { get; } = new Animation(200, 0, 100000);

        [Display(GroupName = nameof(Translate.Group_Parameter), Name = nameof(Translate.Hit_DynamicOverrides_Name), Description = nameof(Translate.Hit_DynamicOverrides_Desc), ResourceType = typeof(Translate))]
        [DynamicFaceOverridesEditor(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public Live2DFaceDynamicOverrides DynamicOverrides { get; } = new();

        [Browsable(false)]
        public int DynamicOverridesRevision
        {
            get => dynamicOverridesRevision;
            private set => Set(ref dynamicOverridesRevision, value);
        }
        int dynamicOverridesRevision;

        public HitAreaShapeParameter(SharedDataStore? sharedData) : base(sharedData)
        {
            HitArea = new HitAreaIdViewModel("HitArea", ResolveTargetModelFile);
            Motion = new MotionViewModel("Idle", modelPathProvider: ResolveTargetModelFile);
            Expression = new ExpressionViewModel("exp", ResolveTargetModelFile);
            TargetCharacter = CreateTargetCharacter();
            HitArea.PropertyChanged += HitArea_PropertyChanged;
            Motion.PropertyChanged += Motion_PropertyChanged;
            Expression.PropertyChanged += Expression_PropertyChanged;
            DynamicOverrides.PropertyChanged += DynamicOverrides_PropertyChanged;
            SyncDynamicOverridesModelFile();
        }

        public HitAreaShapeParameter() : this(null)
        {
        }

        public override IEnumerable<string> CreateMaskExoFilter(int keyFrameIndex, ExoOutputDescription desc, ShapeMaskExoOutputDescription shapeMaskDesc) => [];

        public override IEnumerable<string> CreateShapeItemExoFilter(int keyFrameIndex, ExoOutputDescription desc) => [];

        public override IShapeSource CreateShapeSource(IGraphicsDevicesAndContext devices) => new HitAreaShapeSource(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [X, Y, Width, Height, DynamicOverrides];

        protected override void LoadSharedData(SharedDataStore store)
        {
            if (store.Load<SharedData>() is not { } data)
                return;

            LinkId = data.LinkId;
            HitAreaName = data.HitAreaName;
            ExpressionId = data.ExpressionId;
            MotionGroup = data.MotionGroup;
            MotionIndex = data.MotionIndex;
            X.CopyFrom(data.X);
            Y.CopyFrom(data.Y);
            Width.CopyFrom(data.Width);
            Height.CopyFrom(data.Height);
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
                if (!string.IsNullOrWhiteSpace(linkId))
                {
                    ClearReactionSelections();
                }

                Set(ref linkId, selected, nameof(LinkId));
                HitArea.UpdateItemsSource();
                HitArea.UpdateSelectedValue();
                Motion.UpdateItemsSource();
                Motion.UpdateSelectedValue();
                Expression.UpdateItemsSource();
                Expression.UpdateSelectedValue();
                SyncDynamicOverridesModelFile();
            }

            OnPropertyChanged(nameof(TargetCharacter));
            OnPropertyChanged(nameof(LinkId));
        }

        private void HitArea_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(HitAreaIdViewModel.SelectedId))
            {
                return;
            }

            OnPropertyChanged(nameof(HitArea));
            OnPropertyChanged(nameof(HitAreaName));
        }

        private void Motion_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(MotionViewModel.SelectedGroup) &&
                e.PropertyName != nameof(MotionViewModel.SelectedIndex) &&
                e.PropertyName != nameof(MotionViewModel.SelectedValue))
            {
                return;
            }

            OnPropertyChanged(nameof(Motion));
            OnPropertyChanged(nameof(MotionGroup));
            OnPropertyChanged(nameof(MotionIndex));
        }

        private void Expression_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ExpressionViewModel.SelectedExpressionId))
            {
                return;
            }

            OnPropertyChanged(nameof(Expression));
            OnPropertyChanged(nameof(ExpressionId));
        }

        private string ResolveTargetModelFile()
        {
            var resolvedLinkId = LinkId;
            return Live2DInteractionStore.GetInteractionTargetModelFile(resolvedLinkId);
        }

        private void SyncDynamicOverridesModelFile()
        {
            DynamicOverrides.ModelFile = ResolveTargetModelFile();
        }

        private void DynamicOverrides_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            DynamicOverridesRevision++;
            OnPropertyChanged(nameof(DynamicOverrides));
        }

        private void ClearReactionSelections()
        {
            HitAreaName = string.Empty;
            MotionGroup = string.Empty;
            MotionIndex = -1;
            ExpressionId = string.Empty;
        }

        private void SyncMotionSelection()
        {
            if (Motion == null)
                return;

            if (Motion.ItemsSource.Count == 0)
            {
                Motion.UpdateItemsSource();
            }

            if (motionIndex < 0)
            {
                Motion.SelectedValue = Motion.ItemsSource.FirstOrDefault() ?? new MotionItem { IsNone = true, Group = string.Empty, Index = -1, FileName = string.Empty };
                Motion.SelectedDisplayMember = Motion.SelectedValue?.DisplayMember ?? string.Empty;
                return;
            }

            var found = Motion.ItemsSource
                .OfType<MotionItem>()
                .FirstOrDefault(x => !x.IsNone && x.Group == motionGroup && x.Index == motionIndex);
            if (found != null)
            {
                Motion.SelectedValue = found;
                Motion.SelectedDisplayMember = found.DisplayMember;
            }
        }

        private sealed class SharedData
        {
            public string LinkId { get; set; } = string.Empty;
            public string HitAreaName { get; set; } = string.Empty;
            public string ExpressionId { get; set; } = string.Empty;
            public string MotionGroup { get; set; } = string.Empty;
            public int MotionIndex { get; set; } = -1;
            public Animation X { get; } = new Animation(0, -1, 1);
            public Animation Y { get; } = new Animation(0, -1, 1);
            public Animation Width { get; } = new Animation(0.2, 0.02, 2.0);
            public Animation Height { get; } = new Animation(0.2, 0.02, 2.0);
            public bool IsHidden { get; set; }

            public SharedData(HitAreaShapeParameter parameter)
            {
                LinkId = parameter.LinkId;
                HitAreaName = parameter.HitAreaName;
                ExpressionId = parameter.ExpressionId;
                MotionGroup = parameter.MotionGroup;
                MotionIndex = parameter.MotionIndex;
                X.CopyFrom(parameter.X);
                Y.CopyFrom(parameter.Y);
                Width.CopyFrom(parameter.Width);
                Height.CopyFrom(parameter.Height);
                IsHidden = parameter.IsHidden;
            }
        }
    }

    internal sealed class HitAreaShapeSource : IShapeSource
    {
        private readonly string sourceId = Guid.NewGuid().ToString("N");
        private readonly IGraphicsDevicesAndContext devices;
        private readonly HitAreaShapeParameter parameter;
        private readonly ID2D1SolidColorBrush hitBrush;
        private readonly ID2D1SolidColorBrush missBrush;
        private ID2D1CommandList? commandList;
        private string currentLinkId = string.Empty;
        private float x;
        private float y;
        private float width;
        private float height;
        private bool isHit;
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

        public HitAreaShapeSource(IGraphicsDevicesAndContext devices, HitAreaShapeParameter parameter)
        {
            this.devices = devices;
            this.parameter = parameter;
            hitBrush = devices.DeviceContext.CreateSolidColorBrush(InteractionShapeColors.GetHitAreaHitColor(parameter.LinkId));
            missBrush = devices.DeviceContext.CreateSolidColorBrush(InteractionShapeColors.GetHitAreaMissColor(parameter.LinkId));
            currentLinkId = parameter.LinkId;
        }

        public void Update(TimelineItemSourceDescription timelineItemSourceDescription)
        {
            var fps = timelineItemSourceDescription.FPS;
            var frame = timelineItemSourceDescription.ItemPosition.Frame;
            var length = timelineItemSourceDescription.ItemDuration.Frame;

            var newX = (float)parameter.X.GetValue(frame, length, fps);
            var newY = (float)parameter.Y.GetValue(frame, length, fps);
            var newWidth = (float)parameter.Width.GetValue(frame, length, fps);
            var newHeight = (float)parameter.Height.GetValue(frame, length, fps);
            var newLinkId = parameter.LinkId;
            var newScreenWidth = Math.Max(1, timelineItemSourceDescription.ScreenSize.Width);
            var newScreenHeight = Math.Max(1, timelineItemSourceDescription.ScreenSize.Height);
            var transform = GetTransformState(newLinkId);
            var newIsHidden = parameter.IsHidden;

            var newIsHit = isHit;
            if (string.IsNullOrWhiteSpace(newLinkId))
            {
                Live2DInteractionStore.RemoveHitAreaRect(sourceId);
                newIsHit = false;
            }
            else
            {
                Live2DInteractionStore.UpdateHitAreaRect(
                    sourceId,
                    newLinkId,
                    parameter.HitAreaName,
                    parameter.ExpressionId,
                    parameter.MotionGroup,
                    parameter.MotionIndex,
                    ResolveParameterOverrides(frame, length, fps),
                    ResolvePartOverrides(frame, length, fps),
                    newX,
                    newY,
                    newWidth,
                    newHeight,
                    timelineItemSourceDescription.Layer);
                if (Live2DInteractionStore.TryGetHitAreaRect(sourceId, out var state) && state != null)
                {
                    newIsHit = state.IsHit;
                }
            }

            if (commandList != null &&
                currentLinkId == newLinkId &&
                x == newX &&
                y == newY &&
                width == newWidth &&
                height == newHeight &&
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
                this.isHit == newIsHit &&
                isHidden == newIsHidden)
                return;

            var dc = devices.DeviceContext;
            commandList?.Dispose();
            commandList = dc.CreateCommandList();
            hitBrush.Color = InteractionShapeColors.GetHitAreaHitColor(newLinkId);
            missBrush.Color = InteractionShapeColors.GetHitAreaMissColor(newLinkId);

            if (string.IsNullOrWhiteSpace(newLinkId))
            {
                dc.Target = commandList;
                dc.BeginDraw();
                dc.Clear(null);
                dc.EndDraw();
                dc.Target = null;
                commandList.Close();

                x = newX;
                y = newY;
                width = newWidth;
                height = newHeight;
                this.isHit = false;
                screenWidth = newScreenWidth;
                screenHeight = newScreenHeight;
                currentLinkId = string.Empty;
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
                isHidden = newIsHidden;
                return;
            }

            dc.Target = commandList;
            dc.BeginDraw();
            dc.Clear(null);
            if (!newIsHidden)
            {
                var topLeft = TransformPoint(transform, newX - newWidth / 2.0f, newY + newHeight / 2.0f, newScreenWidth, newScreenHeight);
                var topRight = TransformPoint(transform, newX + newWidth / 2.0f, newY + newHeight / 2.0f, newScreenWidth, newScreenHeight);
                var bottomRight = TransformPoint(transform, newX + newWidth / 2.0f, newY - newHeight / 2.0f, newScreenWidth, newScreenHeight);
                var bottomLeft = TransformPoint(transform, newX - newWidth / 2.0f, newY - newHeight / 2.0f, newScreenWidth, newScreenHeight);
                var activeBrush = newIsHit ? hitBrush : missBrush;
                dc.DrawLine(topLeft, topRight, activeBrush, 4.0f);
                dc.DrawLine(topRight, bottomRight, activeBrush, 4.0f);
                dc.DrawLine(bottomRight, bottomLeft, activeBrush, 4.0f);
                dc.DrawLine(bottomLeft, topLeft, activeBrush, 4.0f);
            }
            dc.EndDraw();
            dc.Target = null;
            commandList.Close();

            x = newX;
            y = newY;
            width = newWidth;
            height = newHeight;
            this.isHit = newIsHit;
            screenWidth = newScreenWidth;
            screenHeight = newScreenHeight;
            currentLinkId = newLinkId;
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
            isHidden = newIsHidden;
        }

        private static Vector2 TransformPoint(Live2DInteractionStore.InteractionTransformState state, float x, float y, int screenWidth, int screenHeight)
        {
            var localPoint = InteractionShapeTransform.PixelToLocal(new Vector2(x, y), screenWidth, screenHeight);
            return InteractionShapeTransform.TransformHitBoxPointToPixel(localPoint, Vector2.Zero, state, screenWidth, screenHeight);
        }

        private Live2DInteractionStore.HitAreaParameterOverrideState[] ResolveParameterOverrides(long frame, long length, int fps)
        {
            return parameter.DynamicOverrides.GetParameterRowsSnapshot()
                .Where(x => x.Hold && !string.IsNullOrWhiteSpace(x.Id))
                .Select(x => new Live2DInteractionStore.HitAreaParameterOverrideState(x.Id, x.GetValue(frame, length, fps)))
                .ToArray();
        }

        private Live2DInteractionStore.HitAreaPartOverrideState[] ResolvePartOverrides(long frame, long length, int fps)
        {
            return parameter.DynamicOverrides.GetPartRowsSnapshot()
                .Where(x => x.Hold && !string.IsNullOrWhiteSpace(x.Id))
                .Select(x => new Live2DInteractionStore.HitAreaPartOverrideState(
                    x.Id,
                    Math.Clamp((float)x.Opacity.GetValue(frame, length, fps), 0.0f, 1.0f)))
                .ToArray();
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
            Live2DInteractionStore.RemoveHitAreaRect(sourceId);
            commandList?.Dispose();
            hitBrush.Dispose();
            missBrush.Dispose();
        }
    }
}
