using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Tachie;
using VTuberKitForYMM4.Commons.CustomPropertyEditor;
using VTuberKitForYMM4.Plugin.CustomPropertyEditor;

namespace VTuberKitForYMM4.Plugin
{
    public class Live2DItemParameter : TachieItemParameterBase
    {
        [Display(Name = "待機モーション", Description = "Idleグループのモーションから選択")]
        [CustomComboBox]
        public MotionViewModel Motion { get; set; } = new("Idle", onlyIdle: true);

        [System.ComponentModel.Browsable(false)]
        public string MotionGroup => Motion?.SelectedGroup ?? string.Empty;

        [System.ComponentModel.Browsable(false)]
        public int MotionIndex => Motion?.SelectedIndex ?? -1;

        [Display(Name = "不透明度", Description = "モデル全体の不透明度")]
        [AnimationSlider("F2", "", 0.0, 1.0)]
        public Animation Opacity { get; } = new Animation(1, 0, 1);

        [Display(Name = "乗算色R", Description = "モデルカラー乗算のR")]
        [AnimationSlider("F2", "", 0.0, 1.0)]
        public Animation MultiplyR { get; } = new Animation(1, 0, 1);

        [Display(Name = "乗算色G", Description = "モデルカラー乗算のG")]
        [AnimationSlider("F2", "", 0.0, 1.0)]
        public Animation MultiplyG { get; } = new Animation(1, 0, 1);

        [Display(Name = "乗算色B", Description = "モデルカラー乗算のB")]
        [AnimationSlider("F2", "", 0.0, 1.0)]
        public Animation MultiplyB { get; } = new Animation(1, 0, 1);

        [Display(Name = "乗算色A", Description = "モデルカラー乗算のA")]
        [AnimationSlider("F2", "", 0.0, 1.0)]
        public Animation MultiplyA { get; } = new Animation(1, 0, 1);

        [Display(Name = "位置X", Description = "モデル表示位置のXオフセット")]
        [AnimationSlider("F3", "", -2.0, 2.0)]
        public Animation PositionX { get; } = new Animation(0, -2, 2);

        [Display(Name = "位置Y", Description = "モデル表示位置のYオフセット")]
        [AnimationSlider("F3", "", -2.0, 2.0)]
        public Animation PositionY { get; } = new Animation(0, -2, 2);

        [Display(Name = "拡大率", Description = "モデル表示スケール")]
        [AnimationSlider("F3", "x", 0.1, 5.0)]
        public Animation Scale { get; } = new Animation(1, 0.1, 5);

        [Display(Name = "回転", Description = "モデル表示の回転角度")]
        [AnimationSlider("F1", "°", -180.0, 180.0)]
        public Animation Rotation { get; } = new Animation(0, -180, 180);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Opacity, MultiplyR, MultiplyG, MultiplyB, MultiplyA, PositionX, PositionY, Scale, Rotation];
    }
}

