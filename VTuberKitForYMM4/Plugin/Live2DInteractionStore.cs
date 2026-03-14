using System.Collections.Immutable;
using System.Collections.Concurrent;
using System.Linq;

namespace VTuberKitForYMM4.Plugin
{
    internal static class Live2DInteractionStore
    {
        internal sealed record InteractionTargetState(
            string LinkId,
            string DisplayName,
            string ModelFile,
            long Sequence,
            long LastSeenUtcTicks);

        internal sealed record InteractionHitAreaState(
            string Id,
            string Name,
            float X,
            float Y,
            float Width,
            float Height);

        internal sealed record InteractionTransformState(
            float PositionX,
            float PositionY,
            float Scale,
            float RotationDegrees,
            float ModelCenterX,
            float ModelCenterY,
            float HitTestScaleX,
            float HitTestScaleY,
            float HitTestTranslateX,
            float HitTestTranslateY,
            int ScreenWidth,
            int ScreenHeight);

        internal sealed record TargetPointState(
            string SourceId,
            string LinkId,
            float X,
            float Y,
            int Layer);

        internal sealed record HitAreaRectState(
            string SourceId,
            string LinkId,
            string HitAreaName,
            string ExpressionId,
            string MotionGroup,
            int MotionIndex,
            ImmutableArray<HitAreaParameterOverrideState> ParameterOverrides,
            ImmutableArray<HitAreaPartOverrideState> PartOverrides,
            float X,
            float Y,
            float Width,
            float Height,
            bool IsHit,
            int Layer);

        internal readonly record struct HitAreaParameterOverrideState(
            string Id,
            float Value);

        internal readonly record struct HitAreaPartOverrideState(
            string Id,
            float Opacity);

        private static readonly ConcurrentDictionary<string, TargetPointState> TargetPoints = new();
        private static readonly ConcurrentDictionary<string, HitAreaRectState> HitAreaRects = new();
        private static readonly ConcurrentDictionary<string, InteractionTransformState> InteractionTransforms = new();
        private static readonly ConcurrentDictionary<string, InteractionHitAreaState[]> InteractionHitAreas = new();
        private static readonly ConcurrentDictionary<string, InteractionTargetState> InteractionTargets = new();
        private static long InteractionTargetSequence;
        private static readonly TimeSpan InteractionTargetTtl = TimeSpan.FromSeconds(10);

        public static void UpdateTargetPoint(string sourceId, string linkId, float x, float y, int layer)
        {
            if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(linkId))
                return;

            TargetPoints[sourceId] = new TargetPointState(sourceId, linkId, x, y, layer);
        }

        public static bool TryGetTargetPoint(string? linkId, out float x, out float y)
        {
            x = 0.0f;
            y = 0.0f;
            if (string.IsNullOrWhiteSpace(linkId))
                return false;

            var point = GetPreferredTargetPoint(linkId);
            if (point is null)
                return false;

            x = point.X;
            y = point.Y;
            return true;
        }

        public static void RemoveTargetPoint(string? sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
                return;

            TargetPoints.TryRemove(sourceId, out _);
        }

        public static void UpdateInteractionTransform(
            string? linkId,
            float positionX,
            float positionY,
            float scale,
            float rotationDegrees,
            float modelCenterX,
            float modelCenterY,
            float hitTestScaleX,
            float hitTestScaleY,
            float hitTestTranslateX,
            float hitTestTranslateY,
            int screenWidth,
            int screenHeight)
        {
            if (string.IsNullOrWhiteSpace(linkId))
                return;

            InteractionTransforms[linkId] = new InteractionTransformState(
                positionX,
                positionY,
                scale,
                rotationDegrees,
                modelCenterX,
                modelCenterY,
                hitTestScaleX,
                hitTestScaleY,
                hitTestTranslateX,
                hitTestTranslateY,
                screenWidth,
                screenHeight);
        }

        public static bool TryGetInteractionTransform(string? linkId, out InteractionTransformState? state)
        {
            if (!string.IsNullOrWhiteSpace(linkId) && InteractionTransforms.TryGetValue(linkId, out var transform))
            {
                state = transform;
                return true;
            }

            state = null;
            return false;
        }

        public static void UpdateInteractionTarget(string? linkId, string? displayName, string? modelFile = null)
        {
            if (string.IsNullOrWhiteSpace(linkId))
                return;

            var resolvedName = string.IsNullOrWhiteSpace(displayName) ? linkId : displayName;
            var sequence = Interlocked.Increment(ref InteractionTargetSequence);
            InteractionTargets[linkId] = new InteractionTargetState(linkId, resolvedName, modelFile ?? string.Empty, sequence, DateTime.UtcNow.Ticks);
        }

        public static IReadOnlyList<InteractionTargetState> GetInteractionTargets()
        {
            var nowTicks = DateTime.UtcNow.Ticks;
            var minTicks = nowTicks - InteractionTargetTtl.Ticks;

            foreach (var item in InteractionTargets)
            {
                if (item.Value.LastSeenUtcTicks < minTicks)
                {
                    InteractionTargets.TryRemove(item.Key, out _);
                    InteractionTransforms.TryRemove(item.Key, out _);
                    InteractionHitAreas.TryRemove(item.Key, out _);
                }
            }

            return InteractionTargets.Values
                .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(x => x.Sequence)
                .ToArray();
        }

        public static void RemoveInteractionTarget(string? linkId)
        {
            if (string.IsNullOrWhiteSpace(linkId))
                return;

            InteractionTargets.TryRemove(linkId, out _);
            InteractionTransforms.TryRemove(linkId, out _);
            InteractionHitAreas.TryRemove(linkId, out _);
        }

