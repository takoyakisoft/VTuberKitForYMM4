using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Tachie;

namespace VTuberKitForYMM4.Plugin
{
    public enum Live2DMsaaSamplePreset
    {
        [Display(Name = "2x", Description = "負荷を抑えつつ輪郭を改善")]
        X2 = 2,

        [Display(Name = "4x", Description = "より高品質。負荷が増加")]
        X4 = 4,
    }

    public class Live2DCharacterParameter : TachieCharacterParameterBase
    {
        [Display(GroupName = "モデル", Name = "モデルファイル", Description = "Live2Dモデルのmodel3.jsonファイルを選択してください")]
        [FileSelector(YukkuriMovieMaker.Settings.FileGroupType.None)]
        public string? File
        {
            get => file;
            set
            {
                if (Set(ref file, value))
                {
                    ModelMetadataCatalog.UpdateFromModelPath(value);
                }
            }
        }
        string? file = null;

        [Display(GroupName = "目パチ", Name = "自動まばたき", Description = "自動でまばたきを行います")]
        [ToggleSlider]
        public bool AutoEyeBlink { get => autoEyeBlink; set => Set(ref autoEyeBlink, value); }
        bool autoEyeBlink = true;

        [Display(GroupName = "目パチ", Name = "目パチ間隔", Description = "まばたきの間隔を秒単位で設定します")]
        [TextBoxSlider("F2", "s", 0.1, 10.0, Delay = -1)]
        [Range(0.1, 10.0)]
        [DefaultValue(3.0)]
        public double EyeBlinkInterval { get => eyeBlinkInterval; set => Set(ref eyeBlinkInterval, value); }
        double eyeBlinkInterval = 3.0;

        [Display(GroupName = "口パク", Name = "自動口パク", Description = "音声に連動して口を動かします")]
        [ToggleSlider]
        [Browsable(false)]
        public bool AutoLipSync { get => autoLipSync; set => Set(ref autoLipSync, value); }
        bool autoLipSync = true;

        [Display(GroupName = "口パク", Name = "口パク感度", Description = "口パクの感度を調整します")]
        [TextBoxSlider("F2", "", 0.0, 5.0, Delay = -1)]
        [Range(0.0, 5.0)]
        [DefaultValue(1.0)]
        public double LipSyncGain { get => lipSyncGain; set => Set(ref lipSyncGain, value); }
        double lipSyncGain = 1.0;

        [Display(GroupName = "物理演算", Name = "物理演算有効", Description = "物理演算による風揺れを有効にします")]
        [ToggleSlider]
        public bool EnablePhysics { get => enablePhysics; set => Set(ref enablePhysics, value); }
        bool enablePhysics = true;

        [Display(GroupName = "物理演算", Name = "風の強さ", Description = "0で無効、値を上げると揺れ入力を強めます")]
        [TextBoxSlider("F2", "", 0.0, 3.0, Delay = -1)]
        [Range(0.0, 3.0)]
        [DefaultValue(0.0)]
        public double WindStrength { get => windStrength; set => Set(ref windStrength, value); }
        double windStrength = 0.0;

        [Display(GroupName = "呼吸", Name = "呼吸有効", Description = "呼吸モーションを有効にします")]
        [ToggleSlider]
        public bool EnableBreath { get => enableBreath; set => Set(ref enableBreath, value); }
        bool enableBreath = true;

        [Display(GroupName = "描画品質", Name = "RT最大サイズ", Description = "内部描画の最大ピクセルサイズ。大きいほど高品質だがVRAM消費増（既定: 8192）", Order = 2)]
        [TextBoxSlider("F0", "px", 2048, 8192, Delay = -1)]
        [Range(2048, 8192)]
        [DefaultValue(8192)]
        public int RenderTargetMaxSize { get => renderTargetMaxSize; set => Set(ref renderTargetMaxSize, value); }
        int renderTargetMaxSize = 8192;

        [Display(GroupName = "描画品質", Name = "内部倍率", Description = "内部描画倍率。大きいほど拡大に強い（既定: 2.0）", Order = 1)]
        [TextBoxSlider("F2", "x", 1.0, 4.0, Delay = -1)]
        [Range(1.0, 4.0)]
        [DefaultValue(2.0)]
        public double InternalRenderScale { get => internalRenderScale; set => Set(ref internalRenderScale, value); }
        double internalRenderScale = 2.0;

        [Display(GroupName = "描画品質", Name = "FXAA", Description = "ONで最終縮小時の補間を高品質化（滑らか寄り、ややソフトになる場合あり）", Order = 5)]
        [ToggleSlider]
        [DefaultValue(true)]
        public bool EnableFxaa { get => enableFxaa; set => Set(ref enableFxaa, value); }
        bool enableFxaa = true;

        [Display(GroupName = "描画品質", Name = "MSAA", Description = "ONでMSAAを有効化（エッジ改善、負荷増）。FXAAと併用可", Order = 3)]
        [ToggleSlider]
        [DefaultValue(true)]
        public bool EnableMsaa { get => enableMsaa; set => Set(ref enableMsaa, value); }
        bool enableMsaa = true;

        [Display(GroupName = "描画品質", Name = "MSAAサンプル", Description = "MSAA ON時のサンプル数（2x / 4x）", Order = 4)]
        [EnumComboBox]
        [DefaultValue(Live2DMsaaSamplePreset.X4)]
        public Live2DMsaaSamplePreset MsaaSamplePreset { get => msaaSamplePreset; set => Set(ref msaaSamplePreset, value); }
        Live2DMsaaSamplePreset msaaSamplePreset = Live2DMsaaSamplePreset.X4;
    }
}

