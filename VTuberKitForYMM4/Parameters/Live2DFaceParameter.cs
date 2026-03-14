using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using VTuberKitForYMM4.Commons.CustomPropertyEditor;
using VTuberKitForYMM4.Plugin.CustomPropertyEditor;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Tachie;

namespace VTuberKitForYMM4.Plugin
{
    public class Live2DFaceParameter : TachieFaceParameterBase
    {
        public Live2DFaceParameter()
        {
            Motion = new MotionViewModel("Idle", modelPathProvider: () => ModelFile);
            Expression = new ExpressionViewModel("exp", () => ModelFile);
            DynamicOverrides.PropertyChanged += DynamicOverrides_PropertyChanged;
        }

        [Display(Name = nameof(Translate.Face_Motion_Name), Description = nameof(Translate.Face_Motion_Desc), ResourceType = typeof(Translate))]
        [CustomComboBox]
        public MotionViewModel Motion { get; set; }

        [Display(Name = nameof(Translate.Face_Expression_Name), Description = nameof(Translate.Face_Expression_Desc), ResourceType = typeof(Translate))]
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

        [Display(Name = nameof(Translate.Face_MotionLoop_Name), Description = nameof(Translate.Face_MotionLoop_Desc), ResourceType = typeof(Translate))]
        [ToggleSlider]
        [DefaultValue(false)]
        public bool MotionLoop { get => motionLoop; set => Set(ref motionLoop, value); }
        bool motionLoop = false;

        [Display(Name = nameof(Translate.Face_IsHidden_Name), Description = nameof(Translate.Face_IsHidden_Desc), ResourceType = typeof(Translate))]
        [ToggleSlider]
        [DefaultValue(false)]
        public bool IsHidden { get => isHidden; set => Set(ref isHidden, value); }
        bool isHidden;

        [Browsable(false)]
        [AnimatedHoldSlider("F3", "", 0.0, 1.0)]
        public Animation EyeLOpen { get; } = new(1, 0, 1);

        [Browsable(false)]
        public bool EyeLOpenHold { get => eyeLOpenHold; set => Set(ref eyeLOpenHold, value); }
        bool eyeLOpenHold;

        [Browsable(false)]
        [AnimatedHoldSlider("F3", "", 0.0, 1.0)]
        public Animation EyeROpen { get; } = new(1, 0, 1);

        [Browsable(false)]
        public bool EyeROpenHold { get => eyeROpenHold; set => Set(ref eyeROpenHold, value); }
        bool eyeROpenHold;

        [Browsable(false)]
        [AnimatedHoldSlider("F3", "", 0.0, 1.0)]
        public Animation MouthOpen { get; } = new(0, 0, 1);

        [Browsable(false)]
        public bool MouthOpenHold { get => mouthOpenHold; set => Set(ref mouthOpenHold, value); }
        bool mouthOpenHold;

        [Browsable(false)]
        [AnimatedHoldSlider("F3", "", -1.0, 1.0)]
        public Animation MouthForm { get; } = new(0, -1, 1);

        [Browsable(false)]
        public bool MouthFormHold { get => mouthFormHold; set => Set(ref mouthFormHold, value); }
        bool mouthFormHold;

        [Browsable(false)]
        [AnimatedHoldSlider("F3", "", -30.0, 30.0)]
        public Animation AngleX { get; } = new(0, -30, 30);

        [Browsable(false)]
        public bool AngleXHold { get => angleXHold; set => Set(ref angleXHold, value); }
        bool angleXHold;

        [Browsable(false)]
        [AnimatedHoldSlider("F3", "", -30.0, 30.0)]
        public Animation AngleY { get; } = new(0, -30, 30);

        [Browsable(false)]
        public bool AngleYHold { get => angleYHold; set => Set(ref angleYHold, value); }
        bool angleYHold;

        [Browsable(false)]
        [AnimatedHoldSlider("F3", "", -30.0, 30.0)]
        public Animation AngleZ { get; } = new(0, -30, 30);

        [Browsable(false)]
        public bool AngleZHold { get => angleZHold; set => Set(ref angleZHold, value); }
        bool angleZHold;

        [Browsable(false)]
        [AnimatedHoldSlider("F3", "", -10.0, 10.0)]
        public Animation BodyAngleX { get; } = new(0, -10, 10);

