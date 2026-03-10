using Vortice.Mathematics;

namespace VTuberKitForYMM4.Plugin.Shape
{
    internal static class InteractionShapeColors
    {
        public static Color4 GetTargetPointColor(string? linkId)
        {
            return ApplyLinkTint(new Color4(0.10f, 0.90f, 0.90f, 0.85f), linkId, 0.10f, 0.06f);
        }

        public static Color4 GetHitAreaHitColor(string? linkId)
        {
            return ApplyLinkTint(new Color4(0.20f, 0.90f, 0.20f, 0.90f), linkId, 0.08f, 0.05f);
        }

        public static Color4 GetHitAreaMissColor(string? linkId)
        {
            return ApplyLinkTint(new Color4(0.95f, 0.25f, 0.25f, 0.90f), linkId, 0.08f, 0.05f);
        }

        private static Color4 ApplyLinkTint(Color4 baseColor, string? linkId, float rgbRange, float brightnessRange)
        {
            if (string.IsNullOrWhiteSpace(linkId))
            {
                return baseColor;
            }

            var hash = GetStableHash(linkId);
            var rShift = GetShift(hash, 0, rgbRange);
            var gShift = GetShift(hash, 8, rgbRange);
            var bShift = GetShift(hash, 16, rgbRange);
            var brightness = GetShift(hash, 24, brightnessRange);

            return new Color4(
                Clamp01(baseColor.R + rShift + brightness),
                Clamp01(baseColor.G + gShift + brightness),
                Clamp01(baseColor.B + bShift + brightness),
                baseColor.A);
        }

        private static int GetStableHash(string value)
        {
            unchecked
            {
                var hash = 23;
                foreach (var ch in value)
                {
                    hash = (hash * 31) + ch;
                }

                return hash;
            }
        }

        private static float GetShift(int hash, int bitOffset, float range)
        {
            var sample = (hash >> bitOffset) & 0xFF;
            var normalized = (sample / 255.0f) - 0.5f;
            return normalized * 2.0f * range;
        }

        private static float Clamp01(float value)
        {
            return Math.Clamp(value, 0.0f, 1.0f);
        }
    }
}
