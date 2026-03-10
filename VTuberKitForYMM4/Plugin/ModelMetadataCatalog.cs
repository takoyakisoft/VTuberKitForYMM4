using System.IO;
using System.Globalization;
using Newtonsoft.Json.Linq;
using VTuberKitForNative;

namespace VTuberKitForYMM4.Plugin
{
    public static class ModelMetadataCatalog
    {
        public readonly record struct ParameterMetadata(string Id, string Name, float Default, float Min, float Max);
        public readonly record struct HitTestTransform(float ScaleX, float ScaleY, float TranslateX, float TranslateY)
        {
            public static HitTestTransform Identity => new(1.0f, 1.0f, 0.0f, 0.0f);
        }
        private sealed record ModelMetadataSnapshot(
            string ModelPath,
            IReadOnlyList<string> Expressions,
            IReadOnlyList<(string Group, int Index, string FileName)> Motions,
            IReadOnlyList<(string Id, string Name)> Parameters,
            IReadOnlyDictionary<string, ParameterMetadata> ParameterMetadataById,
            IReadOnlyList<(string Id, string Name)> Parts,
            IReadOnlyList<(string Id, string Name)> HitAreas,
            IReadOnlyDictionary<string, float> Layout,
            LipSyncVowelParameterIds LipSyncVowelParameters,
            IReadOnlyList<string> ParameterIds,
            IReadOnlyList<string> PartIds);
        public readonly record struct LipSyncVowelParameterIds(string A, string I, string U, string E, string O)
        {
            public bool HasAny =>
                !string.IsNullOrWhiteSpace(A) ||
                !string.IsNullOrWhiteSpace(I) ||
                !string.IsNullOrWhiteSpace(U) ||
                !string.IsNullOrWhiteSpace(E) ||
                !string.IsNullOrWhiteSpace(O);
        }

        private static readonly object LockObj = new();
        private static readonly Dictionary<string, ModelMetadataSnapshot> SnapshotCache = new(StringComparer.OrdinalIgnoreCase);
        private static string currentModelPath = string.Empty;
        private static List<string> expressions = [];
        private static List<(string Group, int Index, string FileName)> motions = [];
        private static List<(string Id, string Name)> parameters = [];
        private static Dictionary<string, ParameterMetadata> parameterMetadataById = new(StringComparer.OrdinalIgnoreCase);
        private static List<(string Id, string Name)> parts = [];
        private static List<(string Id, string Name)> hitAreas = [];
        private static LipSyncVowelParameterIds lipSyncVowelParameters;
        private static List<string> parameterIds = [];
        private static List<string> partIds = [];

        public static IReadOnlyList<string> Expressions
        {
            get
            {
                lock (LockObj)
                {
                    return expressions.ToArray();
                }
            }
        }

        public static IReadOnlyList<(string Group, int Index, string FileName)> Motions
        {
            get
            {
                lock (LockObj)
                {
                    return motions.ToArray();
                }
            }
        }

        public static IReadOnlyList<string> ParameterIds
        {
            get
            {
                lock (LockObj)
                {
                    return parameterIds.ToArray();
                }
            }
        }

        public static IReadOnlyList<(string Id, string Name)> Parameters
        {
            get
            {
                lock (LockObj)
                {
                    return parameters.ToArray();
                }
            }
        }

        public static IReadOnlyList<string> PartIds
        {
            get
            {
                lock (LockObj)
                {
                    return partIds.ToArray();
                }
            }
        }

        public static IReadOnlyList<(string Id, string Name)> Parts
        {
            get
            {
                lock (LockObj)
                {
                    return parts.ToArray();
                }
            }
        }

        public static bool TryGetParameterMetadata(string? id, out ParameterMetadata metadata)
        {
            lock (LockObj)
            {
                if (!string.IsNullOrWhiteSpace(id) && parameterMetadataById.TryGetValue(id, out metadata))
                {
                    return true;
                }
            }

            metadata = default;
            return false;
        }

        public static IReadOnlyList<(string Id, string Name)> HitAreas
        {
            get
            {
                lock (LockObj)
                {
                    return hitAreas.ToArray();
                }
            }
        }

