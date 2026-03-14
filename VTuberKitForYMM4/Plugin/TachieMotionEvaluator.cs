using VTuberKitForNative;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Voice;

namespace VTuberKitForYMM4.Plugin
{
    public static class TachieMotionEvaluator
    {
        public static void UpdateMotionToCurrentTime(
            Live2DModelWrapper nativeModel,
            TachieSourceDescription description,
            string? modelPath,
            Live2DFaceParameter? activeFace,
            Live2DItemParameter? itemParam,
            float activeFaceTimeSeconds,
            float lipSyncGain,
            bool lipSyncVowelsOnly,
            string? interactionMotionGroup = null,
            int interactionMotionIndex = -1,
            float interactionMotionTimeSeconds = 0.0f,
            float? itemTimeSecondsOverride = null)
        {
            if (nativeModel == null || description == null)
            {
                return;
            }

            if (TryResolveInteractionMotionSelection(nativeModel, interactionMotionGroup, interactionMotionIndex, out var interactionGroup, out var interactionIndex))
            {
                nativeModel.EvaluateMotion(interactionGroup, interactionIndex, Math.Max(0, interactionMotionTimeSeconds), false);
            }
            else if (activeFace != null &&
                TryResolveMotionSelection(nativeModel, activeFace, out var motionGroup, out var motionIndex))
            {
                nativeModel.EvaluateMotion(motionGroup, motionIndex, Math.Max(0, activeFaceTimeSeconds), activeFace.MotionLoop);
            }
            else if (TryResolveItemMotionSelection(nativeModel, itemParam, out var itemGroup, out var itemIndex))
            {
                var itemTimeSeconds = itemTimeSecondsOverride ?? (float)Math.Max(0.0, description.ItemPosition.Time.TotalSeconds);
                nativeModel.EvaluateMotion(itemGroup, itemIndex, Math.Max(0, itemTimeSeconds), itemParam?.MotionLoop ?? true);
            }
            else if (activeFace != null)
            {
                nativeModel.StopAllMotions();
            }

            var (lipValue, mouthForm) = ComputeLipSync(description.MouthShape, description.VoiceVolume, lipSyncGain);
            var useVowelOnlyLipSync = ShouldBypassNativeLipSync(modelPath, lipSyncVowelsOnly);
            if (activeFace == null)
            {
                if (!useVowelOnlyLipSync)
                {
                    nativeModel.SetLipSyncValue(lipValue);
                }
            }
        }

        public static float ApplyFaceAndLipSync(
            Live2DModelWrapper model,
            TachieSourceDescription description,
            string? modelPath,
            Live2DFaceParameter activeFace,
            double faceLocalFrame,
            double faceDurationFrame,
            float lipSyncGain,
            bool lipSyncVowelsOnly)
        {
            var frame = Math.Max(0L, (long)Math.Round(faceLocalFrame));
            var length = Math.Max(1L, (long)Math.Round(faceDurationFrame));

            var (lipValue, mouthFormValue) = ComputeLipSync(description.MouthShape, description.VoiceVolume, lipSyncGain);
            var useVowelOnlyLipSync = ShouldBypassNativeLipSync(modelPath, lipSyncVowelsOnly);
            if (!useVowelOnlyLipSync)
            {
                model.SetLipSyncValue(lipValue);
            }
            if (!useVowelOnlyLipSync)
            {
                model.SetParameterValue(Live2DManager.ParamMouthOpenY, lipValue);
                model.SetParameterValue(Live2DManager.ParamMouthForm, mouthFormValue);
            }
            if (useVowelOnlyLipSync)
            {
                ApplyVowelLipSyncParameters(model, modelPath, description.MouthShape, lipValue);
            }

            ApplyDynamicFaceParameters(model, activeFace, frame, length, description.FPS);
            return lipValue;
        }

        public static void ApplyItemLipSyncPostPhysics(
            Live2DModelWrapper model,
            TachieSourceDescription description,
            string? modelPath,
            float lipSyncGain,
            bool lipSyncVowelsOnly)
        {
            var (lipValue, mouthFormValue) = ComputeLipSync(description.MouthShape, description.VoiceVolume, lipSyncGain);
            var useVowelOnlyLipSync = ShouldBypassNativeLipSync(modelPath, lipSyncVowelsOnly);
            if (!useVowelOnlyLipSync)
            {
                model.SetParameterValue(Live2DManager.ParamMouthOpenY, lipValue);
                model.SetParameterValue(Live2DManager.ParamMouthForm, mouthFormValue);
                return;
            }

            ApplyVowelLipSyncParameters(model, modelPath, description.MouthShape, lipValue);
        }

