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

        [Display(Name = "モーション", Description = "model3.json の Motions から選択（Idle含む）")]
        [CustomComboBox]
        public MotionViewModel Motion { get; set; }

        [Display(Name = "表情", Description = "model3.json の Expressions から選択")]
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

        [Display(Name = "モーションループ", Description = "ONで face モーションを繰り返し再生します")]
        [ToggleSlider]
        [DefaultValue(false)]
        public bool MotionLoop { get => motionLoop; set => Set(ref motionLoop, value); }
        bool motionLoop = false;

        [Display(Name = "不透明度", Description = "モデル全体の不透明度")]
        [AnimatedHoldSlider("F3", "", 0.0, 1.0)]
        public Animation Opacity { get; } = new(1, 0, 1);

        [Browsable(false)]
        public bool OpacityHold { get => opacityHold; set => Set(ref opacityHold, value); }
        bool opacityHold;

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

        [Display(Name = "表示位置Xオフセット", Description = "Itemの位置Xに加算する一時オフセット")]
        [AnimatedHoldSlider("F3", "", -2.0, 2.0)]
        public Animation OffsetPositionX { get; } = new Animation(0, -2, 2);

        [Display(Name = "表示位置Yオフセット", Description = "Itemの位置Yに加算する一時オフセット")]
        [AnimatedHoldSlider("F3", "", -2.0, 2.0)]
        public Animation OffsetPositionY { get; } = new Animation(0, -2, 2);

        [Display(Name = "表示拡大率オフセット", Description = "Itemの拡大率に加算する一時オフセット")]
        [AnimatedHoldSlider("F3", "x", -2.0, 2.0)]
        public Animation OffsetScale { get; } = new Animation(0, -2, 2);

        [Display(Name = "表示回転オフセット", Description = "Itemの回転に加算する一時オフセット")]
        [AnimatedHoldSlider("F1", "°", -180.0, 180.0)]
        public Animation OffsetRotation { get; } = new Animation(0, -180, 180);

        [Display(GroupName = "パラメータ", Name = "一覧", Description = "Cubism標準パラメータとモデル固有のParam/Partを一覧で編集します。")]
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
            Opacity,
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