        public static LipSyncVowelParameterIds LipSyncVowelParameters
        {
            get
            {
                lock (LockObj)
                {
                    return lipSyncVowelParameters;
                }
            }
        }

        public static void UpdateFromModelPath(string? modelPath)
        {
            lock (LockObj)
            {
                var snapshot = GetSnapshotCore(modelPath);
                if (snapshot == null)
                {
                    currentModelPath = string.Empty;
                    expressions = [];
                    motions = [];
                    parameters = [];
                    parameterMetadataById = new(StringComparer.OrdinalIgnoreCase);
                    parts = [];
                    hitAreas = [];
                    lipSyncVowelParameters = default;
                    parameterIds = [];
                    partIds = [];
                    return;
                }

                if (string.Equals(currentModelPath, snapshot.ModelPath, StringComparison.OrdinalIgnoreCase) &&
                    parameterMetadataById.Count > 0)
                {
                    return;
                }

                currentModelPath = snapshot.ModelPath;
                expressions = snapshot.Expressions.ToList();
                motions = snapshot.Motions.ToList();
                parameters = snapshot.Parameters.ToList();
                parameterMetadataById = new Dictionary<string, ParameterMetadata>(snapshot.ParameterMetadataById, StringComparer.OrdinalIgnoreCase);
                parts = snapshot.Parts.ToList();
                hitAreas = snapshot.HitAreas.ToList();
                lipSyncVowelParameters = snapshot.LipSyncVowelParameters;
                parameterIds = snapshot.ParameterIds.ToList();
                partIds = snapshot.PartIds.ToList();
            }
        }

        public static IReadOnlyList<string> GetExpressions(string? modelPath)
        {
            lock (LockObj)
            {
                return GetSnapshotCore(modelPath)?.Expressions ?? [];
            }
        }

        public static IReadOnlyList<(string Group, int Index, string FileName)> GetMotions(string? modelPath)
        {
            lock (LockObj)
            {
                return GetSnapshotCore(modelPath)?.Motions ?? [];
            }
        }

        public static IReadOnlyList<(string Id, string Name)> GetHitAreas(string? modelPath)
        {
            lock (LockObj)
            {
                return GetSnapshotCore(modelPath)?.HitAreas ?? [];
            }
        }

        public static HitTestTransform GetHitTestTransform(string? modelPath, float canvasWidth, float canvasHeight)
        {
            lock (LockObj)
            {
                var snapshot = GetSnapshotCore(modelPath);
                if (snapshot == null || canvasWidth <= 0.0f || canvasHeight <= 0.0f)
                {
                    return HitTestTransform.Identity;
                }

                var scale = 2.0f / canvasHeight;
                if (TryGetLayoutValue(snapshot.Layout, "height", out var layoutHeight))
                {
                    scale = layoutHeight / canvasHeight;
                }

                if (TryGetLayoutValue(snapshot.Layout, "width", out var layoutWidth))
                {
                    scale = layoutWidth / canvasWidth;
                }

                var translatedWidth = canvasWidth * scale;
                var translatedHeight = canvasHeight * scale;
                var translateX = 0.0f;
                var translateY = 0.0f;

                if (TryGetLayoutValue(snapshot.Layout, "x", out var x))
                {
                    translateX = x;
                }
                else if (TryGetLayoutValue(snapshot.Layout, "center_x", out var centerX))
                {
                    translateX = centerX - (translatedWidth / 2.0f);
                }
                else if (TryGetLayoutValue(snapshot.Layout, "left", out var left))
                {
                    translateX = left;
                }
                else if (TryGetLayoutValue(snapshot.Layout, "right", out var right))
                {
                    translateX = right - translatedWidth;
                }

                if (TryGetLayoutValue(snapshot.Layout, "y", out var y))
                {
                    translateY = y;
                }
                else if (TryGetLayoutValue(snapshot.Layout, "center_y", out var centerY))
                {
                    translateY = centerY - (translatedHeight / 2.0f);
                }
                else if (TryGetLayoutValue(snapshot.Layout, "top", out var top))
                {
                    translateY = top;
                }
                else if (TryGetLayoutValue(snapshot.Layout, "bottom", out var bottom))
                {
                    translateY = bottom - translatedHeight;
                }

                return new HitTestTransform(scale, scale, translateX, translateY);
            }
        }

