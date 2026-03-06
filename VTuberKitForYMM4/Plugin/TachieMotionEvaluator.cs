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
            bool autoLipSync,
            float? itemTimeSecondsOverride = null)
        {
            if (nativeModel == null || description == null)
            {
                return;
            }

            if (activeFace?.UseMotion == true &&
                TryResolveMotionSelection(nativeModel, activeFace, out var motionGroup, out var motionIndex))
            {
                nativeModel.EvaluateMotion(motionGroup, motionIndex, Math.Max(0, activeFaceTimeSeconds), activeFace.MotionLoop);
            }
            else if (TryResolveItemMotionSelection(nativeModel, itemParam, out var itemGroup, out var itemIndex))
            {
                var itemTimeSeconds = itemTimeSecondsOverride ?? (float)Math.Max(0.0, description.ItemPosition.Time.TotalSeconds);
                nativeModel.EvaluateMotion(itemGroup, itemIndex, Math.Max(0, itemTimeSeconds), itemParam?.MotionLoop ?? true);
            }
            else if (activeFace?.UseMotion == true)
            {
                nativeModel.StopAllMotions();
            }

            var (lipValue, mouthForm) = ComputeLipSync(description.MouthShape, description.VoiceVolume, lipSyncGain);
            if (activeFace == null)
            {
                nativeModel.SetLipSyncValue(lipValue);
                nativeModel.AddParameterValue(Live2DManager.ParamMouthOpenY, lipValue);
                nativeModel.AddParameterValue(Live2DManager.ParamMouthForm, mouthForm);
            }
        }

        public static float ApplyFaceAndLipSync(
            Live2DModelWrapper model,
            TachieSourceDescription description,
            Live2DFaceParameter activeFace,
            double faceLocalFrame,
            double faceDurationFrame,
            float lipSyncGain,
            bool applyManualFaceParameters)
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

            if (applyManualFaceParameters)
            {
                ApplyStandardFaceParameters(
                    model,
                    activeFace.AdditiveParameters,
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
            }

            var (lipValue, mouthFormValue) = ComputeLipSync(description.MouthShape, description.VoiceVolume, lipSyncGain);
            model.SetLipSyncValue(lipValue);
            ApplyParameterValue(model, Live2DManager.ParamMouthOpenY, lipValue, activeFace.AdditiveParameters);
            ApplyParameterValue(model, Live2DManager.ParamMouthForm, mouthFormValue, activeFace.AdditiveParameters);

            ApplyCustomFaceParts(model, activeFace, frame, length, fps);
            ApplyCustomFaceParameters(model, activeFace, frame, length, fps);
            return lipValue;
        }

        private static void ApplyCustomFaceParts(
            Live2DModelWrapper model,
            Live2DFaceParameter activeFace,
            long frame,
            long length,
            int fps)
        {
            ApplyCustomFacePartOpacity(model, activeFace.CustomPart1Id, activeFace.CustomPart1Opacity, frame, length, fps);
            ApplyCustomFacePartOpacity(model, activeFace.CustomPart2Id, activeFace.CustomPart2Opacity, frame, length, fps);
            ApplyCustomFacePartOpacity(model, activeFace.CustomPart3Id, activeFace.CustomPart3Opacity, frame, length, fps);
        }

        private static void ApplyCustomFacePartOpacity(
            Live2DModelWrapper model,
            string? partId,
            Animation animation,
            long frame,
            long length,
            int fps)
        {
            if (string.IsNullOrWhiteSpace(partId))
            {
                return;
            }

            var opacity = (float)Math.Clamp(animation.GetValue(frame, length, fps), 0.0, 1.0);
            model.SetPartOpacity(partId, opacity);
        }

        private static void ApplyCustomFaceParameters(
            Live2DModelWrapper model,
            Live2DFaceParameter activeFace,
            long frame,
            long length,
            int fps)
        {
            ApplyCustomFaceParameter(model, activeFace.CustomParam1Id, activeFace.CustomParam1Value, frame, length, fps, activeFace.AdditiveParameters);
            ApplyCustomFaceParameter(model, activeFace.CustomParam2Id, activeFace.CustomParam2Value, frame, length, fps, activeFace.AdditiveParameters);
            ApplyCustomFaceParameter(model, activeFace.CustomParam3Id, activeFace.CustomParam3Value, frame, length, fps, activeFace.AdditiveParameters);
        }

        private static void ApplyCustomFaceParameter(
            Live2DModelWrapper model,
            string? paramId,
            Animation animation,
            long frame,
            long length,
            int fps,
            bool additiveParameters)
        {
            if (string.IsNullOrWhiteSpace(paramId))
            {
                return;
            }

            var value = (float)animation.GetValue(frame, length, fps);
            ApplyParameterValue(model, paramId, value, additiveParameters);
        }

        private static void ApplyStandardFaceParameters(
            Live2DModelWrapper model,
            bool additiveParameters,
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
            ApplyParameterValue(model, Live2DManager.ParamEyeLOpen, eyeLOpen, additiveParameters);
            ApplyParameterValue(model, Live2DManager.ParamEyeROpen, eyeROpen, additiveParameters);
            ApplyParameterValue(model, Live2DManager.ParamMouthOpenY, mouthOpenY, additiveParameters);
            ApplyParameterValue(model, Live2DManager.ParamMouthForm, mouthForm, additiveParameters);
            ApplyParameterValue(model, Live2DManager.ParamAngleX, angleX, additiveParameters);
            ApplyParameterValue(model, Live2DManager.ParamAngleY, angleY, additiveParameters);
            ApplyParameterValue(model, Live2DManager.ParamAngleZ, angleZ, additiveParameters);
            ApplyParameterValue(model, Live2DManager.ParamBodyAngleX, bodyAngleX, additiveParameters);
            ApplyParameterValue(model, Live2DManager.ParamEyeBallX, eyeBallX, additiveParameters);
            ApplyParameterValue(model, Live2DManager.ParamEyeBallY, eyeBallY, additiveParameters);
            ApplyParameterValue(model, Live2DManager.ParamCheek, cheek, additiveParameters);
            ApplyParameterValue(model, Live2DManager.ParamArmLA, armLA, additiveParameters);
            ApplyParameterValue(model, Live2DManager.ParamArmRA, armRA, additiveParameters);
        }

        private static void ApplyParameterValue(Live2DModelWrapper model, string parameterId, float value, bool additiveParameters)
        {
            if (additiveParameters)
            {
                model.AddParameterValue(parameterId, value);
            }
            else
            {
                model.SetParameterValue(parameterId, value);
            }
        }

        public static bool ShouldApplyManualFaceParameters(Live2DFaceParameter activeFace)
        {
            var hasMotion = activeFace.UseMotion && activeFace.MotionIndex >= 0;
            return !hasMotion;
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
