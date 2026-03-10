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
            else if (activeFace != null || interactionMotionIndex >= 0)
            {
                nativeModel.StopAllMotions();
            }

            var (lipValue, mouthForm) = ComputeLipSync(description.MouthShape, description.VoiceVolume, lipSyncGain);
            var useVowelOnlyLipSync = ShouldBypassNativeLipSync(lipSyncVowelsOnly);
            if (activeFace == null)
            {
                if (!useVowelOnlyLipSync)
                {
                    nativeModel.SetLipSyncValue(lipValue);
                }
                if (!useVowelOnlyLipSync)
                {
                    nativeModel.SetParameterValue(Live2DManager.ParamMouthOpenY, lipValue);
                    nativeModel.SetParameterValue(Live2DManager.ParamMouthForm, mouthForm);
                }
                ApplyVowelLipSyncParameters(nativeModel, description.MouthShape, lipValue);
            }
        }

        public static float ApplyFaceAndLipSync(
            Live2DModelWrapper model,
            TachieSourceDescription description,
            Live2DFaceParameter activeFace,
            double faceLocalFrame,
            double faceDurationFrame,
            float lipSyncGain,
            bool lipSyncVowelsOnly)
        {
            var frame = Math.Max(0L, (long)Math.Round(faceLocalFrame));
            var length = Math.Max(1L, (long)Math.Round(faceDurationFrame));
            var fps = description.FPS;

            var eyeLOpen = activeFace.EyeLOpen.GetValue(frame, length, fps);
            var eyeROpen = activeFace.EyeROpen.GetValue(frame, length, fps);
            var mouthOpen = activeFace.MouthOpen.GetValue(frame, length, fps);
            var mouthForm = activeFace.MouthForm.GetValue(frame, length, fps);
            var angleX = activeFace.AngleX.GetValue(frame, length, fps);
            var angleY = activeFace.AngleY.GetValue(frame, length, fps);
            var angleZ = activeFace.AngleZ.GetValue(frame, length, fps);
            var bodyAngleX = activeFace.BodyAngleX.GetValue(frame, length, fps);
            var eyeBallX = activeFace.EyeBallX.GetValue(frame, length, fps);
            var eyeBallY = activeFace.EyeBallY.GetValue(frame, length, fps);
            var cheek = activeFace.Cheek.GetValue(frame, length, fps);
            var armLA = activeFace.ArmLA.GetValue(frame, length, fps);
            var armRA = activeFace.ArmRA.GetValue(frame, length, fps);

            var (lipValue, mouthFormValue) = ComputeLipSync(description.MouthShape, description.VoiceVolume, lipSyncGain);
            var useVowelOnlyLipSync = ShouldBypassNativeLipSync(lipSyncVowelsOnly);
            if (!useVowelOnlyLipSync)
            {
                model.SetLipSyncValue(lipValue);
            }
            if (!useVowelOnlyLipSync)
            {
                model.SetParameterValue(Live2DManager.ParamMouthOpenY, lipValue);
                model.SetParameterValue(Live2DManager.ParamMouthForm, mouthFormValue);
            }
            ApplyVowelLipSyncParameters(model, description.MouthShape, lipValue);

            ApplyStandardFaceParameters(
                model,
                activeFace,
                (float)eyeLOpen,
                (float)eyeROpen,
                (float)mouthOpen,
                (float)mouthForm,
                (float)angleX,
                (float)angleY,
                (float)angleZ,
                (float)bodyAngleX,
                (float)eyeBallX,
                (float)eyeBallY,
                (float)cheek,
                (float)armLA,
                (float)armRA);

            ApplyDynamicFaceParameters(model, activeFace, frame, length, fps);
            return lipValue;
        }

        public static void ApplyDynamicFaceParts(
            Live2DModelWrapper model,
            Live2DFaceParameter activeFace,
            long frame,
            long length,
            int fps)
        {
            foreach (var row in activeFace.DynamicOverrides.PartRows.ToArray())
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
            foreach (var row in activeFace.DynamicOverrides.ParameterRows.ToArray())
            {
                if (!row.Hold || string.IsNullOrWhiteSpace(row.Id))
                {
                    continue;
                }

                var value = row.GetValue(frame, length, fps);
                model.SetParameterValue(row.Id, value);
            }
        }

        private static void ApplyStandardFaceParameters(
            Live2DModelWrapper model,
            Live2DFaceParameter activeFace,
            float eyeLOpen,
            float eyeROpen,
            float mouthOpenY,
            float mouthForm,
            float angleX,
            float angleY,
            float angleZ,
            float bodyAngleX,
            float eyeBallX,
            float eyeBallY,
            float cheek,
            float armLA,
            float armRA)
        {
            ApplyHeldParameter(model, activeFace.EyeLOpenHold, activeFace.EyeLOpen, Live2DManager.ParamEyeLOpen, eyeLOpen);
            ApplyHeldParameter(model, activeFace.EyeROpenHold, activeFace.EyeROpen, Live2DManager.ParamEyeROpen, eyeROpen);
            ApplyHeldParameter(model, activeFace.MouthOpenHold, activeFace.MouthOpen, Live2DManager.ParamMouthOpenY, mouthOpenY);
            ApplyHeldParameter(model, activeFace.MouthFormHold, activeFace.MouthForm, Live2DManager.ParamMouthForm, mouthForm);
            ApplyHeldParameter(model, activeFace.AngleXHold, activeFace.AngleX, Live2DManager.ParamAngleX, angleX);
            ApplyHeldParameter(model, activeFace.AngleYHold, activeFace.AngleY, Live2DManager.ParamAngleY, angleY);
            ApplyHeldParameter(model, activeFace.AngleZHold, activeFace.AngleZ, Live2DManager.ParamAngleZ, angleZ);
            ApplyHeldParameter(model, activeFace.BodyAngleXHold, activeFace.BodyAngleX, Live2DManager.ParamBodyAngleX, bodyAngleX);
            ApplyHeldParameter(model, activeFace.EyeBallXHold, activeFace.EyeBallX, Live2DManager.ParamEyeBallX, eyeBallX);
            ApplyHeldParameter(model, activeFace.EyeBallYHold, activeFace.EyeBallY, Live2DManager.ParamEyeBallY, eyeBallY);
            ApplyHeldParameter(model, activeFace.CheekHold, activeFace.Cheek, Live2DManager.ParamCheek, cheek);
            ApplyHeldParameter(model, activeFace.ArmLAHold, activeFace.ArmLA, Live2DManager.ParamArmLA, armLA);
            ApplyHeldParameter(model, activeFace.ArmRAHold, activeFace.ArmRA, Live2DManager.ParamArmRA, armRA);
        }

        private static void ApplyHeldParameter(Live2DModelWrapper model, bool hold, Animation animation, string parameterId, float value)
        {
            if (!hold && !HasAnimatedValues(animation))
            {
                return;
            }

            model.SetParameterValue(parameterId, value);
        }

        private static bool HasAnimatedValues(Animation animation)
        {
            return animation.Values.Count > 1;
        }

        private static void ApplyVowelLipSyncParameters(Live2DModelWrapper model, MouthShape shape, float lipValue)
        {
            var vowelParameters = ModelMetadataCatalog.LipSyncVowelParameters;
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

        private static bool ShouldBypassNativeLipSync(bool lipSyncVowelsOnly)
        {
            return lipSyncVowelsOnly && ModelMetadataCatalog.LipSyncVowelParameters.HasAny;
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
