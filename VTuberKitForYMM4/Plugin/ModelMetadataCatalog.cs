using System.IO;
using System.Globalization;
using Newtonsoft.Json.Linq;
using VTuberKitForNative;

namespace VTuberKitForYMM4.Plugin
{
    public static class ModelMetadataCatalog
    {
        public readonly record struct ParameterMetadata(string Id, string Name, float Default, float Min, float Max);
        public readonly record struct ModelSelectionResolution(
            string ResolvedModelPath,
            bool IsValid,
            bool SelectionWasDirectory,
            bool MultipleCandidatesFound);
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
        private sealed record SnapshotCacheEntry(ModelMetadataSnapshot Snapshot, bool NativeRefreshAttempted);
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
        private static readonly Dictionary<string, SnapshotCacheEntry> SnapshotCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> ShownSelectionIssueKeys = new(StringComparer.Ordinal);
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

        public static bool TryGetParameterMetadata(string? modelPath, string? id, out ParameterMetadata metadata)
        {
            lock (LockObj)
            {
                var snapshot = GetSnapshotCore(modelPath);
                if (snapshot != null &&
                    !string.IsNullOrWhiteSpace(id) &&
                    snapshot.ParameterMetadataById.TryGetValue(id, out metadata))
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

        public static LipSyncVowelParameterIds GetLipSyncVowelParameters(string? modelPath)
        {
            lock (LockObj)
            {
                return GetSnapshotCore(modelPath)?.LipSyncVowelParameters ?? default;
            }
        }

        public static void UpdateFromModelPath(string? modelPath)
        {
            lock (LockObj)
            {
                var resolvedModelPath = ResolveModelSelection(modelPath).ResolvedModelPath;
                var snapshot = GetSnapshotCore(resolvedModelPath);
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
                return GetSnapshotCore(ResolveModelSelection(modelPath).ResolvedModelPath)?.Expressions ?? [];
            }
        }

        public static string CurrentModelPath
        {
            get
            {
                lock (LockObj)
                {
                    return currentModelPath;
                }
            }
        }

        public static IReadOnlyList<(string Group, int Index, string FileName)> GetMotions(string? modelPath)
        {
            lock (LockObj)
            {
                return GetSnapshotCore(ResolveModelSelection(modelPath).ResolvedModelPath)?.Motions ?? [];
            }
        }

        public static IReadOnlyList<(string Id, string Name)> GetHitAreas(string? modelPath)
        {
            lock (LockObj)
            {
                return GetSnapshotCore(ResolveModelSelection(modelPath).ResolvedModelPath)?.HitAreas ?? [];
            }
        }

        public static IReadOnlyList<(string Id, string Name)> GetParameters(string? modelPath)
        {
            lock (LockObj)
            {
                return GetSnapshotCore(ResolveModelSelection(modelPath).ResolvedModelPath)?.Parameters ?? [];
            }
        }

        public static IReadOnlyList<(string Id, string Name)> GetParts(string? modelPath)
        {
            lock (LockObj)
            {
                return GetSnapshotCore(ResolveModelSelection(modelPath).ResolvedModelPath)?.Parts ?? [];
            }
        }

        public static ModelSelectionResolution ResolveModelSelection(string? selection)
        {
            if (string.IsNullOrWhiteSpace(selection))
            {
                return new ModelSelectionResolution(string.Empty, false, false, false);
            }

            try
            {
                if (Directory.Exists(selection))
                {
                    var candidates = Directory
                        .EnumerateFiles(selection, "*.model3.json", SearchOption.TopDirectoryOnly)
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    if (candidates.Length == 0)
                    {
                        return new ModelSelectionResolution(string.Empty, false, true, false);
                    }

                    return new ModelSelectionResolution(
                        Path.GetFullPath(candidates[0]),
                        true,
                        true,
                        candidates.Length > 1);
                }

                if (File.Exists(selection) &&
                    selection.EndsWith(".model3.json", StringComparison.OrdinalIgnoreCase))
                {
                    return new ModelSelectionResolution(Path.GetFullPath(selection), true, false, false);
                }
            }
            catch (Exception ex)
            {
                LogMetadataError(nameof(ResolveModelSelection), selection, ex);
            }

            return new ModelSelectionResolution(string.Empty, false, false, false);
        }

        public static IReadOnlyList<string> GetModelWarnings(string? modelPath)
        {
            var resolved = ResolveModelSelection(modelPath);
            if (!resolved.IsValid || string.IsNullOrWhiteSpace(resolved.ResolvedModelPath))
            {
                return [];
            }

            var warnings = new List<string>();
            if (resolved.SelectionWasDirectory && resolved.MultipleCandidatesFound)
            {
                warnings.Add(Translate.Warning_MultipleModel3Json);
            }

            try
            {
                var root = JObject.Parse(File.ReadAllText(resolved.ResolvedModelPath));
                var refs = root["FileReferences"] as JObject;
                if (refs == null)
                {
                    warnings.Add(Translate.Warning_FileReferencesMissing);
                    return warnings;
                }

                var modelDirectory = Path.GetDirectoryName(resolved.ResolvedModelPath) ?? string.Empty;
                if (refs["Moc"] is not JValue mocValue || string.IsNullOrWhiteSpace(mocValue.ToString()))
                {
                    warnings.Add(Translate.Warning_MocMissing);
                }
                else if (!File.Exists(Path.GetFullPath(Path.Combine(modelDirectory, mocValue.ToString()))))
                {
                    warnings.Add(string.Format(Translate.Warning_MocFileMissing, mocValue));
                }

                if (refs["Textures"] is JArray textures)
                {
                    foreach (var texture in textures.Select(x => x?.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)))
                    {
                        var texturePath = Path.GetFullPath(Path.Combine(modelDirectory, texture!));
                        if (!File.Exists(texturePath))
                        {
                            warnings.Add(string.Format(Translate.Warning_TextureFileMissing, texture));
                        }
                    }
                }

                if (refs["Expressions"] is JArray expressionsArray)
                {
                    foreach (var expressionEntry in expressionsArray.OfType<JObject>())
                    {
                        var expressionName = expressionEntry["Name"]?.ToString();
                        var expressionFile = expressionEntry["File"]?.ToString();
                        if (string.IsNullOrWhiteSpace(expressionFile))
                        {
                            warnings.Add(string.Format(Translate.Warning_ExpressionFileSettingMissing, expressionName ?? Translate.Warning_NameUnset));
                            continue;
                        }

                        var expressionPath = Path.GetFullPath(Path.Combine(modelDirectory, expressionFile));
                        if (!File.Exists(expressionPath))
                        {
                            warnings.Add(string.Format(Translate.Warning_ExpressionFileMissing, expressionFile));
                        }
                    }
                }

                if (refs["Motions"] is JObject motionsObject)
                {
                    foreach (var group in motionsObject.Properties())
                    {
                        if (group.Value is not JArray motionArray)
                        {
                            continue;
                        }

                        foreach (var motionEntry in motionArray.OfType<JObject>())
                        {
                            var motionFile = motionEntry["File"]?.ToString();
                            if (string.IsNullOrWhiteSpace(motionFile))
                            {
                                warnings.Add(string.Format(Translate.Warning_MotionFileSettingMissing, group.Name));
                                continue;
                            }

                            var motionPath = Path.GetFullPath(Path.Combine(modelDirectory, motionFile));
                            if (!File.Exists(motionPath))
                            {
                                warnings.Add(string.Format(Translate.Warning_MotionFileMissing, motionFile));
                            }
                        }
                    }
                }

                if (root["HitAreas"] is JArray hitAreasArray)
                {
                    foreach (var hitArea in hitAreasArray.OfType<JObject>())
                    {
                        var hitAreaId = hitArea["Id"]?.ToString();
                        if (string.IsNullOrWhiteSpace(hitAreaId))
                        {
                            warnings.Add(Translate.Warning_HitAreaIdMissing);
                        }
                    }
                }

                if (refs["Physics"] is JValue physicsValue && !string.IsNullOrWhiteSpace(physicsValue.ToString()))
                {
                    var physicsPath = Path.GetFullPath(Path.Combine(modelDirectory, physicsValue.ToString()));
                    if (!File.Exists(physicsPath))
                    {
                        warnings.Add(string.Format(Translate.Warning_PhysicsFileMissing, physicsValue));
                    }
                }

                if (refs["DisplayInfo"] is JValue displayInfoValue && !string.IsNullOrWhiteSpace(displayInfoValue.ToString()))
                {
                    var displayInfoPath = Path.GetFullPath(Path.Combine(modelDirectory, displayInfoValue.ToString()));
                    if (!File.Exists(displayInfoPath))
                    {
                        warnings.Add(string.Format(Translate.Warning_DisplayInfoFileMissing, displayInfoValue));
                    }
                }
            }
            catch (Exception ex)
            {
                LogMetadataError(nameof(GetModelWarnings), resolved.ResolvedModelPath, ex);
                warnings.Add(Translate.Warning_ModelParseException);
            }

            return warnings.Distinct(StringComparer.Ordinal).ToArray();
        }

        public static IReadOnlyList<string> GetModelSelectionWarnings(string? modelPath)
        {
            var warnings = GetModelWarnings(modelPath).ToList();
            var resolved = ResolveModelSelection(modelPath);
            if (!resolved.IsValid || string.IsNullOrWhiteSpace(resolved.ResolvedModelPath))
            {
                return warnings;
            }

            var loadError = ProbeModelLoadError(resolved.ResolvedModelPath);
            if (!string.IsNullOrWhiteSpace(loadError))
            {
                warnings.Add(loadError);
            }

            return warnings
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        public static void RememberShownSelectionIssue(string? modelPath, string detail)
        {
            var key = CreateIssueKey(modelPath, detail);
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            lock (LockObj)
            {
                ShownSelectionIssueKeys.Add(key);
            }
        }

        public static bool WasSelectionIssueShown(string? modelPath, string detail)
        {
            var key = CreateIssueKey(modelPath, detail);
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            lock (LockObj)
            {
                return ShownSelectionIssueKeys.Contains(key);
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
            var initializedManager = false;
            try
            {
                var manager = Live2DManager.GetInstance();
                manager?.Initialize();
                initializedManager = manager != null;
                if (manager == null)
                {
                    return loadedParameters;
                }

                if (!manager.HasD3D11Device())
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
            catch (Exception ex)
            {
                LogMetadataError(nameof(LoadNativeParameterMetadata), modelPath, ex);
            }
            finally
            {
                if (initializedManager)
                {
                    try
                    {
                        Live2DManager.GetInstance()?.Release();
                    }
                    catch (Exception ex)
                    {
                        LogMetadataError(nameof(LoadNativeParameterMetadata), $"{modelPath} (release)", ex);
                    }
                }
            }

            return loadedParameters;
        }

        private static string ProbeModelLoadError(string modelPath)
        {
            var initializedManager = false;
            try
            {
                var manager = Live2DManager.GetInstance();
                manager?.Initialize();
                initializedManager = manager != null;
                if (manager == null)
                {
                    return string.Empty;
                }

                if (!manager.HasD3D11Device())
                {
                    return string.Empty;
                }

                var model = manager.CreateModel();
                if (model == null)
                {
                    return string.Empty;
                }

                using (model)
                {
                    if (model.LoadModel(modelPath))
                    {
                        return string.Empty;
                    }

                    return model.LastErrorMessage ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                LogMetadataError(nameof(ProbeModelLoadError), modelPath, ex);
                return string.Empty;
            }
            finally
            {
                if (initializedManager)
                {
                    try
                    {
                        Live2DManager.GetInstance()?.Release();
                    }
                    catch (Exception ex)
                    {
                        LogMetadataError(nameof(ProbeModelLoadError), $"{modelPath} (release)", ex);
                    }
                }
            }
        }

        private static string CreateIssueKey(string? modelPath, string? detail)
        {
            var safePath = modelPath?.Trim();
            var safeDetail = detail?.Trim();
            if (string.IsNullOrWhiteSpace(safePath) || string.IsNullOrWhiteSpace(safeDetail))
            {
                return string.Empty;
            }

            return $"{safePath}|{safeDetail}";
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
            if (SnapshotCache.TryGetValue(normalizedPath, out var cached))
            {
                if (!cached.NativeRefreshAttempted && CanLoadNativeParameterMetadata())
                {
                    var refreshed = LoadSnapshot(normalizedPath);
                    if (refreshed != null)
                    {
                        SnapshotCache[normalizedPath] = new SnapshotCacheEntry(refreshed, true);
                        return refreshed;
                    }

                    SnapshotCache[normalizedPath] = cached with { NativeRefreshAttempted = true };
                }

                return cached.Snapshot;
            }

            var nativeRefreshAttempted = CanLoadNativeParameterMetadata();
            var snapshot = LoadSnapshot(normalizedPath);
            if (snapshot != null)
            {
                SnapshotCache[normalizedPath] = new SnapshotCacheEntry(snapshot, nativeRefreshAttempted);
            }

            return snapshot;
        }

        private static bool CanLoadNativeParameterMetadata()
        {
            try
            {
                var manager = Live2DManager.GetInstance();
                return manager != null && manager.HasD3D11Device();
            }
            catch (Exception ex)
            {
                LogMetadataError(nameof(CanLoadNativeParameterMetadata), "D3D11 device availability", ex);
                return false;
            }
        }

        private static ModelMetadataSnapshot? LoadSnapshot(string modelPath)
        {
            var loadedExpressions = new List<string>();
            var loadedMotions = new List<(string Group, int Index, string FileName)>();
            var loadedParametersById = new Dictionary<string, (string Id, string Name)>(StringComparer.OrdinalIgnoreCase);
            var loadedParameterMetadata = new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase);
            var loadedParts = new List<(string Id, string Name)>();
            var loadedHitAreas = new List<(string Id, string Name)>();
            var loadedLayout = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            var loadedParameterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                                    loadedParametersById[id] = (id, name);
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

                foreach (var id in loadedParameterMetadata.Keys)
                {
                    loadedParameterIds.Add(id);
                }

                var mergedParameters = loadedParameterIds
                    .Select(id =>
                    {
                        loadedParametersById.TryGetValue(id, out var displayInfo);
                        var displayName = displayInfo.Name;

                        if (loadedParameterMetadata.TryGetValue(id, out var metadata))
                        {
                            var mergedName = string.IsNullOrWhiteSpace(metadata.Name) ? displayName : metadata.Name;
                            var mergedMetadata = metadata with { Name = mergedName ?? string.Empty };
                            loadedParameterMetadata[id] = mergedMetadata;
                            return (Parameter: (Id: id, Name: mergedName ?? string.Empty), Metadata: mergedMetadata);
                        }

                        var synthesizedMetadata = new ParameterMetadata(id, displayName ?? string.Empty, 0.0f, -100.0f, 100.0f);
                        loadedParameterMetadata[id] = synthesizedMetadata;
                        return (Parameter: (Id: id, Name: displayName ?? string.Empty), Metadata: synthesizedMetadata);
                    })
                    .OrderBy(x => x.Parameter.Id, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                var loadedParameters = mergedParameters
                    .Select(x => (x.Parameter.Id, x.Parameter.Name))
                    .ToList();
                loadedParts.Sort((x, y) => StringComparer.OrdinalIgnoreCase.Compare(x.Id, y.Id));
                loadedHitAreas.Sort((x, y) => StringComparer.OrdinalIgnoreCase.Compare(x.Id, y.Id));
                var sortedParameterIds = loadedParameterIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
                loadedPartIds.Sort(StringComparer.OrdinalIgnoreCase);
                var loadedLipSyncVowelParameters = ResolveLipSyncVowelParameterIds(mergedParameters.Select(x => x.Metadata));

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
                    sortedParameterIds,
                    loadedPartIds);
            }
            catch (Exception ex)
            {
                LogMetadataError(nameof(LoadSnapshot), modelPath, ex);
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

        private static void LogMetadataError(string methodName, string context, Exception exception)
        {
            Commons.ConsoleManager.Error(string.Format(
                Translate.Log_ModelMetadataCatalogFailed,
                methodName,
                context,
                exception));
        }
    }
}
