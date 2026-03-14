using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using VTuberKitForYMM4.Commons.CustomPropertyEditor;
using VTuberKitForYMM4.Plugin.CustomPropertyEditor;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Tachie;

namespace VTuberKitForYMM4.Plugin
{
    public class Live2DItemParameter : TachieItemParameterBase
    {
        public Live2DItemParameter()
        {
            Motion = new MotionViewModel("Idle", modelPathProvider: () => ModelFile);
            Expression = new ExpressionViewModel("exp", () => ModelFile);
            DynamicOverrides.PropertyChanged += DynamicOverrides_PropertyChanged;
        }

        [Display(Name = nameof(Translate.Item_Motion_Name), Description = nameof(Translate.Item_Motion_Desc), ResourceType = typeof(Translate))]
        [CustomComboBox]
        public MotionViewModel Motion { get; set; }

        [Display(Name = nameof(Translate.Item_Expression_Name), Description = nameof(Translate.Item_Expression_Desc), ResourceType = typeof(Translate))]
        [CustomComboBox]
        public ExpressionViewModel Expression { get; set; }

        [Browsable(false)]
        public string ModelFile
        {
            get => modelFile;
            set
            {
                var normalized = value ?? string.Empty;
                if (!Set(ref modelFile, normalized))
                {
                    return;
                }

                ModelMetadataCatalog.UpdateFromModelPath(normalized);
                DynamicOverrides.ModelFile = normalized;
            }
        }
        string modelFile = string.Empty;

        [Browsable(false)]
        public string ExpressionId
        {
            get
            {
                return Expression?.SelectedExpressionId ?? string.Empty;
            }
        }

        [Display(Name = nameof(Translate.Item_MotionLoop_Name), Description = nameof(Translate.Item_MotionLoop_Desc), ResourceType = typeof(Translate))]
        [ToggleSlider]
        [DefaultValue(true)]
        public bool MotionLoop { get => motionLoop; set => Set(ref motionLoop, value); }
        bool motionLoop = true;

        [Display(Name = nameof(Translate.Item_IsHidden_Name), Description = nameof(Translate.Item_IsHidden_Desc), ResourceType = typeof(Translate))]
        [ToggleSlider]
        [DefaultValue(false)]
        public bool IsHidden { get => isHidden; set => Set(ref isHidden, value); }
        bool isHidden;

        [Browsable(false)]
        public string MotionGroup
        {
            get
            {
                return Motion?.SelectedGroup ?? string.Empty;
            }
        }

        [Browsable(false)]
        public int MotionIndex
        {
            get
            {
                return Motion?.SelectedIndex ?? -1;
            }
        }

        [Display(GroupName = nameof(Translate.Group_Parameter), Name = nameof(Translate.Item_DynamicOverrides_Name), Description = nameof(Translate.Item_DynamicOverrides_Desc), ResourceType = typeof(Translate))]
        [DynamicFaceOverridesEditor(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public Live2DFaceDynamicOverrides DynamicOverrides { get; } = new();

        [Browsable(false)]
        public int DynamicOverridesRevision
        {
            get => dynamicOverridesRevision;
            private set => Set(ref dynamicOverridesRevision, value);
        }
        int dynamicOverridesRevision;

        [Display(Name = nameof(Translate.Item_MultiplyR_Name), Description = nameof(Translate.Item_MultiplyR_Desc), ResourceType = typeof(Translate))]
        [AnimatedHoldSlider("F1", "%", 0.0, 100.0)]
        public Animation MultiplyR { get; } = new Animation(100, 0, 100);

        [Display(Name = nameof(Translate.Item_MultiplyG_Name), Description = nameof(Translate.Item_MultiplyG_Desc), ResourceType = typeof(Translate))]
        [AnimatedHoldSlider("F1", "%", 0.0, 100.0)]
        public Animation MultiplyG { get; } = new Animation(100, 0, 100);

        [Display(Name = nameof(Translate.Item_MultiplyB_Name), Description = nameof(Translate.Item_MultiplyB_Desc), ResourceType = typeof(Translate))]
        [AnimatedHoldSlider("F1", "%", 0.0, 100.0)]
        public Animation MultiplyB { get; } = new Animation(100, 0, 100);

        [Display(Name = nameof(Translate.Item_MultiplyA_Name), Description = nameof(Translate.Item_MultiplyA_Desc), ResourceType = typeof(Translate))]
        [AnimatedHoldSlider("F1", "%", 0.0, 100.0)]
        public Animation MultiplyA { get; } = new Animation(100, 0, 100);

        [Display(Name = nameof(Translate.Item_PositionX_Name), Description = nameof(Translate.Item_PositionX_Desc), ResourceType = typeof(Translate))]
        [AnimatedHoldSlider("F1", "px", -500.0, 500.0)]
        public Animation PositionX { get; } = new Animation(0, -100000, 100000);

        [Display(Name = nameof(Translate.Item_PositionY_Name), Description = nameof(Translate.Item_PositionY_Desc), ResourceType = typeof(Translate))]
        [AnimatedHoldSlider("F1", "px", -500.0, 500.0)]
        public Animation PositionY { get; } = new Animation(0, -100000, 100000);

        [Display(Name = nameof(Translate.Item_Scale_Name), Description = nameof(Translate.Item_Scale_Desc), ResourceType = typeof(Translate))]
        [AnimatedHoldSlider("F1", "%", 0.0, 400.0)]
        public Animation Scale { get; } = new Animation(100, 0, 100000);

        [Display(Name = nameof(Translate.Item_Rotation_Name), Description = nameof(Translate.Item_Rotation_Desc), ResourceType = typeof(Translate))]
        [AnimatedHoldSlider("F1", "°", -360.0, 360.0)]
        public Animation Rotation { get; } = new Animation(0, -360, 360);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [MultiplyR, MultiplyG, MultiplyB, MultiplyA, PositionX, PositionY, Scale, Rotation, DynamicOverrides];

        private void DynamicOverrides_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            DynamicOverridesRevision++;
            OnPropertyChanged(nameof(DynamicOverrides));
        }
    }
}