        [Browsable(false)]
        public bool BodyAngleXHold { get => bodyAngleXHold; set => Set(ref bodyAngleXHold, value); }
        bool bodyAngleXHold;

        [Browsable(false)]
        [AnimatedHoldSlider("F3", "", -1.0, 1.0)]
        public Animation EyeBallX { get; } = new(0, -1, 1);

        [Browsable(false)]
        public bool EyeBallXHold { get => eyeBallXHold; set => Set(ref eyeBallXHold, value); }
        bool eyeBallXHold;

        [Browsable(false)]
        [AnimatedHoldSlider("F3", "", -1.0, 1.0)]
        public Animation EyeBallY { get; } = new(0, -1, 1);

        [Browsable(false)]
        public bool EyeBallYHold { get => eyeBallYHold; set => Set(ref eyeBallYHold, value); }
        bool eyeBallYHold;

        [Browsable(false)]
        [AnimatedHoldSlider("F3", "", 0.0, 1.0)]
        public Animation Cheek { get; } = new(0, 0, 1);

        [Browsable(false)]
        public bool CheekHold { get => cheekHold; set => Set(ref cheekHold, value); }
        bool cheekHold;

        [Browsable(false)]
        [AnimatedHoldSlider("F3", "", -1.0, 1.0)]
        public Animation ArmLA { get; } = new(0, -1, 1);

        [Browsable(false)]
        public bool ArmLAHold { get => armLAHold; set => Set(ref armLAHold, value); }
        bool armLAHold;

        [Browsable(false)]
        [AnimatedHoldSlider("F3", "", -1.0, 1.0)]
        public Animation ArmRA { get; } = new(0, -1, 1);

        [Browsable(false)]
        public bool ArmRAHold { get => armRAHold; set => Set(ref armRAHold, value); }
        bool armRAHold;

        [Display(Name = nameof(Translate.Face_OffsetPositionX_Name), Description = nameof(Translate.Face_OffsetPositionX_Desc), ResourceType = typeof(Translate))]
        [AnimatedHoldSlider("F1", "px", -500.0, 500.0)]
        public Animation OffsetPositionX { get; } = new Animation(0, -100000, 100000);

        [Display(Name = nameof(Translate.Face_OffsetPositionY_Name), Description = nameof(Translate.Face_OffsetPositionY_Desc), ResourceType = typeof(Translate))]
        [AnimatedHoldSlider("F1", "px", -500.0, 500.0)]
        public Animation OffsetPositionY { get; } = new Animation(0, -100000, 100000);

        [Display(Name = nameof(Translate.Face_OffsetScale_Name), Description = nameof(Translate.Face_OffsetScale_Desc), ResourceType = typeof(Translate))]
        [AnimatedHoldSlider("F1", "%", 0.0, 400.0)]
        public Animation OffsetScale { get; } = new Animation(100, 0, 100000);

        [Display(Name = nameof(Translate.Face_OffsetRotation_Name), Description = nameof(Translate.Face_OffsetRotation_Desc), ResourceType = typeof(Translate))]
        [AnimatedHoldSlider("F1", "°", -360.0, 360.0)]
        public Animation OffsetRotation { get; } = new Animation(0, -360, 360);

        [Display(GroupName = nameof(Translate.Group_Parameter), Name = nameof(Translate.Face_DynamicOverrides_Name), Description = nameof(Translate.Face_DynamicOverrides_Desc), ResourceType = typeof(Translate))]
        [DynamicFaceOverridesEditor(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public Live2DFaceDynamicOverrides DynamicOverrides { get; } = new();

        [Browsable(false)]
        public int DynamicOverridesRevision
        {
            get => dynamicOverridesRevision;
            private set => Set(ref dynamicOverridesRevision, value);
        }
        int dynamicOverridesRevision;

        protected override IEnumerable<IAnimatable> GetAnimatables() =>
        [
            OffsetPositionX,
            OffsetPositionY,
            OffsetScale,
            OffsetRotation,
            DynamicOverrides
        ];

        private void DynamicOverrides_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            DynamicOverridesRevision++;
            OnPropertyChanged(nameof(DynamicOverrides));
        }

    }
}
