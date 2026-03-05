using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace VTuberKitForYMM4.Plugin
{
    public static class ModelMetadataCatalog
    {
        private static readonly object LockObj = new();
        private static string currentModelPath = string.Empty;
        private static List<string> expressions = [];
        private static List<(string Group, int Index, string FileName)> motions = [];
        private static List<(string Id, string Name)> parameters = [];
        private static List<(string Id, string Name)> parts = [];
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

        public static void UpdateFromModelPath(string? modelPath)
        {
            lock (LockObj)
            {
                if (string.IsNullOrWhiteSpace(modelPath) || 
                    !modelPath.EndsWith(".model3.json", StringComparison.OrdinalIgnoreCase) || 
                    !File.Exists(modelPath))
                {
                    currentModelPath = string.Empty;
                    expressions = [];
                    motions = [];
                    parameters = [];
                    parts = [];
                    parameterIds = [];
                    partIds = [];
                    return;
                }

                if (string.Equals(currentModelPath, modelPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                currentModelPath = modelPath;
                expressions = [];
                motions = [];
                parameters = [];
                parts = [];
                parameterIds = [];
                partIds = [];

                try
                {
                    var root = JObject.Parse(File.ReadAllText(modelPath));
                    var refs = root["FileReferences"] as JObject;
                    if (refs == null)
                    {
                        return;
                    }

                    if (refs["Expressions"] is JArray exprArray)
                    {
                        foreach (var expr in exprArray)
                        {
                            var name = expr?["Name"]?.ToString();
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                expressions.Add(name);
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
                                motions.Add((group, i, fileName));
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
                                        parameters.Add((id, name));
                                        parameterIds.Add(id);
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
                                        parts.Add((id, name));
                                        partIds.Add(id);
                                    }
                                }
                            }
                        }
                        else
                        {
                            Commons.ConsoleManager.Debug($"DisplayInfo not found: {displayInfoPath}");
                        }
                    }
                    else
                    {
                        Commons.ConsoleManager.Debug("DisplayInfo entry is missing in model3.json FileReferences.");
                    }

                    parameters.Sort((x, y) => StringComparer.OrdinalIgnoreCase.Compare(x.Id, y.Id));
                    parts.Sort((x, y) => StringComparer.OrdinalIgnoreCase.Compare(x.Id, y.Id));
                    parameterIds.Sort(StringComparer.OrdinalIgnoreCase);
                    partIds.Sort(StringComparer.OrdinalIgnoreCase);

                    Commons.ConsoleManager.Debug($"Model metadata loaded: expressions={expressions.Count}, motions={motions.Count}, parameters={parameterIds.Count}, parts={partIds.Count}");
                }
                catch (Exception ex)
                {
                    expressions = [];
                    motions = [];
                    parameters = [];
                    parts = [];
                    parameterIds = [];
                    partIds = [];
                    Commons.ConsoleManager.Debug($"Model metadata load failed: {ex.Message}");
                }
            }
        }
    }
}
