using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace TheResourceOfLong
{
    public static class ResourceManifestGenerator
    {
        private const string ManifestFileName = "res_manifest.json";
        private const string RawDirectoryName = "Raw";
        private const string MappingDirectoryName = "Mapping";
        private const string MappingRulesFileName = "MappingRules.csv";

        private static bool _initialized;

        public static void Initialize(string gameRoot, string modsOfLongRoot)
        {
            if (_initialized) return;
            _initialized = true;

            ResourceProbeConfig config = UserConfigManager.LoadOrCreate(gameRoot);
            if (config == null || !config.EnableResourceManifestGenerator) return;

            List<ModProjectInfo> projects = ModDiscovery.DiscoverProjects(modsOfLongRoot);
            int changedCount = 0;
            foreach (ModProjectInfo project in projects)
            {
                if (TryGenerate(project)) changedCount++;
            }

            LoggerManager.Info("Resource manifest generator finished. Updated file count: " + changedCount);
        }

        private static bool TryGenerate(ModProjectInfo project)
        {
            try
            {
                string manifestPath = Path.Combine(project.ResDirectoryPath, ManifestFileName);
                ManifestDocument document = ManifestDocument.LoadOrCreate(manifestPath);

                int addedCount = 0;
                addedCount += AddRawEntries(project, document);
                addedCount += AddMappingEntries(project, document);

                if (addedCount <= 0) return false;

                Directory.CreateDirectory(project.ResDirectoryPath);
                File.WriteAllText(manifestPath, document.ToJson(), new UTF8Encoding(false));
                LoggerManager.Info("Generated resource manifest entries: " + manifestPath + " added=" + addedCount);
                return true;
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("Failed to generate resource manifest for " + project.ModId + ": " + ex.Message);
                return false;
            }
        }

        private static int AddRawEntries(ModProjectInfo project, ManifestDocument document)
        {
            string rawRoot = Path.Combine(project.ResDirectoryPath, RawDirectoryName);
            if (!Directory.Exists(rawRoot)) return 0;

            int count = 0;
            string[] files = Directory.GetFiles(rawRoot, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
            {
                if (ShouldSkipFile(file)) continue;

                string relative = PathUtility.NormalizeResourcePath(PathUtility.GetRelativePath(rawRoot, file));
                if (string.IsNullOrWhiteSpace(relative)) continue;

                string virtualPath = PathUtility.RemoveExtensionFromResourcePath(relative);
                if (string.IsNullOrWhiteSpace(virtualPath) || document.ContainsResourcePath(virtualPath)) continue;

                GeneratedManifestEntry entry = CreateEntry(
                    virtualPath,
                    InferTypeName(relative),
                    RawDirectoryName + "/" + relative,
                    "TheResourceOfLong.RawScanner");

                document.AddResource(entry);
                count++;
            }

            return count;
        }

        private static int AddMappingEntries(ModProjectInfo project, ManifestDocument document)
        {
            string mappingRoot = Path.Combine(project.ResDirectoryPath, MappingDirectoryName);
            if (!Directory.Exists(mappingRoot)) return 0;

            int count = 0;
            string[] files = Directory.GetFiles(mappingRoot, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
            {
                string relative = PathUtility.NormalizeResourcePath(PathUtility.GetRelativePath(mappingRoot, file));
                if (ShouldSkipMappingFile(relative, file)) continue;

                string virtualPath = MappingDirectoryName + "/" + relative;
                if (document.ContainsResourcePath(virtualPath)) continue;

                GeneratedManifestEntry entry = CreateEntry(
                    virtualPath,
                    InferTypeName(relative),
                    MappingDirectoryName + "/" + relative,
                    "TheResourceOfLong.MappingScanner");

                document.AddResource(entry);
                count++;
            }

            return count;
        }

        private static GeneratedManifestEntry CreateEntry(string path, string type, string source, string generatedBy)
        {
            GeneratedManifestEntry entry = new GeneratedManifestEntry();
            entry.Path = PathUtility.NormalizeResourcePath(path);
            entry.Type = type;
            entry.Source = PathUtility.NormalizeResourcePath(source);
            entry.Mode = "replace";
            entry.GeneratedBy = generatedBy;

            if (IsImageSource(source))
            {
                entry.PixelsPerUnit = 100f;
                entry.PivotX = 0.5f;
                entry.PivotY = 0.5f;
            }

            return entry;
        }

        private static bool ShouldSkipMappingFile(string relativePath, string fullPath)
        {
            if (ShouldSkipFile(fullPath)) return true;
            if (string.IsNullOrWhiteSpace(relativePath)) return true;

            string fileName = Path.GetFileName(relativePath);
            return string.Equals(fileName, MappingRulesFileName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldSkipFile(string fullPath)
        {
            string fileName = Path.GetFileName(fullPath);
            if (string.IsNullOrEmpty(fileName)) return true;
            if (fileName.StartsWith("~", StringComparison.Ordinal)) return true;
            if (fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) return true;
            if (fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) return true;
            if (fileName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)) return true;

            FileAttributes attributes = File.GetAttributes(fullPath);
            return (attributes & FileAttributes.Directory) != 0;
        }

        private static string InferTypeName(string source)
        {
            string extension = Path.GetExtension(source).ToLowerInvariant();
            if (extension == ".csv" || extension == ".json" || extension == ".txt" || extension == ".bytes") return "TextAsset";
            if (extension == ".png" || extension == ".jpg" || extension == ".jpeg") return "Sprite";
            if (extension == ".wav" || extension == ".ogg") return "AudioClip";
            if (extension == ".prefab") return "GameObject";
            if (extension == ".mat") return "Material";
            if (extension == ".controller") return "RuntimeAnimatorController";
            if (extension == ".anim") return "AnimationClip";
            return "Object";
        }

        private static bool IsImageSource(string source)
        {
            string extension = Path.GetExtension(source).ToLowerInvariant();
            return extension == ".png" || extension == ".jpg" || extension == ".jpeg";
        }

        private sealed class ManifestDocument
        {
            private readonly List<Dictionary<string, object>> _pathTypeRules = new List<Dictionary<string, object>>();
            private readonly List<GeneratedManifestEntry> _resources = new List<GeneratedManifestEntry>();
            private readonly HashSet<string> _resourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public static ManifestDocument LoadOrCreate(string manifestPath)
            {
                ManifestDocument document = new ManifestDocument();
                if (!File.Exists(manifestPath)) return document;

                Dictionary<string, object> root = SimpleJson.ParseObject(File.ReadAllText(manifestPath));
                document.ReadPathTypeRules(root);
                document.ReadResources(root);
                return document;
            }

            public bool ContainsResourcePath(string path)
            {
                return _resourcePaths.Contains(PathUtility.NormalizeResourcePath(path));
            }

            public void AddResource(GeneratedManifestEntry entry)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Path)) return;

                entry.Path = PathUtility.NormalizeResourcePath(entry.Path);
                if (_resourcePaths.Contains(entry.Path)) return;

                _resourcePaths.Add(entry.Path);
                _resources.Add(entry);
            }

            public string ToJson()
            {
                _resources.Sort(delegate (GeneratedManifestEntry left, GeneratedManifestEntry right)
                {
                    return string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase);
                });

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("{");
                builder.AppendLine("  \"formatVersion\": 1,");

                if (_pathTypeRules.Count > 0)
                {
                    builder.AppendLine("  \"pathTypeRules\": [");
                    for (int i = 0; i < _pathTypeRules.Count; i++)
                    {
                        WriteObject(builder, _pathTypeRules[i], "    ");
                        builder.AppendLine(i < _pathTypeRules.Count - 1 ? "," : string.Empty);
                    }

                    builder.AppendLine("  ],");
                }

                builder.AppendLine("  \"resources\": [");
                for (int i = 0; i < _resources.Count; i++)
                {
                    WriteResource(builder, _resources[i], "    ");
                    builder.AppendLine(i < _resources.Count - 1 ? "," : string.Empty);
                }

                builder.AppendLine("  ]");
                builder.AppendLine("}");
                return builder.ToString();
            }

            private void ReadPathTypeRules(Dictionary<string, object> root)
            {
                object rulesObject;
                if (!SimpleJson.TryGetValueIgnoreCase(root, "pathTypeRules", out rulesObject)) return;

                object[] rules = rulesObject as object[];
                if (rules == null) return;

                foreach (object item in rules)
                {
                    Dictionary<string, object> rule = item as Dictionary<string, object>;
                    if (rule == null) continue;
                    _pathTypeRules.Add(rule);
                }
            }

            private void ReadResources(Dictionary<string, object> root)
            {
                object resourcesObject;
                if (!SimpleJson.TryGetValueIgnoreCase(root, "resources", out resourcesObject)) return;

                object[] resources = resourcesObject as object[];
                if (resources == null) return;

                foreach (object item in resources)
                {
                    Dictionary<string, object> raw = item as Dictionary<string, object>;
                    if (raw == null) continue;

                    GeneratedManifestEntry entry = GeneratedManifestEntry.FromDictionary(raw);
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Path)) continue;

                    entry.Path = PathUtility.NormalizeResourcePath(entry.Path);
                    if (_resourcePaths.Contains(entry.Path)) continue;

                    _resourcePaths.Add(entry.Path);
                    _resources.Add(entry);
                }
            }
        }

        private sealed class GeneratedManifestEntry
        {
            public string Path;
            public string Type;
            public string Source;
            public string Mode;
            public float? PixelsPerUnit;
            public float? PivotX;
            public float? PivotY;
            public string GeneratedBy;
            public Dictionary<string, object> Raw;

            public static GeneratedManifestEntry FromDictionary(Dictionary<string, object> raw)
            {
                GeneratedManifestEntry entry = new GeneratedManifestEntry();
                entry.Raw = raw;
                entry.Path = SimpleJson.GetString(raw, "path");
                entry.Type = SimpleJson.GetString(raw, "type");
                entry.Source = SimpleJson.GetString(raw, "source");
                entry.Mode = SimpleJson.GetString(raw, "mode");
                entry.PixelsPerUnit = SimpleJson.GetNullableFloat(raw, "pixelsPerUnit");
                entry.PivotX = SimpleJson.GetNullableFloat(raw, "pivotX");
                entry.PivotY = SimpleJson.GetNullableFloat(raw, "pivotY");
                entry.GeneratedBy = SimpleJson.GetString(raw, "generatedBy");
                return entry;
            }
        }

        private static void WriteResource(StringBuilder builder, GeneratedManifestEntry entry, string indent)
        {
            if (entry.Raw != null)
            {
                WriteObject(builder, entry.Raw, indent);
                return;
            }

            builder.AppendLine(indent + "{");
            WriteStringProperty(builder, "path", entry.Path, indent + "  ", true);
            WriteStringProperty(builder, "type", entry.Type, indent + "  ", true);
            WriteStringProperty(builder, "source", entry.Source, indent + "  ", true);
            WriteStringProperty(builder, "mode", string.IsNullOrWhiteSpace(entry.Mode) ? "replace" : entry.Mode, indent + "  ", entry.PixelsPerUnit.HasValue || entry.PivotX.HasValue || entry.PivotY.HasValue || !string.IsNullOrWhiteSpace(entry.GeneratedBy));

            if (entry.PixelsPerUnit.HasValue)
            {
                WriteFloatProperty(builder, "pixelsPerUnit", entry.PixelsPerUnit.Value, indent + "  ", entry.PivotX.HasValue || entry.PivotY.HasValue || !string.IsNullOrWhiteSpace(entry.GeneratedBy));
            }

            if (entry.PivotX.HasValue)
            {
                WriteFloatProperty(builder, "pivotX", entry.PivotX.Value, indent + "  ", entry.PivotY.HasValue || !string.IsNullOrWhiteSpace(entry.GeneratedBy));
            }

            if (entry.PivotY.HasValue)
            {
                WriteFloatProperty(builder, "pivotY", entry.PivotY.Value, indent + "  ", !string.IsNullOrWhiteSpace(entry.GeneratedBy));
            }

            if (!string.IsNullOrWhiteSpace(entry.GeneratedBy))
            {
                WriteStringProperty(builder, "generatedBy", entry.GeneratedBy, indent + "  ", false);
            }

            builder.Append(indent + "}");
        }

        private static void WriteObject(StringBuilder builder, Dictionary<string, object> values, string indent)
        {
            builder.AppendLine(indent + "{");

            int index = 0;
            foreach (KeyValuePair<string, object> pair in values)
            {
                WriteRawProperty(builder, pair.Key, pair.Value, indent + "  ", index < values.Count - 1);
                index++;
            }

            builder.Append(indent + "}");
        }

        private static void WriteStringProperty(StringBuilder builder, string key, string value, string indent, bool comma)
        {
            builder.Append(indent);
            builder.Append("\"");
            builder.Append(EscapeJson(key));
            builder.Append("\": \"");
            builder.Append(EscapeJson(value ?? string.Empty));
            builder.Append("\"");
            if (comma) builder.Append(",");
            builder.AppendLine();
        }

        private static void WriteFloatProperty(StringBuilder builder, string key, float value, string indent, bool comma)
        {
            builder.Append(indent);
            builder.Append("\"");
            builder.Append(EscapeJson(key));
            builder.Append("\": ");
            builder.Append(value.ToString("0.####", CultureInfo.InvariantCulture));
            if (comma) builder.Append(",");
            builder.AppendLine();
        }

        private static void WriteRawProperty(StringBuilder builder, string key, object value, string indent, bool comma)
        {
            builder.Append(indent);
            builder.Append("\"");
            builder.Append(EscapeJson(key));
            builder.Append("\": ");
            WriteRawValue(builder, value);
            if (comma) builder.Append(",");
            builder.AppendLine();
        }

        private static void WriteRawValue(StringBuilder builder, object value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            if (value is bool)
            {
                builder.Append((bool)value ? "true" : "false");
                return;
            }

            if (value is int || value is long || value is float || value is double || value is decimal)
            {
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            builder.Append("\"");
            builder.Append(EscapeJson(Convert.ToString(value, CultureInfo.InvariantCulture)));
            builder.Append("\"");
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
