using System.Numerics;

namespace VTuberKitForYMM4.Plugin.Shape
{
    internal static class InteractionShapeTransform
    {
        private static float GetIsotropicPixelScale(int screenWidth, int screenHeight)
        {
            var width = Math.Max(1, screenWidth);
            var height = Math.Max(1, screenHeight);
            return Math.Min(width, height) / 2.0f;
        }

        private static Vector2 GetTranslationPixelScale(int screenWidth, int screenHeight)
        {
            var width = Math.Max(1, screenWidth);
            var height = Math.Max(1, screenHeight);
            return new Vector2(width / 2.0f, height / 2.0f);
        }

        private static Vector2 TransformLocalPointToModelSpace(
            Vector2 localPoint,
            Live2DInteractionStore.InteractionTransformState state)
        {
            var anchoredX = state.ModelCenterX + localPoint.X;
            var anchoredY = state.ModelCenterY + localPoint.Y;
            return new Vector2(
                anchoredX * state.HitTestScaleX + state.HitTestTranslateX,
                anchoredY * state.HitTestScaleY + state.HitTestTranslateY);
        }

        private static Vector2 TransformModelSpacePointToViewSpace(
            Vector2 modelPoint,
            Live2DInteractionStore.InteractionTransformState state)
        {
            var rotated = TransformModelSpacePointToViewSpaceWithoutTranslation(modelPoint, state);
            return new Vector2(
                rotated.X + state.PositionX,
                rotated.Y + state.PositionY);
        }

        private static Vector2 TransformModelSpacePointToViewSpaceWithoutTranslation(
            Vector2 modelPoint,
            Live2DInteractionStore.InteractionTransformState state)
        {
            var radians = state.RotationDegrees * (MathF.PI / 180.0f);
            var cos = MathF.Cos(radians) * state.Scale;
            var sin = MathF.Sin(radians) * state.Scale;
            return new Vector2(
                modelPoint.X * cos - modelPoint.Y * sin,
                modelPoint.X * sin + modelPoint.Y * cos);
        }

        public static Vector2 TransformHitBoxCenter(
            Vector2 localCenter,
            Vector2 anchor,
            Live2DInteractionStore.InteractionTransformState? state)
        {
            return TransformHitBoxPoint(localCenter, anchor, state);
        }

        public static Vector2 TransformHitBoxPoint(
            Vector2 localPoint,
            Vector2 anchor,
            Live2DInteractionStore.InteractionTransformState? state)
        {
            if (state is null)
            {
                return localPoint + anchor;
            }

            var modelPoint = TransformLocalPointToModelSpace(localPoint + anchor, state);
            return TransformModelSpacePointToViewSpace(modelPoint, state);
        }

        public static Vector2 TransformTargetPoint(
            Vector2 localPoint,
            Live2DInteractionStore.InteractionTransformState? state)
        {
            if (state is null)
            {
                return localPoint;
            }

            var modelPoint = TransformLocalPointToModelSpace(localPoint, state);
            return TransformModelSpacePointToViewSpace(modelPoint, state);
        }

        public static Vector2 TransformLocalVector(
            Vector2 localVector,
            Live2DInteractionStore.InteractionTransformState? state)
        {
            if (state is null)
            {
                return localVector;
            }

            var modelVector = new Vector2(
                localVector.X * state.HitTestScaleX,
                localVector.Y * state.HitTestScaleY);
            var radians = state.RotationDegrees * (MathF.PI / 180.0f);
            var cos = MathF.Cos(radians) * state.Scale;
            var sin = MathF.Sin(radians) * state.Scale;
            return new Vector2(
                modelVector.X * cos - modelVector.Y * sin,
                modelVector.X * sin + modelVector.Y * cos);
        }

        public static Vector2 TransformTargetPointToPixel(
            Vector2 localPoint,
            Live2DInteractionStore.InteractionTransformState? state,
            int screenWidth,
            int screenHeight)
        {
            return TransformAnchoredPointToPixel(localPoint, Vector2.Zero, state, screenWidth, screenHeight);
        }