        private static LipSyncVowelParameterIds ResolveLipSyncVowelParameterIds(IEnumerable<ParameterMetadata> candidates)
        {
            var items = candidates.ToArray();
            return new LipSyncVowelParameterIds(
                FindVowelParameterId(items, "A", "あ"),
                FindVowelParameterId(items, "I", "い"),
                FindVowelParameterId(items, "U", "う"),
                FindVowelParameterId(items, "E", "え"),
                FindVowelParameterId(items, "O", "お"));
        }

        private static string FindVowelParameterId(IEnumerable<ParameterMetadata> candidates, string latin, string kana)
        {
            foreach (var candidate in candidates)
            {
                if (IsMatchingVowelParameter(candidate.Id, latin, kana) ||
                    IsMatchingVowelParameter(candidate.Name, latin, kana))
                {
                    return candidate.Id;
                }
            }

            return string.Empty;
        }

        private static bool IsMatchingVowelParameter(string? value, string latin, string kana)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            if (string.Equals(trimmed, kana, StringComparison.Ordinal) ||
                string.Equals(trimmed, latin, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, $"Param{latin}", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static IEnumerable<ParameterMetadata> LoadNativeParameterMetadata(string modelPath)
        {
            var loadedParameters = new List<ParameterMetadata>();
            try
            {
                var manager = Live2DManager.GetInstance();
                manager?.Initialize();
                if (manager == null)
                {
                    return loadedParameters;
                }

                var model = manager.CreateModel();
                if (model == null)
                {
                    return loadedParameters;
                }

                using (model)
                {
                    if (!model.LoadModel(modelPath))
                    {
                        return loadedParameters;
                    }

                    foreach (var parameter in model.GetParameters())
                    {
                        if (parameter == null || string.IsNullOrWhiteSpace(parameter.Id))
                        {
                            continue;
                        }

                        loadedParameters.Add(new ParameterMetadata(
                            parameter.Id,
                            parameter.Name ?? string.Empty,
                            parameter.Default,
                            parameter.Min,
                            parameter.Max));
                    }
                }
            }
            catch (Exception)
            {
            }

            return loadedParameters;
        }

        private static ModelMetadataSnapshot? GetSnapshotCore(string? modelPath)
        {
            if (string.IsNullOrWhiteSpace(modelPath) ||
                !modelPath.EndsWith(".model3.json", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(modelPath))
            {
                return null;
            }

            var normalizedPath = Path.GetFullPath(modelPath);
            if (SnapshotCache.TryGetValue(normalizedPath, out var cached) &&
                cached.ParameterMetadataById.Count > 0)
            {
                return cached;
            }

            var snapshot = LoadSnapshot(normalizedPath);
            if (snapshot != null)
            {
                SnapshotCache[normalizedPath] = snapshot;
            }

            return snapshot;
        }

        private static ModelMetadataSnapshot? LoadSnapshot(string modelPath)
        {
            var loadedExpressions = new List<string>();
            var loadedMotions = new List<(string Group, int Index, string FileName)>();
            var loadedParameters = new List<(string Id, string Name)>();
            var loadedParameterMetadata = new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase);
            var loadedParts = new List<(string Id, string Name)>();
            var loadedHitAreas = new List<(string Id, string Name)>();
            var loadedLayout = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            var loadedParameterIds = new List<string>();
            var loadedPartIds = new List<string>();

            try
            {
                var root = JObject.Parse(File.ReadAllText(modelPath));
                var refs = root["FileReferences"] as JObject;
                if (refs == null)
                {
                    return null;
                }

                if (refs["Expressions"] is JArray exprArray)
                {
                    foreach (var expr in exprArray)
                    {
                        var name = expr?["Name"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            loadedExpressions.Add(name);
                        }
                    }
                }

                if (refs["Motions"] is JObject motionObj)
                {
                    foreach (var prop in motionObj.Properties())
                    {
                        var group = prop.Name ?? string.Empty;
                        if (prop.Value is not JArray arr)
                        {
                            continue;
                        }

                        for (var i = 0; i < arr.Count; i++)
                        {
                            var fileName = arr[i]?["File"]?.ToString() ?? string.Empty;
                            loadedMotions.Add((group, i, fileName));
                        }
                    }
                }

                foreach (var parameter in LoadNativeParameterMetadata(modelPath))
                {
                    loadedParameterMetadata[parameter.Id] = parameter;
                }

                if (root["HitAreas"] is JArray hitAreaArray)
                {
                    foreach (var hitArea in hitAreaArray)
                    {
                        var id = hitArea?["Id"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            var name = hitArea?["Name"]?.ToString() ?? string.Empty;
                            loadedHitAreas.Add((id, name));
                        }
                    }
                }

                if (root["Layout"] is JObject layoutObject)
                {
                    foreach (var property in layoutObject.Properties())
                    {
                        if (float.TryParse(property.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                        {
                            loadedLayout[property.Name] = value;
                        }
                    }
                }

                var modelDirectory = Path.GetDirectoryName(modelPath) ?? string.Empty;
                var displayInfoRelativePath = refs["DisplayInfo"]?.ToString();
                if (!string.IsNullOrWhiteSpace(displayInfoRelativePath))
                {
                    var displayInfoPath = Path.GetFullPath(Path.Combine(modelDirectory, displayInfoRelativePath));
                    if (File.Exists(displayInfoPath))
                    {
                        var displayInfo = JObject.Parse(File.ReadAllText(displayInfoPath));
                        if (displayInfo["Parameters"] is JArray parameterArray)
                        {
                            foreach (var parameter in parameterArray)
                            {
                                var id = parameter?["Id"]?.ToString();
                                if (!string.IsNullOrWhiteSpace(id))
                                {
                                    var name = parameter?["Name"]?.ToString() ?? string.Empty;
                                    if (loadedParameterMetadata.TryGetValue(id, out var metadata))
                                    {
                                        loadedParameterMetadata[id] = metadata with { Name = name };
                                    }
                                    loadedParameters.Add((id, name));
                                    loadedParameterIds.Add(id);
                                }
                            }
                        }

                        if (displayInfo["Parts"] is JArray partArray)
                        {
                            foreach (var part in partArray)
                            {
                                var id = part?["Id"]?.ToString();
                                if (!string.IsNullOrWhiteSpace(id))
                                {
                                    var name = part?["Name"]?.ToString() ?? string.Empty;
                                    loadedParts.Add((id, name));
                                    loadedPartIds.Add(id);
                                }
                            }
                        }
                    }
                }

                loadedParameters.Sort((x, y) => StringComparer.OrdinalIgnoreCase.Compare(x.Id, y.Id));
                loadedParts.Sort((x, y) => StringComparer.OrdinalIgnoreCase.Compare(x.Id, y.Id));
                loadedHitAreas.Sort((x, y) => StringComparer.OrdinalIgnoreCase.Compare(x.Id, y.Id));
                loadedParameterIds.Sort(StringComparer.OrdinalIgnoreCase);
                loadedPartIds.Sort(StringComparer.OrdinalIgnoreCase);
                var loadedLipSyncVowelParameters = ResolveLipSyncVowelParameterIds(loadedParameterMetadata.Values);

                return new ModelMetadataSnapshot(
                    modelPath,
                    loadedExpressions,
                    loadedMotions,
                    loadedParameters,
                    loadedParameterMetadata,
                    loadedParts,
                    loadedHitAreas,
                    loadedLayout,
                    loadedLipSyncVowelParameters,
                    loadedParameterIds,
                    loadedPartIds);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool TryGetLayoutValue(IReadOnlyDictionary<string, float> layout, string key, out float value)
        {
            if (layout.TryGetValue(key, out value))
            {
                return true;
            }

            value = 0.0f;
            return false;
        }
    }
}