        public static void ApplyDynamicFaceParts(
            Live2DModelWrapper model,
            Live2DFaceParameter activeFace,
            long frame,
            long length,
            int fps)
        {
            foreach (var row in activeFace.DynamicOverrides.GetPartRowsSnapshot())
            {
                if (!row.Hold || string.IsNullOrWhiteSpace(row.Id))
                {
                    continue;
                }

                var value = (float)row.Opacity.GetValue(frame, length, fps);
                model.SetPartOpacity(row.Id, Math.Clamp(value, 0.0f, 1.0f));
            }
        }

        public static void ApplyDynamicItemParameters(
            Live2DModelWrapper model,
            Live2DItemParameter itemParam,
            long frame,
            long length,
            int fps)
        {
            foreach (var row in itemParam.DynamicOverrides.GetParameterRowsSnapshot())
            {
                if (!row.Hold || string.IsNullOrWhiteSpace(row.Id))
                {
                    continue;
                }

                var value = row.GetValue(frame, length, fps);
                model.SetParameterValue(row.Id, value);
            }
        }

        public static void ApplyDynamicItemParts(
            Live2DModelWrapper model,
            Live2DItemParameter itemParam,
            long frame,
            long length,
            int fps)
        {
            foreach (var row in itemParam.DynamicOverrides.GetPartRowsSnapshot())
            {
                if (!row.Hold || string.IsNullOrWhiteSpace(row.Id))
                {
                    continue;
                }

                var value = (float)row.Opacity.GetValue(frame, length, fps);
                model.SetPartOpacity(row.Id, Math.Clamp(value, 0.0f, 1.0f));
            }
        }

        private static void ApplyDynamicFaceParameters(
            Live2DModelWrapper model,
            Live2DFaceParameter activeFace,
            long frame,
            long length,
            int fps)
        {
            foreach (var row in activeFace.DynamicOverrides.GetParameterRowsSnapshot())
            {
                if (!row.Hold || string.IsNullOrWhiteSpace(row.Id))
                {
                    continue;
                }

                var value = row.GetValue(frame, length, fps);
                model.SetParameterValue(row.Id, value);
            }
        }

        private static void ApplyVowelLipSyncParameters(Live2DModelWrapper model, string? modelPath, MouthShape shape, float lipValue)
        {
            var vowelParameters = ModelMetadataCatalog.GetLipSyncVowelParameters(modelPath);
            if (!vowelParameters.HasAny)
            {
                return;
            }

            SetVowelParameter(model, vowelParameters.A, shape == MouthShape.A ? lipValue : 0.0f);
            SetVowelParameter(model, vowelParameters.I, shape == MouthShape.I ? lipValue : 0.0f);
            SetVowelParameter(model, vowelParameters.U, shape == MouthShape.U ? lipValue : 0.0f);
            SetVowelParameter(model, vowelParameters.E, shape == MouthShape.E ? lipValue : 0.0f);
            SetVowelParameter(model, vowelParameters.O, shape == MouthShape.O ? lipValue : 0.0f);
        }

        private static void SetVowelParameter(Live2DModelWrapper model, string parameterId, float value)
        {
            if (string.IsNullOrWhiteSpace(parameterId))
            {
                return;
            }

            model.SetParameterValue(parameterId, value);
        }

        private static bool ShouldBypassNativeLipSync(string? modelPath, bool lipSyncVowelsOnly)
        {
            return lipSyncVowelsOnly && ModelMetadataCatalog.GetLipSyncVowelParameters(modelPath).HasAny;
        }

        private static bool TryResolveMotionSelection(Live2DModelWrapper nativeModel, Live2DFaceParameter activeFace, out string group, out int index)
        {
            var motionGroup = activeFace.MotionGroup ?? string.Empty;
            var motionIndex = Math.Max(0, activeFace.MotionIndex);
            group = string.Empty;
            index = -1;

            if (activeFace.MotionIndex < 0)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(motionGroup))
            {
                group = motionGroup;
                index = motionIndex;
                return true;
            }