        public static Vector2 TransformHitBoxPointToPixel(
            Vector2 localPoint,
            Vector2 anchor,
            Live2DInteractionStore.InteractionTransformState? state,
            int screenWidth,
            int screenHeight)
        {
            return TransformAnchoredPointToPixel(localPoint, anchor, state, screenWidth, screenHeight);
        }

        public static Vector2 TransformLocalVectorToPixel(
            Vector2 localVector,
            Live2DInteractionStore.InteractionTransformState? state,
            int screenWidth,
            int screenHeight)
        {
            var transformed = TransformLocalVector(localVector, state);
            var isotropicScale = GetIsotropicPixelScale(screenWidth, screenHeight);
            return ToPixel(transformed, isotropicScale, isotropicScale);
        }

        private static Vector2 TransformAnchoredPointToPixel(
            Vector2 localPoint,
            Vector2 anchor,
            Live2DInteractionStore.InteractionTransformState? state,
            int screenWidth,
            int screenHeight)
        {
            if (state is null)
            {
                return ToPixel(localPoint + anchor, screenWidth, screenHeight);
            }

            var modelPoint = TransformLocalPointToModelSpace(localPoint + anchor, state);
            var viewPointWithoutTranslation = TransformModelSpacePointToViewSpaceWithoutTranslation(modelPoint, state);
            var isotropicScale = GetIsotropicPixelScale(screenWidth, screenHeight);
            var modelPixels = ToPixel(viewPointWithoutTranslation, isotropicScale, isotropicScale);
            var translationPixels = TranslationToPixel(
                new Vector2(state.PositionX, state.PositionY),
                screenWidth,
                screenHeight);
            return modelPixels + translationPixels;
        }

        public static Vector2 ToPixel(Vector2 point, int screenWidth, int screenHeight)
        {
            // Live2D applies aspect correction in model space before the final 2D output scale.
            // For wide clips the effective X pixel scale matches the clip height, not the width.
            var isotropicScale = GetIsotropicPixelScale(screenWidth, screenHeight);
            return ToPixel(point, isotropicScale, isotropicScale);
        }

        public static Vector2 PixelToLocal(Vector2 pixelPoint, int screenWidth, int screenHeight)
        {
            var isotropicScale = GetIsotropicPixelScale(screenWidth, screenHeight);
            if (isotropicScale <= 0.0f)
            {
                return Vector2.Zero;
            }

            return new Vector2(
                pixelPoint.X / isotropicScale,
                -pixelPoint.Y / isotropicScale);
        }

        public static Vector2 PixelToTranslation(Vector2 pixelPoint, int screenWidth, int screenHeight)
        {
            var scale = GetTranslationPixelScale(screenWidth, screenHeight);
            if (scale.X <= 0.0f || scale.Y <= 0.0f)
            {
                return Vector2.Zero;
            }

            return new Vector2(
                pixelPoint.X / scale.X,
                -pixelPoint.Y / scale.Y);
        }

        public static Vector2 TranslationToPixel(Vector2 translation, int screenWidth, int screenHeight)
        {
            var scale = GetTranslationPixelScale(screenWidth, screenHeight);
            return new Vector2(
                translation.X * scale.X,
                -translation.Y * scale.Y);
        }

        public static float PixelToLocalScalar(float pixelValue, int screenWidth, int screenHeight)
        {
            var isotropicScale = GetIsotropicPixelScale(screenWidth, screenHeight);
            if (isotropicScale <= 0.0f)
            {
                return 0.0f;
            }

            return pixelValue / isotropicScale;
        }

        public static Vector2 ToPixel(Vector2 point, float pixelScaleX, float pixelScaleY)
        {
            return new Vector2(point.X * pixelScaleX, -point.Y * pixelScaleY);
        }

    }
}
