using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using VTuberKitForYMM4.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Tachie;

namespace VTuberKitForYMM4.Plugin
{
    public enum Live2DMsaaSamplePreset
    {
        [Display(Name = nameof(Translate.Character_Msaa2x_Name), Description = nameof(Translate.Character_Msaa2x_Desc), ResourceType = typeof(Translate))]
        X2 = 2,

        [Display(Name = nameof(Translate.Character_Msaa4x_Name), Description = nameof(Translate.Character_Msaa4x_Desc), ResourceType = typeof(Translate))]
        X4 = 4,
    }

    public class Live2DCharacterParameter : TachieCharacterParameterBase
    {
        private string _lastRegisteredInteractionLinkId = string.Empty;

        public Live2DCharacterParameter()
        {
            RegisterInteractionTarget();
        }

        [Display(GroupName = nameof(Translate.Group_Model), Name = nameof(Translate.Character_File_Name), Description = nameof(Translate.Character_File_Desc), ResourceType = typeof(Translate))]
        [DirectorySelector]
        public string? File
        {
            get => file;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value) ? null : value;
                var resolution = ModelMetadataCatalog.ResolveModelSelection(normalized);
                if (!string.IsNullOrWhiteSpace(normalized) && !resolution.IsValid)
                {
                    ConsoleManager.Error(Translate.Error_InvalidModelSelection);
                    return;
                }

