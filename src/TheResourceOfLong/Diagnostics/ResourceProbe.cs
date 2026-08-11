using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheResourceOfLong
{
    public static class ResourceProbe
    {
        private static readonly object SyncRoot = new object();
        private static ResourceProbeConfig _config = ResourceProbeConfig.CreateDefault();
        private static string _directoryPath;
        private static string _configPath;
        private static string _logPath;
        private static string _uniqueLogPath;
        private static bool _initialized;
        private static bool _warnedLogWriteFailure;
        private static bool _warnedUniqueLogWriteFailure;
        private static readonly Dictionary<string, UniqueResourceRow> UniqueRows = new Dictionary<string, UniqueResourceRow>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> DumpRelativePathsBySourcePath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static bool IsEnabled
        {
            get { return _initialized && _config != null && _config.EnableResourceProbe; }
        }

        public static void Initialize(string gameRoot)
        {
            if (_initialized) return;
            _initialized = true;

            string safeGameRoot = string.IsNullOrWhiteSpace(gameRoot) ? Directory.GetCurrentDirectory() : gameRoot;
            _directoryPath = UserConfigManager.GetConfigDirectoryPath(safeGameRoot);
            _configPath = UserConfigManager.GetConfigPath(safeGameRoot);
            _logPath = Path.Combine(_directoryPath, "resource-load-log.csv");
            _uniqueLogPath = Path.Combine(_directoryPath, "resource-load-log.unique-paths.csv");

            Directory.CreateDirectory(_directoryPath);
            _config = UserConfigManager.LoadOrCreate(safeGameRoot);
            LoadDumpManifest(safeGameRoot);
            EnsureLogHeader();
            EnsureUniqueLogHeader();

            LoggerManager.Info("Resource probe config: " + _configPath);
            if (IsEnabled)
            {
                LoggerManager.Info("Resource probe enabled. Log path: " + _logPath);
                LoggerManager.Info("Resource probe unique path summary: " + _uniqueLogPath);
            }
        }

        public static ResourceProbeState CreateState()
        {
            return new ResourceProbeState();
        }

        public static void MarkHandled(ResourceProbeState state, string path, string outcome)
        {
            if (state == null) return;

            state.HandledByMod = true;
            state.Outcome = outcome;

            ModResourceEntry entry;
            if (ModResourceRegistry.TryGetEntry(path, out entry))
            {
                state.ModId = entry.ModId;
                state.SourceKind = entry.SourceKind.ToString();
                state.Source = entry.Source;
            }
        }

        public static void LogLoad(string api, string path, Type requestedType, UnityEngine.Object result, ResourceProbeState state)
        {
            if (!IsEnabled) return;
            if (api != null && api.IndexOf("LoadAll", StringComparison.OrdinalIgnoreCase) >= 0 && !_config.LogLoadAll) return;

            string outcome = "OriginalHit";
            string modId = string.Empty;
            string sourceKind = string.Empty;
            string source = string.Empty;

            if (state != null && state.HandledByMod)
            {
                outcome = string.IsNullOrEmpty(state.Outcome) ? "ModHit" : state.Outcome;
                modId = state.ModId ?? string.Empty;
                sourceKind = state.SourceKind ?? string.Empty;
                source = state.Source ?? string.Empty;
            }
            else if (result == null)
            {
                outcome = "Miss";
            }
            else
            {
                ModResourceEntry entry;
                if (ModResourceRegistry.TryGetEntry(path, out entry))
                {
                    outcome = "OriginalHitWithRegisteredTextPatch";
                    modId = entry.ModId;
                    sourceKind = entry.SourceKind.ToString();
                    source = entry.Source;
                }
            }

            if (outcome == "Miss" && !_config.LogMisses) return;

            string requestedTypeName = FormatType(requestedType);
            string resultType = FormatResultType(result);
            string effectiveType = InferEffectiveType(path, requestedTypeName, resultType);
            string assetName = GetAssetName(result);
            WriteRow(api, path, requestedTypeName, resultType, effectiveType, assetName, outcome, modId, sourceKind, source);
        }

        public static void LogLoadAll(string api, string path, Type requestedType, int resultCount, ResourceProbeState state)
        {
            if (!IsEnabled || !_config.LogLoadAll) return;

            string outcome = resultCount > 0 ? "OriginalHit" : "Miss";
            string modId = string.Empty;
            string sourceKind = string.Empty;
            string source = string.Empty;

            if (state != null && state.HandledByMod)
            {
                outcome = string.IsNullOrEmpty(state.Outcome) ? "ModHit" : state.Outcome;
                modId = state.ModId ?? string.Empty;
                sourceKind = state.SourceKind ?? string.Empty;
                source = state.Source ?? string.Empty;
            }

            if (outcome == "Miss" && !_config.LogMisses) return;

            string requestedTypeName = FormatType(requestedType);
            string effectiveType = InferEffectiveType(path, requestedTypeName, string.Empty);
            WriteRow(api, path, requestedTypeName, "Count=" + resultCount, effectiveType, string.Empty, outcome, modId, sourceKind, source);
        }

        private static void EnsureLogHeader()
        {
            if (File.Exists(_logPath) && new FileInfo(_logPath).Length > 0) return;

            string header = "Timestamp,Scene,Api,Path,RequestedType,ResultType,Outcome,ModId,SourceKind,Source,StackTrace";
            File.WriteAllText(_logPath, header + Environment.NewLine, Encoding.UTF8);
        }

        private static void EnsureUniqueLogHeader()
        {
            if (File.Exists(_uniqueLogPath) && new FileInfo(_uniqueLogPath).Length > 0) return;

            File.WriteAllText(_uniqueLogPath, GetUniqueLogHeader() + Environment.NewLine, Encoding.UTF8);
        }

        private static void WriteRow(string api, string path, string requestedType, string resultType, string effectiveType, string assetName, string outcome, string modId, string sourceKind, string source)
        {
            string stackTrace = string.Empty;
            if (_config.LogStackTrace)
            {
                stackTrace = Environment.StackTrace ?? string.Empty;
                if (_config.MaxStackTraceLength > 0 && stackTrace.Length > _config.MaxStackTraceLength)
                {
                    stackTrace = stackTrace.Substring(0, _config.MaxStackTraceLength);
                }
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string scene = GetSceneName();

            string row = string.Join(",",
                Csv(timestamp),
                Csv(scene),
                Csv(api),
                Csv(path),
                Csv(requestedType),
                Csv(resultType),
                Csv(outcome),
                Csv(modId),
                Csv(sourceKind),
                Csv(source),
                Csv(stackTrace));

            lock (SyncRoot)
            {
                UpdateUniqueRows(timestamp, scene, path, requestedType, resultType, effectiveType, assetName, outcome, sourceKind, source);
                AppendMainLogRow(row);
                WriteUniqueRows();
            }
        }

        private static void AppendMainLogRow(string row)
        {
            try
            {
                File.AppendAllText(_logPath, row + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                if (_warnedLogWriteFailure) return;
                _warnedLogWriteFailure = true;
                LoggerManager.Warning("Failed to write resource probe log. Close the CSV if it is open in another program: " + ex.Message);
            }
        }

        private static void UpdateUniqueRows(string timestamp, string scene, string path, string requestedType, string resultType, string effectiveType, string assetName, string outcome, string sourceKind, string source)
        {
            string normalizedPath = PathUtility.NormalizeResourcePath(path);
            string key = normalizedPath + "|" + requestedType;

            UniqueResourceRow row;
            if (!UniqueRows.TryGetValue(key, out row))
            {
                row = new UniqueResourceRow();
                row.Path = normalizedPath;
                row.RequestedType = requestedType ?? string.Empty;
                row.FirstSeen = timestamp;
                UniqueRows[key] = row;
            }

            row.HitCount++;
            row.LastSeen = timestamp;
            row.LastScene = scene ?? string.Empty;
            row.ResultType = ChooseNonEmpty(resultType, row.ResultType);
            row.EffectiveType = ChooseNonEmpty(effectiveType, row.EffectiveType);
            row.AssetName = ChooseNonEmpty(assetName, row.AssetName);
            row.Outcome = outcome ?? string.Empty;
            row.SourceKind = sourceKind ?? string.Empty;
            row.Source = source ?? string.Empty;
            row.SuggestedExtensions = GetSuggestedExtensions(row.Path, row.RequestedType, row.ResultType, row.EffectiveType);
            row.RawOverrideCandidates = GetRawOverrideCandidates(row.Path, row.SuggestedExtensions);
            row.ManifestType = GetManifestType(row.EffectiveType);
        }

        private static void WriteUniqueRows()
        {
            List<UniqueResourceRow> rows = new List<UniqueResourceRow>(UniqueRows.Values);
            rows.Sort(delegate(UniqueResourceRow left, UniqueResourceRow right)
            {
                int pathCompare = string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase);
                if (pathCompare != 0) return pathCompare;
                return string.Compare(left.RequestedType, right.RequestedType, StringComparison.OrdinalIgnoreCase);
            });

            StringBuilder builder = new StringBuilder();
            builder.AppendLine(GetUniqueLogHeader());
            foreach (UniqueResourceRow row in rows)
            {
                builder.AppendLine(string.Join(",",
                    Csv(row.Path),
                    Csv(row.RequestedType),
                    Csv(row.EffectiveType),
                    Csv(row.ResultType),
                    Csv(row.AssetName),
                    Csv(row.SuggestedExtensions),
                    Csv(row.RawOverrideCandidates),
                    Csv(row.ManifestType),
                    Csv(row.Outcome),
                    Csv(row.HitCount.ToString()),
                    Csv(row.FirstSeen),
                    Csv(row.LastSeen),
                    Csv(row.LastScene),
                    Csv(row.SourceKind),
                    Csv(row.Source)));
            }

            try
            {
                File.WriteAllText(_uniqueLogPath, builder.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                string fallbackPath = Path.Combine(
                    Path.GetDirectoryName(_uniqueLogPath),
                    Path.GetFileNameWithoutExtension(_uniqueLogPath) + ".new.csv");

                try
                {
                    File.WriteAllText(fallbackPath, builder.ToString(), Encoding.UTF8);
                }
                catch
                {
                    if (_warnedUniqueLogWriteFailure) return;
                    _warnedUniqueLogWriteFailure = true;
                    LoggerManager.Warning("Failed to write resource probe unique path summary. Close the CSV if it is open in another program: " + ex.Message);
                    return;
                }

                if (_warnedUniqueLogWriteFailure) return;
                _warnedUniqueLogWriteFailure = true;
                LoggerManager.Warning("Resource probe unique path summary is locked. Wrote fallback file: " + fallbackPath);
            }
        }

        private static string GetUniqueLogHeader()
        {
            return "Path,RequestedType,EffectiveType,ResultType,AssetName,SuggestedExtensions,RawOverrideCandidates,ManifestType,Outcome,HitCount,FirstSeen,LastSeen,LastScene,SourceKind,Source";
        }

        private static string GetSceneName()
        {
            try
            {
                return SceneManager.GetActiveScene().name;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string FormatType(Type type)
        {
            return type == null ? string.Empty : type.FullName;
        }

        private static string FormatResultType(UnityEngine.Object result)
        {
            if (result == null) return string.Empty;

            Type knownType = GetKnownUnityRuntimeType(result);
            if (knownType != null) return knownType.FullName;

            Type resultType = result.GetType();
            return resultType == null ? string.Empty : resultType.FullName;
        }

        private static Type GetKnownUnityRuntimeType(UnityEngine.Object result)
        {
            if (result is TextAsset) return typeof(TextAsset);
            if (result is Sprite) return typeof(Sprite);
            if (result is Texture2D) return typeof(Texture2D);
            if (result is Texture) return typeof(Texture);
            if (result is AudioClip) return typeof(AudioClip);
            if (result is GameObject) return typeof(GameObject);
            if (result is Material) return typeof(Material);
            if (result is Shader) return typeof(Shader);
            if (result is AnimationClip) return typeof(AnimationClip);
            if (result is RuntimeAnimatorController) return typeof(RuntimeAnimatorController);
            return null;
        }

        private static string GetAssetName(UnityEngine.Object result)
        {
            if (result == null) return string.Empty;

            try
            {
                return result.name ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string InferEffectiveType(string path, string requestedType, string resultType)
        {
            if (!string.IsNullOrEmpty(requestedType) &&
                !string.Equals(requestedType, typeof(UnityEngine.Object).FullName, StringComparison.OrdinalIgnoreCase))
            {
                return requestedType;
            }

            if (!string.IsNullOrEmpty(resultType) &&
                !string.Equals(resultType, typeof(UnityEngine.Object).FullName, StringComparison.OrdinalIgnoreCase) &&
                resultType.IndexOf("Count=", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return resultType;
            }

            string normalizedPath = PathUtility.NormalizeResourcePath(path);
            if (normalizedPath.StartsWith("GameData/", StringComparison.OrdinalIgnoreCase)) return typeof(TextAsset).FullName;
            if (normalizedPath.StartsWith("Sound/", StringComparison.OrdinalIgnoreCase)) return typeof(AudioClip).FullName;
            if (normalizedPath.StartsWith("Textures/", StringComparison.OrdinalIgnoreCase)) return typeof(Sprite).FullName;
            if (normalizedPath.StartsWith("Skeleton/", StringComparison.OrdinalIgnoreCase)) return typeof(UnityEngine.Object).FullName;

            return requestedType ?? string.Empty;
        }

        private static string GetSuggestedExtensions(string path, string requestedType, string resultType, string effectiveType)
        {
            string dumpRelativePath;
            if (DumpRelativePathsBySourcePath.TryGetValue(PathUtility.NormalizeResourcePath(path), out dumpRelativePath))
            {
                string extension = Path.GetExtension(dumpRelativePath);
                if (!string.IsNullOrEmpty(extension)) return extension.ToLowerInvariant();
            }

            string typeName = FirstNonEmpty(effectiveType, resultType, requestedType);
            if (ContainsTypeName(typeName, "TextAsset")) return ".csv; .json; .txt; .bytes";
            if (ContainsTypeName(typeName, "Sprite") || ContainsTypeName(typeName, "Texture2D") || ContainsTypeName(typeName, "Texture")) return ".png; .jpg; .jpeg";
            if (ContainsTypeName(typeName, "AudioClip")) return ".wav";
            if (ContainsTypeName(typeName, "GameObject") ||
                ContainsTypeName(typeName, "Material") ||
                ContainsTypeName(typeName, "Shader") ||
                ContainsTypeName(typeName, "AnimationClip") ||
                ContainsTypeName(typeName, "RuntimeAnimatorController") ||
                PathUtility.NormalizeResourcePath(path).StartsWith("Skeleton/", StringComparison.OrdinalIgnoreCase))
            {
                return "AssetBundle";
            }

            return "Unknown; prefer AssetBundle";
        }

        private static string GetRawOverrideCandidates(string path, string suggestedExtensions)
        {
            string normalizedPath = PathUtility.NormalizeResourcePath(path);
            if (string.IsNullOrEmpty(normalizedPath)) return string.Empty;
            if (string.IsNullOrEmpty(suggestedExtensions)) return "RES/Raw/" + normalizedPath;

            if (suggestedExtensions.IndexOf("AssetBundle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                suggestedExtensions.IndexOf("Unknown", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Use RES/res_manifest.json with path=\"" + normalizedPath + "\" and source=\"Bundles/your.bundle:assetName\"";
            }

            string[] parts = suggestedExtensions.Split(';');
            List<string> candidates = new List<string>();
            foreach (string part in parts)
            {
                string extension = part.Trim();
                if (!extension.StartsWith(".", StringComparison.Ordinal)) continue;
                candidates.Add("RES/Raw/" + normalizedPath + extension);
            }

            return string.Join("; ", candidates.ToArray());
        }

        private static string GetManifestType(string effectiveType)
        {
            if (string.IsNullOrEmpty(effectiveType)) return string.Empty;

            int lastDot = effectiveType.LastIndexOf('.');
            return lastDot >= 0 && lastDot + 1 < effectiveType.Length
                ? effectiveType.Substring(lastDot + 1)
                : effectiveType;
        }

        private static bool ContainsTypeName(string fullTypeName, string simpleTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName)) return false;
            return fullTypeName.IndexOf(simpleTypeName, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null) return string.Empty;
            foreach (string value in values)
            {
                if (!string.IsNullOrEmpty(value)) return value;
            }

            return string.Empty;
        }

        private static string ChooseNonEmpty(string preferred, string fallback)
        {
            return string.IsNullOrEmpty(preferred) ? (fallback ?? string.Empty) : preferred;
        }

        private static void LoadDumpManifest(string gameRoot)
        {
            DumpRelativePathsBySourcePath.Clear();

            string manifestPath = Path.Combine(gameRoot, "DataDump", "Latest", "manifest.json");
            if (!File.Exists(manifestPath)) return;

            try
            {
                Dictionary<string, object> root = SimpleJson.ParseObject(File.ReadAllText(manifestPath));
                object entriesObject;
                if (!SimpleJson.TryGetValueIgnoreCase(root, "Entries", out entriesObject)) return;

                object[] entries = entriesObject as object[];
                if (entries == null) return;

                foreach (object item in entries)
                {
                    Dictionary<string, object> entry = item as Dictionary<string, object>;
                    if (entry == null) continue;

                    string sourcePath = SimpleJson.GetString(entry, "SourcePath");
                    string relativePath = SimpleJson.GetString(entry, "RelativePath");
                    if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(relativePath)) continue;

                    DumpRelativePathsBySourcePath[PathUtility.NormalizeResourcePath(sourcePath)] = relativePath;
                }

                LoggerManager.Info("Loaded resource dump manifest hints: " + DumpRelativePathsBySourcePath.Count);
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("Failed to read resource dump manifest hints: " + ex.Message);
            }
        }

        private static string Csv(string value)
        {
            if (value == null) value = string.Empty;
            value = value.Replace("\r", "\\r").Replace("\n", "\\n");
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private sealed class UniqueResourceRow
        {
            public string Path;
            public string RequestedType;
            public string EffectiveType;
            public string ResultType;
            public string AssetName;
            public string SuggestedExtensions;
            public string RawOverrideCandidates;
            public string ManifestType;
            public string Outcome;
            public int HitCount;
            public string FirstSeen;
            public string LastSeen;
            public string LastScene;
            public string SourceKind;
            public string Source;
        }
    }
}
