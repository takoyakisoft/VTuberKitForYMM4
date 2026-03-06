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
        [Display(Name = "表情", Description = "model3.json の Expressions から選択")]
        [CustomComboBox]
        public ExpressionViewModel Expression { get; set; } = new("exp");

        [Display(Name = "モーション", Description = "model3.json の Motions から選択（Idle含む）")]
        [CustomComboBox]
        public MotionViewModel Motion { get; set; } = new("Idle");

        [Browsable(false)]
        public string ExpressionId => Expression?.SelectedExpressionId ?? string.Empty;

        [Browsable(false)]
        public string MotionGroup => Motion?.SelectedGroup ?? string.Empty;

        [Browsable(false)]
        public int MotionIndex => Motion?.SelectedIndex ?? -1;

        [Display(Name = "モーション再生", Description = "有効時は指定モーションをクリップ時間で評価します")]
        public bool UseMotion { get => useMotion; set => Set(ref useMotion, value); }
        bool useMotion = false;

        [Display(Name = "モーションループ", Description = "ONで face モーションを繰り返し再生します")]
        [ToggleSlider]
        [DefaultValue(false)]
        public bool MotionLoop { get => motionLoop; set => Set(ref motionLoop, value); }
        bool motionLoop = false;

        [Display(Name = "パラメータ加算", Description = "ONで既存モーションへ加算、OFFで絶対値として適用します")]
        [ToggleSlider]
        [DefaultValue(true)]
        public bool AdditiveParameters { get => additiveParameters; set => Set(ref additiveParameters, value); }
        bool additiveParameters = true;

        [Display(Name = "不透明度", Description = "モデル全体の不透明度")]
        [AnimationSlider("F2", "", -1.0, 1.0)]
        public Animation Opacity { get; } = new Animation(0, -1, 1);

        [Display(Name = "目の開きL", Description = "ParamEyeLOpen")]
        [AnimationSlider("F2", "", -1.0, 1.0)]
        public Animation EyeLOpen { get; } = new Animation(0, -1, 1);

        [Display(Name = "目の開きR", Description = "ParamEyeROpen")]
        [AnimationSlider("F2", "", -1.0, 1.0)]
        public Animation EyeROpen { get; } = new Animation(0, -1, 1);

        [Display(Name = "口の開き", Description = "ParamMouthOpenY")]
        [AnimationSlider("F2", "", -1.0, 1.0)]
        public Animation MouthOpen { get; } = new Animation(0, -1, 1);

        [Display(Name = "口の形", Description = "ParamMouthForm")]
        [AnimationSlider("F2", "", -1.0, 1.0)]
        public Animation MouthForm { get; } = new Animation(0, -1, 1);

        [Display(Name = "顔X", Description = "ParamAngleX")]
        [AnimationSlider("F1", "", -30.0, 30.0)]
        public Animation AngleX { get; } = new Animation(0, -30, 30);

        [Display(Name = "顔Y", Description = "ParamAngleY")]
        [AnimationSlider("F1", "", -30.0, 30.0)]
        public Animation AngleY { get; } = new Animation(0, -30, 30);

        [Display(Name = "顔Z", Description = "ParamAngleZ")]
        [AnimationSlider("F1", "", -30.0, 30.0)]
        public Animation AngleZ { get; } = new Animation(0, -30, 30);

        [Display(Name = "体X", Description = "ParamBodyAngleX")]
        [AnimationSlider("F1", "", -10.0, 10.0)]
        public Animation BodyAngleX { get; } = new Animation(0, -10, 10);

        [Display(Name = "視線X", Description = "ParamEyeBallX")]
        [AnimationSlider("F2", "", -1.0, 1.0)]
        public Animation EyeBallX { get; } = new Animation(0, -1, 1);

        [Display(Name = "視線Y", Description = "ParamEyeBallY")]
        [AnimationSlider("F2", "", -1.0, 1.0)]
        public Animation EyeBallY { get; } = new Animation(0, -1, 1);

        [Display(Name = "頬", Description = "ParamCheek")]
        [AnimationSlider("F2", "", -1.0, 1.0)]
        public Animation Cheek { get; } = new Animation(0, -1, 1);

        [Display(Name = "腕L", Description = "ParamArmLA")]
        [AnimationSlider("F2", "", -1.0, 1.0)]
        public Animation ArmLA { get; } = new Animation(0, -1, 1);

        [Display(Name = "腕R", Description = "ParamArmRA")]
        [AnimationSlider("F2", "", -1.0, 1.0)]
        public Animation ArmRA { get; } = new Animation(0, -1, 1);

        [Display(Name = "表示位置Xオフセット", Description = "Itemの位置Xに加算する一時オフセット")]
        [AnimationSlider("F3", "", -2.0, 2.0)]
        public Animation OffsetPositionX { get; } = new Animation(0, -2, 2);

        [Display(Name = "表示位置Yオフセット", Description = "Itemの位置Yに加算する一時オフセット")]
        [AnimationSlider("F3", "", -2.0, 2.0)]
        public Animation OffsetPositionY { get; } = new Animation(0, -2, 2);

        [Display(Name = "表示拡大率オフセット", Description = "Itemの拡大率に加算する一時オフセット")]
        [AnimationSlider("F3", "x", -2.0, 2.0)]
        public Animation OffsetScale { get; } = new Animation(0, -2, 2);

        [Display(Name = "表示回転オフセット", Description = "Itemの回転に加算する一時オフセット")]
        [AnimationSlider("F1", "°", -180.0, 180.0)]
        public Animation OffsetRotation { get; } = new Animation(0, -180, 180);

        [Display(GroupName = "カスタムParam", Name = "カスタムParam1 ID", Description = "Param IDを選択（未選択で無効）")]
        [CustomComboBox]
        public ParameterIdViewModel CustomParam1 { get; set; } = new("Param");

        [Browsable(false)]
        public string CustomParam1Id
        {
            get => CustomParam1?.SelectedId ?? customParam1Id;
            set
            {
                var normalized = value ?? string.Empty;
                Set(ref customParam1Id, normalized);
                if (CustomParam1 != null)
                {
                    CustomParam1.SelectedId = normalized;
                }
            }
        }
        string customParam1Id = string.Empty;

        [Display(GroupName = "カスタムParam", Name = "カスタムParam1 値", Description = "カスタムParam1 IDに適用する値")]
        [AnimationSlider("F2", "", -100.0, 100.0)]
        public Animation CustomParam1Value { get; } = new Animation(0, -100, 100);

        [Display(GroupName = "カスタムParam", Name = "カスタムParam2 ID", Description = "Param IDを選択（未選択で無効）")]
        [CustomComboBox]
        public ParameterIdViewModel CustomParam2 { get; set; } = new("Param");

        [Browsable(false)]
        public string CustomParam2Id
        {
            get => CustomParam2?.SelectedId ?? customParam2Id;
            set
            {
                var normalized = value ?? string.Empty;
                Set(ref customParam2Id, normalized);
                if (CustomParam2 != null)
                {
                    CustomParam2.SelectedId = normalized;
                }
            }
        }
        string customParam2Id = string.Empty;

        [Display(GroupName = "カスタムParam", Name = "カスタムParam2 値", Description = "カスタムParam2 IDに適用する値")]
        [AnimationSlider("F2", "", -100.0, 100.0)]
        public Animation CustomParam2Value { get; } = new Animation(0, -100, 100);

        [Display(GroupName = "カスタムParam", Name = "カスタムParam3 ID", Description = "Param IDを選択（未選択で無効）")]
        [CustomComboBox]
        public ParameterIdViewModel CustomParam3 { get; set; } = new("Param");

        [Browsable(false)]
        public string CustomParam3Id
        {
            get => CustomParam3?.SelectedId ?? customParam3Id;
            set
            {
                var normalized = value ?? string.Empty;
                Set(ref customParam3Id, normalized);
                if (CustomParam3 != null)
                {
                    CustomParam3.SelectedId = normalized;
                }
            }
        }
        string customParam3Id = string.Empty;

        [Display(GroupName = "カスタムParam", Name = "カスタムParam3 値", Description = "カスタムParam3 IDに適用する値")]
        [AnimationSlider("F2", "", -100.0, 100.0)]
        public Animation CustomParam3Value { get; } = new Animation(0, -100, 100);

        [Display(GroupName = "カスタムPart", Name = "カスタムPart1 ID", Description = "Part IDを選択（未選択で無効）")]
        [CustomComboBox]
        public PartIdViewModel CustomPart1 { get; set; } = new("Part");

        [Browsable(false)]
        public string CustomPart1Id
        {
            get => CustomPart1?.SelectedId ?? customPart1Id;
            set
            {
                var normalized = value ?? string.Empty;
                Set(ref customPart1Id, normalized);
                if (CustomPart1 != null)
                {
                    CustomPart1.SelectedId = normalized;
                }
            }
        }
        string customPart1Id = string.Empty;

        [Display(GroupName = "カスタムPart", Name = "カスタムPart1 不透明度", Description = "カスタムPart1 IDに適用する不透明度")]
        [AnimationSlider("F2", "", -1.0, 1.0)]
        public Animation CustomPart1Opacity { get; } = new Animation(0, -1, 1);

        [Display(GroupName = "カスタムPart", Name = "カスタムPart2 ID", Description = "Part IDを選択（未選択で無効）")]
        [CustomComboBox]
        public PartIdViewModel CustomPart2 { get; set; } = new("Part");

        [Browsable(false)]
        public string CustomPart2Id
        {
            get => CustomPart2?.SelectedId ?? customPart2Id;
            set
            {
                var normalized = value ?? string.Empty;
                Set(ref customPart2Id, normalized);
                if (CustomPart2 != null)
                {
                    CustomPart2.SelectedId = normalized;
                }
            }
        }
        string customPart2Id = string.Empty;

        [Display(GroupName = "カスタムPart", Name = "カスタムPart2 不透明度", Description = "カスタムPart2 IDに適用する不透明度")]
        [AnimationSlider("F2", "", -1.0, 1.0)]
        public Animation CustomPart2Opacity { get; } = new Animation(0, -1, 1);

        [Display(GroupName = "カスタムPart", Name = "カスタムPart3 ID", Description = "Part IDを選択（未選択で無効）")]
        [CustomComboBox]
        public PartIdViewModel CustomPart3 { get; set; } = new("Part");

        [Browsable(false)]
        public string CustomPart3Id
        {
            get => CustomPart3?.SelectedId ?? customPart3Id;
            set
            {
                var normalized = value ?? string.Empty;
                Set(ref customPart3Id, normalized);
                if (CustomPart3 != null)
                {
                    CustomPart3.SelectedId = normalized;
                }
            }
        }
        string customPart3Id = string.Empty;

        [Display(GroupName = "カスタムPart", Name = "カスタムPart3 不透明度", Description = "カスタムPart3 IDに適用する不透明度")]
        [AnimationSlider("F2", "", -1.0, 1.0)]
        public Animation CustomPart3Opacity { get; } = new Animation(0, -1, 1);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Opacity, EyeLOpen, EyeROpen, MouthOpen, MouthForm, AngleX, AngleY, AngleZ, BodyAngleX, EyeBallX, EyeBallY, Cheek, ArmLA, ArmRA, OffsetPositionX, OffsetPositionY, OffsetScale, OffsetRotation, CustomPart1Opacity, CustomPart2Opacity, CustomPart3Opacity, CustomParam1Value, CustomParam2Value, CustomParam3Value];
    }
}