                var resolvedValue = resolution.ResolvedModelPath;
                if (string.IsNullOrEmpty(normalized) || !string.IsNullOrEmpty(resolvedValue))
                {
                    var previousAutoDisplayName = ResolveDefaultDisplayName(file, _lastRegisteredInteractionLinkId);
                    var shouldRefreshAutoDisplayName =
                        string.IsNullOrWhiteSpace(interactionDisplayName) ||
                        string.Equals(interactionDisplayName, previousAutoDisplayName, System.StringComparison.OrdinalIgnoreCase);

                    if (Set(ref file, resolvedValue))
                    {
                        if (shouldRefreshAutoDisplayName)
                        {
                            interactionDisplayName = string.Empty;
                        }

                        ModelMetadataCatalog.UpdateFromModelPath(resolvedValue);
                        ShowModelSelectionWarnings(resolvedValue);
                        RegisterInteractionTarget();
                    }
                }
            }
        }
        string? file = null;

        [Display(GroupName = nameof(Translate.Group_Link), Name = nameof(Translate.Character_LinkId_Name), Description = nameof(Translate.Character_LinkId_Desc), ResourceType = typeof(Translate))]
        public string InteractionLinkId
        {
            get
            {
                var resolved = string.IsNullOrWhiteSpace(interactionLinkId) || IsLegacyDefaultLinkId(interactionLinkId)
                    ? AutoInteractionLinkId
                    : interactionLinkId;
                RegisterInteractionTarget(resolved);
                return resolved;
            }
            set
            {
                var normalized = IsLegacyDefaultLinkId(value) ? string.Empty : value ?? string.Empty;
                if (Set(ref interactionLinkId, normalized))
                {
                    RegisterInteractionTarget();
                }
            }
        }
        string interactionLinkId = string.Empty;

        [Browsable(false)]
        public string AutoInteractionLinkId
        {
            get => string.IsNullOrWhiteSpace(autoInteractionLinkId) ? autoInteractionLinkId = Guid.NewGuid().ToString("N") : autoInteractionLinkId;
            set
            {
                if (Set(ref autoInteractionLinkId, value ?? string.Empty))
                {
                    RegisterInteractionTarget();
                }
            }
        }
        string autoInteractionLinkId = Guid.NewGuid().ToString("N");

        [Display(GroupName = nameof(Translate.Group_Link), Name = nameof(Translate.Character_DisplayName_Name), Description = nameof(Translate.Character_DisplayName_Desc), ResourceType = typeof(Translate))]
        public string InteractionDisplayName
        {
            get => ResolveInteractionDisplayName();
            set
            {
                if (Set(ref interactionDisplayName, value ?? string.Empty))
                {
                    RegisterInteractionTarget();
                }
            }
        }
        string interactionDisplayName = string.Empty;

        private void RegisterInteractionTarget(string? resolvedLinkId = null)
        {
            var linkId = string.IsNullOrWhiteSpace(resolvedLinkId)
                ? (string.IsNullOrWhiteSpace(interactionLinkId) || IsLegacyDefaultLinkId(interactionLinkId) ? AutoInteractionLinkId : interactionLinkId)
                : resolvedLinkId;
            var displayName = ResolveInteractionDisplayName(linkId);

            if (!string.IsNullOrWhiteSpace(_lastRegisteredInteractionLinkId) &&
                !string.Equals(_lastRegisteredInteractionLinkId, linkId, System.StringComparison.Ordinal))
            {
                Live2DInteractionStore.RemoveInteractionTarget(_lastRegisteredInteractionLinkId);
            }

            Live2DInteractionStore.UpdateInteractionTarget(linkId, displayName, File);
            _lastRegisteredInteractionLinkId = linkId;
        }

        private string ResolveDefaultDisplayName(string? modelFilePath = null, string? resolvedLinkId = null)
        {
            var sourceFile = string.IsNullOrWhiteSpace(modelFilePath) ? File : modelFilePath;
            if (string.IsNullOrWhiteSpace(sourceFile))
            {
                return resolvedLinkId ?? InteractionLinkId;
            }

            var fileName = Path.GetFileName(sourceFile);
            if (fileName.EndsWith(".model3.json", System.StringComparison.OrdinalIgnoreCase))
            {
                return fileName[..^".model3.json".Length];
            }

            return Path.GetFileNameWithoutExtension(fileName);
        }

        private string ResolveInteractionDisplayName(string? resolvedLinkId = null)
        {
            var defaultDisplayName = ResolveDefaultDisplayName(File, resolvedLinkId);
            if (string.IsNullOrWhiteSpace(interactionDisplayName))
            {
                return defaultDisplayName;
            }

            if (IsLegacyAutoDisplayName(interactionDisplayName))
            {
                return defaultDisplayName;
            }

            return interactionDisplayName;
        }

        private static bool IsLegacyDefaultLinkId(string? value)
        {
            return string.Equals(value, Live2DInteractionDefaults.DefaultLinkId, System.StringComparison.OrdinalIgnoreCase);
        }

        private static void ShowModelSelectionWarnings(string? modelPath)
        {
            var warnings = ModelMetadataCatalog.GetModelSelectionWarnings(modelPath);
            if (warnings.Count == 0)
            {
                return;
            }

            foreach (var warning in warnings)
            {
                ModelMetadataCatalog.RememberShownSelectionIssue(modelPath, warning);
            }

            ConsoleManager.Error(
                $"{Translate.Error_ModelSelectionWarnings_Title}\n\n{Translate.Error_TargetPath_Label}: {modelPath}\n\n- {string.Join("\n- ", warnings)}");
        }

        private bool IsLegacyAutoDisplayName(string value)
        {
            if (string.IsNullOrWhiteSpace(File))
            {
                return false;
            }

            var fileName = Path.GetFileName(File);
            var legacyName = Path.GetFileNameWithoutExtension(fileName);
            return string.Equals(value, legacyName, System.StringComparison.OrdinalIgnoreCase);
        }

        [Display(GroupName = nameof(Translate.Group_Blink), Name = nameof(Translate.Character_AutoEyeBlink_Name), Description = nameof(Translate.Character_AutoEyeBlink_Desc), ResourceType = typeof(Translate))]
        [ToggleSlider]
        public bool AutoEyeBlink { get => autoEyeBlink; set => Set(ref autoEyeBlink, value); }
        bool autoEyeBlink = true;

        [Display(GroupName = nameof(Translate.Group_Blink), Name = nameof(Translate.Character_EyeBlinkInterval_Name), Description = nameof(Translate.Character_EyeBlinkInterval_Desc), ResourceType = typeof(Translate))]
        [TextBoxSlider("F2", "s", 0.1, 10.0, Delay = -1)]
        [Range(0.1, 10.0)]
        [DefaultValue(3.0)]
        public double EyeBlinkInterval { get => eyeBlinkInterval; set => Set(ref eyeBlinkInterval, value); }
        double eyeBlinkInterval = 3.0;

        [Display(GroupName = nameof(Translate.Group_LipSync), Name = nameof(Translate.Character_AutoLipSync_Name), Description = nameof(Translate.Character_AutoLipSync_Desc), ResourceType = typeof(Translate))]
        [ToggleSlider]
        [Browsable(false)]
        public bool AutoLipSync { get => autoLipSync; set => Set(ref autoLipSync, value); }
        bool autoLipSync = true;

        [Display(GroupName = nameof(Translate.Group_LipSync), Name = nameof(Translate.Character_LipSyncGain_Name), Description = nameof(Translate.Character_LipSyncGain_Desc), ResourceType = typeof(Translate))]
        [TextBoxSlider("F2", "", 0.0, 5.0, Delay = -1)]
        [Range(0.0, 5.0)]
        [DefaultValue(1.0)]
        public double LipSyncGain { get => lipSyncGain; set => Set(ref lipSyncGain, value); }
        double lipSyncGain = 1.0;

        [Display(GroupName = nameof(Translate.Group_LipSync), Name = nameof(Translate.Character_LipSyncVowelsOnly_Name), Description = nameof(Translate.Character_LipSyncVowelsOnly_Desc), ResourceType = typeof(Translate))]
        [ToggleSlider]
        public bool LipSyncVowelsOnly { get => lipSyncVowelsOnly; set => Set(ref lipSyncVowelsOnly, value); }
        bool lipSyncVowelsOnly = true;

        [Display(GroupName = nameof(Translate.Group_Physics), Name = nameof(Translate.Character_EnablePhysics_Name), Description = nameof(Translate.Character_EnablePhysics_Desc), ResourceType = typeof(Translate))]
        [ToggleSlider]
        public bool EnablePhysics { get => enablePhysics; set => Set(ref enablePhysics, value); }
        bool enablePhysics = true;

        [Display(GroupName = nameof(Translate.Group_Physics), Name = nameof(Translate.Character_PhysicsStrength_Name), Description = nameof(Translate.Character_PhysicsStrength_Desc), ResourceType = typeof(Translate))]
        [TextBoxSlider("F0", "", 0.0, 100.0, Delay = -1)]
        [Range(0.0, 100.0)]
        [DefaultValue(50.0)]
        public double PhysicsStrength { get => physicsStrength; set => Set(ref physicsStrength, value); }
        double physicsStrength = 50.0;

        [Display(GroupName = nameof(Translate.Group_Physics), Name = nameof(Translate.Character_WindStrength_Name), Description = nameof(Translate.Character_WindStrength_Desc), ResourceType = typeof(Translate))]
        [TextBoxSlider("F0", "", 0.0, 100.0, Delay = -1)]
        [Range(0.0, 100.0)]
        [DefaultValue(0.0)]
        public double WindStrength { get => windStrength; set => Set(ref windStrength, value); }
        double windStrength = 0.0;

        [Display(GroupName = nameof(Translate.Group_Physics), Name = nameof(Translate.Character_HitAreaPhysicsStrength_Name), Description = nameof(Translate.Character_HitAreaPhysicsStrength_Desc), ResourceType = typeof(Translate))]
        [TextBoxSlider("F0", "", 0.0, 100.0, Delay = -1)]
        [Range(0.0, 100.0)]
        [DefaultValue(0.0)]
        public double HitAreaPhysicsStrength { get => hitAreaPhysicsStrength; set => Set(ref hitAreaPhysicsStrength, value); }
        double hitAreaPhysicsStrength = 0.0;

        [Display(GroupName = nameof(Translate.Group_Breath), Name = nameof(Translate.Character_EnableBreath_Name), Description = nameof(Translate.Character_EnableBreath_Desc), ResourceType = typeof(Translate))]
        [ToggleSlider]
        public bool EnableBreath { get => enableBreath; set => Set(ref enableBreath, value); }
        bool enableBreath = true;

        [Display(GroupName = nameof(Translate.Group_Quality), Name = nameof(Translate.Character_RenderTargetMaxSize_Name), Description = nameof(Translate.Character_RenderTargetMaxSize_Desc), ResourceType = typeof(Translate))]
        [TextBoxSlider("F0", "px", 2048, 8192, Delay = -1)]
        [Range(2048, 8192)]
        [DefaultValue(4096)]
        public int RenderTargetMaxSize { get => renderTargetMaxSize; set => Set(ref renderTargetMaxSize, value); }
        int renderTargetMaxSize = 4096;

        [Display(GroupName = nameof(Translate.Group_Quality), Name = nameof(Translate.Character_InternalRenderScale_Name), Description = nameof(Translate.Character_InternalRenderScale_Desc), ResourceType = typeof(Translate))]
        [TextBoxSlider("F2", "x", 1.0, 4.0, Delay = -1)]
        [Range(1.0, 4.0)]
        [DefaultValue(2.0)]
        public double InternalRenderScale { get => internalRenderScale; set => Set(ref internalRenderScale, value); }
        double internalRenderScale = 2.0;

        [Display(GroupName = nameof(Translate.Group_Quality), Name = nameof(Translate.Character_EnableMsaa_Name), Description = nameof(Translate.Character_EnableMsaa_Desc), ResourceType = typeof(Translate))]
        [ToggleSlider]
        [DefaultValue(true)]
        public bool EnableMsaa { get => enableMsaa; set => Set(ref enableMsaa, value); }
        bool enableMsaa = true;

        [Display(GroupName = nameof(Translate.Group_Quality), Name = nameof(Translate.Character_MsaaSamplePreset_Name), Description = nameof(Translate.Character_MsaaSamplePreset_Desc), ResourceType = typeof(Translate))]
        [EnumComboBox]
        [DefaultValue(Live2DMsaaSamplePreset.X4)]
        public Live2DMsaaSamplePreset MsaaSamplePreset { get => msaaSamplePreset; set => Set(ref msaaSamplePreset, value); }
        Live2DMsaaSamplePreset msaaSamplePreset = Live2DMsaaSamplePreset.X4;
    }
}