        public static string GetInteractionTargetModelFile(string? linkId)
        {
            if (!string.IsNullOrWhiteSpace(linkId) && InteractionTargets.TryGetValue(linkId, out var target))
            {
                return target.ModelFile;
            }

            return string.Empty;
        }

        public static void UpdateInteractionHitAreas(string? linkId, IEnumerable<InteractionHitAreaState> hitAreas)
        {
            if (string.IsNullOrWhiteSpace(linkId))
                return;

            InteractionHitAreas[linkId] = hitAreas?.ToArray() ?? [];
        }

        public static bool TryGetInteractionHitArea(string? linkId, string? hitAreaNameOrId, out InteractionHitAreaState? state)
        {
            if (!string.IsNullOrWhiteSpace(linkId) &&
                !string.IsNullOrWhiteSpace(hitAreaNameOrId) &&
                InteractionHitAreas.TryGetValue(linkId, out var hitAreas))
            {
                state = hitAreas.FirstOrDefault(x =>
                    string.Equals(x.Name, hitAreaNameOrId, StringComparison.Ordinal) ||
                    string.Equals(x.Id, hitAreaNameOrId, StringComparison.Ordinal));
                return state is not null;
            }

            state = null;
            return false;
        }

        public static void UpdateHitAreaRect(
            string sourceId,
            string linkId,
            string hitAreaName,
            string expressionId,
            string motionGroup,
            int motionIndex,
            HitAreaParameterOverrideState[]? parameterOverrides,
            HitAreaPartOverrideState[]? partOverrides,
            float x,
            float y,
            float width,
            float height,
            int layer)
        {
            if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(linkId))
                return;

            var isHit = HitAreaRects.TryGetValue(sourceId, out var existing) && existing.IsHit;
            HitAreaRects[sourceId] = new HitAreaRectState(
                sourceId,
                linkId,
                hitAreaName ?? string.Empty,
                expressionId ?? string.Empty,
                motionGroup ?? string.Empty,
                motionIndex,
                parameterOverrides is { Length: > 0 }
                    ? ImmutableArray.CreateRange(parameterOverrides)
                    : ImmutableArray<HitAreaParameterOverrideState>.Empty,
                partOverrides is { Length: > 0 }
                    ? ImmutableArray.CreateRange(partOverrides)
                    : ImmutableArray<HitAreaPartOverrideState>.Empty,
                x,
                y,
                Math.Abs(width),
                Math.Abs(height),
                isHit,
                layer);
        }

        public static IReadOnlyList<HitAreaRectState> GetHitAreaRects(string? linkId)
        {
            if (string.IsNullOrWhiteSpace(linkId))
                return [];

            return HitAreaRects.Values.Where(x => x.LinkId == linkId).ToArray();
        }

        internal static TargetPointState? GetPreferredTargetPoint(string? linkId)
        {
            if (string.IsNullOrWhiteSpace(linkId))
                return null;

            return TargetPoints.Values
                .Where(x => string.Equals(x.LinkId, linkId, StringComparison.Ordinal))
                .OrderByDescending(x => x.Layer)
                .ThenBy(x => x.SourceId, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        internal static HitAreaRectState? GetPreferredHitAreaReaction(string? linkId, Func<string, double?> activeSinceSecondsProvider)
        {
            if (string.IsNullOrWhiteSpace(linkId))
                return null;

            return HitAreaRects.Values
                .Where(x => string.Equals(x.LinkId, linkId, StringComparison.Ordinal))
                .Where(x => x.IsHit)
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.ExpressionId) ||
                    x.MotionIndex >= 0 ||
                    !x.ParameterOverrides.IsDefaultOrEmpty ||
                    !x.PartOverrides.IsDefaultOrEmpty)
                .Select(x => new
                {
                    HitArea = x,
                    ActiveSinceSeconds = activeSinceSecondsProvider(x.SourceId) ?? double.MinValue
                })
                .OrderByDescending(x => x.HitArea.Layer)
                .ThenByDescending(x => x.ActiveSinceSeconds)
                .ThenBy(x => x.HitArea.SourceId, StringComparer.Ordinal)
                .Select(x => x.HitArea)
                .FirstOrDefault();
        }

        public static bool TryGetHitAreaRect(string sourceId, out HitAreaRectState? state)
        {
            if (HitAreaRects.TryGetValue(sourceId, out var hitArea))
            {
                state = hitArea;
                return true;
            }

            state = null;
            return false;
        }

        public static void SetHitAreaResult(string sourceId, bool isHit)
        {
            if (!HitAreaRects.TryGetValue(sourceId, out var state))
                return;

            HitAreaRects[sourceId] = state with { IsHit = isHit };
        }

        public static void RemoveHitAreaRect(string sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
                return;

            HitAreaRects.TryRemove(sourceId, out _);
        }

        internal static void ClearHitAreaResults(string? linkId = null)
        {
            foreach (var pair in HitAreaRects)
            {
                if (!string.IsNullOrWhiteSpace(linkId) &&
                    !string.Equals(pair.Value.LinkId, linkId, StringComparison.Ordinal))
                {
                    continue;
                }

                HitAreaRects[pair.Key] = pair.Value with { IsHit = false };
            }
        }

        internal static void Clear()
        {
            TargetPoints.Clear();
            HitAreaRects.Clear();
            InteractionTransforms.Clear();
            InteractionHitAreas.Clear();
            InteractionTargets.Clear();
            InteractionTargetSequence = 0;
        }
    }
}