            var catalog = ModelMetadataCatalog.Motions;
            if (motionIndex >= 0 && motionIndex < catalog.Count)
            {
                var m = catalog[motionIndex];
                group = m.Group ?? string.Empty;
                index = Math.Max(0, m.Index);
                return true;
            }

            var motions = nativeModel.GetMotions();
            if (motionIndex >= 0 && motionIndex < motions.Length)
            {
                group = motions[motionIndex].Group ?? string.Empty;
                index = Math.Max(0, motions[motionIndex].Index);
                return true;
            }

            return false;
        }

        internal static bool TryResolveInteractionMotionSelection(
            Live2DModelWrapper nativeModel,
            string? selectedGroup,
            int selectedIndex,
            out string group,
            out int index)
        {
            group = string.Empty;
            index = -1;
            if (selectedIndex < 0)
            {
                return false;
            }

            var motionGroup = selectedGroup ?? string.Empty;
            var motionIndex = Math.Max(0, selectedIndex);
            if (!string.IsNullOrWhiteSpace(motionGroup))
            {
                group = motionGroup;
                index = motionIndex;
                return true;
            }

            var motions = nativeModel.GetMotions();
            if (motionIndex >= 0 && motionIndex < motions.Length)
            {
                var motion = motions[motionIndex];
                group = motion.Group ?? string.Empty;
                index = Math.Max(0, motion.Index);
                return true;
            }

            var catalog = ModelMetadataCatalog.Motions;
            if (motionIndex >= 0 && motionIndex < catalog.Count)
            {
                var motion = catalog[motionIndex];
                group = motion.Group ?? string.Empty;
                index = Math.Max(0, motion.Index);
                return true;
            }

            return false;
        }

        private static bool TryResolveItemMotionSelection(Live2DModelWrapper nativeModel, Live2DItemParameter? itemParam, out string group, out int index)
        {
            group = string.Empty;
            index = -1;
            if (itemParam == null || itemParam.MotionIndex < 0)
            {
                return false;
            }

            var motionGroup = itemParam.MotionGroup ?? string.Empty;
            var motionIndex = Math.Max(0, itemParam.MotionIndex);
            if (!string.IsNullOrWhiteSpace(motionGroup))
            {
                group = motionGroup;
                index = motionIndex;
                return true;
            }

            var motions = nativeModel.GetMotions();
            if (motionIndex >= 0 && motionIndex < motions.Length)
            {
                var motion = motions[motionIndex];
                group = motion.Group ?? string.Empty;
                index = Math.Max(0, motion.Index);
                return true;
            }

            var catalog = ModelMetadataCatalog.Motions;
            if (motionIndex >= 0 && motionIndex < catalog.Count)
            {
                var motion = catalog[motionIndex];
                group = motion.Group ?? string.Empty;
                index = Math.Max(0, motion.Index);
                return true;
            }

            return false;
        }

        private static (float LipValue, float MouthForm) ComputeLipSync(MouthShape shape, double volume, float lipSyncGain)
        {
            var shapeOpenScale = shape switch
            {
                MouthShape.A => 1.00f,
                MouthShape.I => 0.75f,
                MouthShape.U => 0.80f,
                MouthShape.E => 0.90f,
                MouthShape.O => 0.95f,
                _ => 0.0f
            };

            var normalizedVolume = volume <= 0
                ? 0.0f
                : (float)(volume <= 1.0 ? volume : Math.Min(volume / 100.0, 1.0));
            // 0..1入力の小さな値でも口が見えるように非線形で持ち上げる
            var loudness = MathF.Pow(Math.Clamp(normalizedVolume, 0.0f, 1.0f), 0.70f);
            var openY = loudness * shapeOpenScale;

            var mouthForm = shape switch
            {
                MouthShape.A => 0.0f,
                MouthShape.I => 1.0f,
                MouthShape.U => 0.6f,
                MouthShape.E => 0.8f,
                MouthShape.O => -0.4f,
                _ => 0.0f
            };
            var value = Math.Clamp(openY * Math.Max(0.0f, lipSyncGain * 2.2f), 0.0f, 1.0f);
            return (value, mouthForm);
        }
    }
}
