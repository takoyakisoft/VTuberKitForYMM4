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
        }

        [Display(Name = "モーション", Description = "モデル表示中に継続再生するモーションを選択（Idle含む）")]
        [CustomComboBox]
        public MotionViewModel Motion { get; set; }

        [Display(Name = "表情", Description = "model3.json の Expressions から選択")]
        [CustomComboBox]
        public ExpressionViewModel Expression { get; set; }

        [Browsable(false)]
        public string ModelFile
        {
            get => modelFile;
            set => Set(ref modelFile, value ?? string.Empty);
        }
        string modelFile = string.Empty;

        [System.ComponentModel.Browsable(false)]
        public string ExpressionId
        {
            get
            {
                return Expression?.SelectedExpressionId ?? string.Empty;
            }
        }

        [Display(Name = "モーションループ", Description = "ONでモーションを繰り返し再生します")]
        [ToggleSlider]
        [DefaultValue(true)]
        public bool MotionLoop { get => motionLoop; set => Set(ref motionLoop, value); }
        bool motionLoop = true;

        [System.ComponentModel.Browsable(false)]
        public string MotionGroup
        {
            get
            {
                return Motion?.SelectedGroup ?? string.Empty;
            }
        }

        [System.ComponentModel.Browsable(false)]
        public int MotionIndex
        {
            get
            {
                return Motion?.SelectedIndex ?? -1;
            }
        }

        [Display(Name = "不透明度", Description = "モデル全体の不透明度")]
        [AnimatedHoldSlider("F2", "", 0.0, 1.0)]
        public Animation Opacity { get; } = new Animation(1, 0, 1);

        [Display(Name = "乗算色R", Description = "モデルカラー乗算のR")]
        [AnimatedHoldSlider("F2", "", 0.0, 1.0)]
        public Animation MultiplyR { get; } = new Animation(1, 0, 1);

        [Display(Name = "乗算色G", Description = "モデルカラー乗算のG")]
        [AnimatedHoldSlider("F2", "", 0.0, 1.0)]
        public Animation MultiplyG { get; } = new Animation(1, 0, 1);

        [Display(Name = "乗算色B", Description = "モデルカラー乗算のB")]
        [AnimatedHoldSlider("F2", "", 0.0, 1.0)]
        public Animation MultiplyB { get; } = new Animation(1, 0, 1);

        [Display(Name = "乗算色A", Description = "モデルカラー乗算のA")]
        [AnimatedHoldSlider("F2", "", 0.0, 1.0)]
        public Animation MultiplyA { get; } = new Animation(1, 0, 1);

        [Display(Name = "位置X", Description = "モデル表示位置のXオフセット")]
        [AnimatedHoldSlider("F3", "", -2.0, 2.0)]
        public Animation PositionX { get; } = new Animation(0, -2, 2);

        [Display(Name = "位置Y", Description = "モデル表示位置のYオフセット")]
        [AnimatedHoldSlider("F3", "", -2.0, 2.0)]
        public Animation PositionY { get; } = new Animation(0, -2, 2);

        [Display(Name = "拡大率", Description = "モデル表示スケール")]
        [AnimatedHoldSlider("F3", "x", 0.1, 5.0)]
        public Animation Scale { get; } = new Animation(1, 0.1, 5);

        [Display(Name = "回転", Description = "モデル表示の回転角度")]
        [AnimatedHoldSlider("F1", "°", -180.0, 180.0)]
        public Animation Rotation { get; } = new Animation(0, -180, 180);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Opacity, MultiplyR, MultiplyG, MultiplyB, MultiplyA, PositionX, PositionY, Scale, Rotation];
    }
}

