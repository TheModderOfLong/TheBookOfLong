using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TheResourceOfLong
{
    public static class ModResourceRegistry
    {
        private static readonly Dictionary<string, ModResourceEntry> EntriesByPath = new Dictionary<string, ModResourceEntry>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, UnityEngine.Object> Cache = new Dictionary<string, UnityEngine.Object>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<int, ModResourceEntry> TextAssetEntriesByInstanceId = new Dictionary<int, ModResourceEntry>();
        private const string RootManifestFileName = "res_manifest.json";
        private const string BundleManifestSearchPattern = "*.manifest.json";
        private const string BundleDirectoryName = "Bundles";
        private static bool _initialized;
        private static string _gameRoot;
        private static string _modsOfLongRoot;

        // 汇总所有 MOD 的路径类型规则（按 prefix 长度降序排列，确保最长前缀优先匹配）
        private static List<PathTypeRule> _pathTypeRules;
        private static bool _pathTypeRulesSorted;

        public static string GameRoot { get { return _gameRoot; } }
        public static string ModsOfLongRoot { get { return _modsOfLongRoot; } }
        public static int EntryCount { get { return EntriesByPath.Count; } }

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _gameRoot = ModDiscovery.ResolveGameRoot();
            _modsOfLongRoot = ModDiscovery.ResolveModsOfLongRoot(_gameRoot);

            LoggerManager.Info("Game root: " + _gameRoot);
            LoggerManager.Info("Resource mod root: " + _modsOfLongRoot);

            _pathTypeRules = new List<PathTypeRule>();
            _pathTypeRulesSorted = false;

            List<ModProjectInfo> projects = ModDiscovery.DiscoverProjects(_modsOfLongRoot);
            LoggerManager.Info("Discovered " + projects.Count + " resource mod project(s).");

            int resourceOrder = 0;
            foreach (ModProjectInfo project in projects)
            {
                LoggerManager.Info("Loading RES project: " + project.ModId +
                    " Source=" + FormatDiscoverySource(project) +
                    " LoadOrder=" + project.LoadOrder +
                    " Priority=" + FormatPriority(project));
                LoadManifestEntries(project, ref resourceOrder);
                LoadBundleManifestEntries(project, ref resourceOrder);
                LoadImplicitRawEntries(project, ref resourceOrder);
            }

            LoggerManager.Info("Registered " + EntriesByPath.Count + " virtual resource path(s).");
        }

        public static bool TryLoad(string path, Type requestedType, out UnityEngine.Object asset)
        {
            asset = null;
            Initialize();

            string normalizedPath = PathUtility.NormalizeResourcePath(path);
            if (string.IsNullOrEmpty(normalizedPath)) return false;

            ModResourceEntry entry;
            if (!EntriesByPath.TryGetValue(normalizedPath, out entry))
            {
                return false;
            }

            if (IsRawTextEntry(entry))
            {
                return false;
            }

            string cacheKey = entry.CacheKey(requestedType);
            UnityEngine.Object cached;
            if (Cache.TryGetValue(cacheKey, out cached) && cached != null)
            {
                asset = cached;
                return true;
            }

            try
            {
                asset = entry.SourceKind == ResourceSourceKind.Bundle
                    ? BundleResourceLoader.Load(entry, requestedType)
                    : RawResourceLoader.Load(entry, requestedType);

                if (asset == null)
                {
                    LoggerManager.Warning("Failed to load virtual resource: " + normalizedPath + " from " + entry.Source);
                    return false;
                }

                if (!ResourceTypeResolver.IsRequestedTypeCompatible(asset, requestedType))
                {
                    LoggerManager.Warning("Virtual resource type mismatch: " + normalizedPath + " requested=" + FormatType(requestedType) + " actual=" + asset.GetType().FullName);
                    return false;
                }

                Cache[cacheKey] = asset;
                LoggerManager.Debug("Resolved virtual resource: " + normalizedPath + " -> " + entry.ModId + "/" + entry.Source);
                return true;
            }
            catch (Exception ex)
            {
                LoggerManager.Error("Exception while loading virtual resource '" + normalizedPath + "': " + ex);
                asset = null;
                return false;
            }
        }

        public static bool TryLoadAll(string path, Type requestedType, out UnityEngine.Object[] assets)
        {
            assets = null;
            UnityEngine.Object asset;
            if (!TryLoad(path, requestedType, out asset) || asset == null) return false;

            assets = new UnityEngine.Object[] { asset };
            return true;
        }

        public static void BindLoadedTextAsset(string path, UnityEngine.Object asset)
        {
            if (asset == null) return;

            TextAsset textAsset = asset as TextAsset;
            if (textAsset == null) return;

            ModResourceEntry entry;
            if (!TryGetEntry(path, out entry) || !IsRawTextEntry(entry)) return;

            int instanceId = textAsset.GetInstanceID();
            TextAssetEntriesByInstanceId[instanceId] = entry;
            LoggerManager.Debug("Bound TextAsset instance " + instanceId + " to virtual text resource: " + entry.VirtualPath);
        }

        public static bool TryGetTextAssetText(TextAsset textAsset, out string text)
        {
            text = null;
            ModResourceEntry entry;
            if (!TryGetTextEntry(textAsset, out entry)) return false;

            byte[] bytes;
            if (!TryReadRawBytes(entry, out bytes)) return false;

            text = TextEncodingDetector.DecodeBest(bytes);
            return true;
        }

        public static bool TryGetTextAssetBytes(TextAsset textAsset, out byte[] bytes)
        {
            bytes = null;
            ModResourceEntry entry;
            if (!TryGetTextEntry(textAsset, out entry)) return false;

            return TryReadRawBytes(entry, out bytes);
        }

        public static bool TryGetEntry(string path, out ModResourceEntry entry)
        {
            Initialize();
            string normalizedPath = PathUtility.NormalizeResourcePath(path);
            return EntriesByPath.TryGetValue(normalizedPath, out entry);
        }

        public static void ClearRuntimeCache()
        {
            Cache.Clear();
        }

        public static void UnloadBundles(bool unloadLoadedObjects)
        {
            BundleResourceLoader.UnloadAll(unloadLoadedObjects);
        }

        private static void LoadManifestEntries(ModProjectInfo project, ref int resourceOrder)
        {
            string manifestPath = Path.Combine(project.ResDirectoryPath, RootManifestFileName);
            LoadManifestEntries(project, manifestPath, ref resourceOrder);
        }

        private static void LoadBundleManifestEntries(ModProjectInfo project, ref int resourceOrder)
        {
            string bundleRoot = Path.Combine(project.ResDirectoryPath, BundleDirectoryName);
            if (!Directory.Exists(bundleRoot)) return;

            string[] manifestPaths = Directory.GetFiles(bundleRoot, BundleManifestSearchPattern, SearchOption.TopDirectoryOnly);
            Array.Sort(manifestPaths, StringComparer.OrdinalIgnoreCase);

            foreach (string manifestPath in manifestPaths)
            {
                LoadManifestEntries(project, manifestPath, ref resourceOrder);
            }
        }

        private static void LoadManifestEntries(ModProjectInfo project, string manifestPath, ref int resourceOrder)
        {
            if (!File.Exists(manifestPath)) return;

            ResourceManifest manifest;
            try
            {
                manifest = ParseManifest(File.ReadAllText(manifestPath));
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("Failed to parse resource manifest: " + manifestPath + " - " + ex.Message);
                return;
            }

            // 收集路径类型规则
            if (manifest.PathTypeRules != null)
            {
                foreach (PathTypeRule rule in manifest.PathTypeRules)
                {
                    if (rule != null && !string.IsNullOrWhiteSpace(rule.Prefix))
                    {
                        _pathTypeRules.Add(rule);
                        _pathTypeRulesSorted = false;
                    }
                }
            }

            if (manifest == null || manifest.Resources == null) return;

            foreach (ResourceManifestEntry manifestEntry in manifest.Resources)
            {
                if (manifestEntry == null) continue;
                if (string.IsNullOrWhiteSpace(manifestEntry.Path) || string.IsNullOrWhiteSpace(manifestEntry.Source))
                {
                    LoggerManager.Warning("Skipped invalid resource manifest entry in " + manifestPath);
                    continue;
                }

                ModResourceEntry entry;
                try
                {
                    entry = BuildManifestEntry(project, manifestEntry, resourceOrder++);
                }
                catch (Exception ex)
                {
                    LoggerManager.Warning("Skipped resource manifest entry '" + manifestEntry.Path + "' in " + manifestPath + " - " + ex.Message);
                    continue;
                }

                Register(entry);
            }
        }

        private static ModResourceEntry BuildManifestEntry(ModProjectInfo project, ResourceManifestEntry manifestEntry, int resourceOrder)
        {
            ModResourceEntry entry = CreateBaseEntry(project, resourceOrder);
            entry.VirtualPath = PathUtility.NormalizeResourcePath(manifestEntry.Path);
            entry.Source = PathUtility.NormalizeResourcePath(manifestEntry.Source);
            entry.Mode = string.IsNullOrWhiteSpace(manifestEntry.Mode) ? "replace" : manifestEntry.Mode.Trim();
            entry.FromManifest = true;

            string normalizedPath = entry.VirtualPath;

            // 优先级: 显式声明 Type > 路径类型规则 > 按扩展名推断
            if (string.IsNullOrWhiteSpace(manifestEntry.Type))
            {
                PathTypeRule matchedRule = MatchPathTypeRule(normalizedPath);
                if (matchedRule != null)
                {
                    entry.ResourceTypeName = matchedRule.Type.Trim();
                    entry.PixelsPerUnit = matchedRule.PixelsPerUnit.HasValue ? matchedRule.PixelsPerUnit.Value : 100f;
                    entry.PivotX = matchedRule.PivotX.HasValue ? matchedRule.PivotX.Value : 0.5f;
                    entry.PivotY = matchedRule.PivotY.HasValue ? matchedRule.PivotY.Value : 0.5f;
                }
                else
                {
                    entry.ResourceTypeName = InferTypeName(manifestEntry.Source);
                    entry.PixelsPerUnit = 100f;
                    entry.PivotX = 0.5f;
                    entry.PivotY = 0.5f;
                }
            }
            else
            {
                entry.ResourceTypeName = manifestEntry.Type.Trim();
                entry.PixelsPerUnit = manifestEntry.PixelsPerUnit.HasValue ? manifestEntry.PixelsPerUnit.Value : 100f;
                entry.PivotX = manifestEntry.PivotX.HasValue ? manifestEntry.PivotX.Value : 0.5f;
                entry.PivotY = manifestEntry.PivotY.HasValue ? manifestEntry.PivotY.Value : 0.5f;
            }

            int bundleSeparator = FindBundleSeparator(entry.Source);
            if (bundleSeparator >= 0)
            {
                string bundleRelativePath = entry.Source.Substring(0, bundleSeparator);
                string assetName = entry.Source.Substring(bundleSeparator + 1);
                if (string.IsNullOrWhiteSpace(bundleRelativePath) || string.IsNullOrWhiteSpace(assetName))
                {
                    throw new InvalidOperationException("Invalid bundle source: " + entry.Source);
                }

                entry.SourceKind = ResourceSourceKind.Bundle;
                entry.BundlePath = PathUtility.CombineSafe(project.ResDirectoryPath, bundleRelativePath);
                entry.BundleAssetName = assetName.Trim();
            }
            else
            {
                entry.SourceKind = ResourceSourceKind.Raw;
                entry.FullSourcePath = PathUtility.CombineSafe(project.ResDirectoryPath, entry.Source);
            }

            return entry;
        }

        private static void LoadImplicitRawEntries(ModProjectInfo project, ref int resourceOrder)
        {
            string rawRoot = Path.Combine(project.ResDirectoryPath, "Raw");
            if (!Directory.Exists(rawRoot)) return;

            string[] files = Directory.GetFiles(rawRoot, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
            {
                if (!IsSupportedImplicitRawFile(file)) continue;

                string relative = PathUtility.GetRelativePath(rawRoot, file);
                string virtualPath = PathUtility.RemoveExtensionFromResourcePath(relative);

                if (string.IsNullOrEmpty(virtualPath)) continue;

                ModResourceEntry entry = CreateBaseEntry(project, resourceOrder++);
                entry.VirtualPath = virtualPath;

                // 优先级: 路径类型规则 > 按扩展名推断
                PathTypeRule matchedRule = MatchPathTypeRule(virtualPath);
                if (matchedRule != null)
                {
                    entry.ResourceTypeName = matchedRule.Type.Trim();
                    entry.PixelsPerUnit = matchedRule.PixelsPerUnit.HasValue ? matchedRule.PixelsPerUnit.Value : 100f;
                    entry.PivotX = matchedRule.PivotX.HasValue ? matchedRule.PivotX.Value : 0.5f;
                    entry.PivotY = matchedRule.PivotY.HasValue ? matchedRule.PivotY.Value : 0.5f;
                }
                else
                {
                    entry.ResourceTypeName = InferTypeName(file);
                    entry.PixelsPerUnit = 100f;
                    entry.PivotX = 0.5f;
                    entry.PivotY = 0.5f;
                }

                entry.SourceKind = ResourceSourceKind.Raw;
                entry.Source = PathUtility.NormalizeResourcePath(Path.Combine("Raw", relative));
                entry.FullSourcePath = file;
                entry.Mode = "replace";
                entry.FromManifest = false;

                Register(entry);
            }
        }

        private static ModResourceEntry CreateBaseEntry(ModProjectInfo project, int resourceOrder)
        {
            ModResourceEntry entry = new ModResourceEntry();
            entry.ModId = project.ModId;
            entry.ModDirectoryPath = project.DirectoryPath;
            entry.ResDirectoryPath = project.ResDirectoryPath;
            entry.Priority = project.Priority;
            entry.HasPriority = project.HasPriority;
            entry.ProjectOrder = GetProjectOrder(project);
            entry.ResourceOrder = resourceOrder;
            return entry;
        }

        private static void Register(ModResourceEntry entry)
        {
            string key = PathUtility.NormalizeResourcePath(entry.VirtualPath);
            if (string.IsNullOrEmpty(key)) return;
            entry.VirtualPath = key;

            ModResourceEntry existing;
            if (EntriesByPath.TryGetValue(key, out existing))
            {
                if (IsSameResourceRegistration(existing, entry))
                {
                    return;
                }

                LoggerManager.Warning("Resource path conflict: " + key + ". Keeping " + Describe(existing) + ", skipped " + Describe(entry));
                return;
            }

            EntriesByPath[key] = entry;
            LoggerManager.Debug("Registered resource: " + key + " -> " + Describe(entry));
        }

        private static bool IsSameResourceRegistration(ModResourceEntry existing, ModResourceEntry incoming)
        {
            if (existing == null || incoming == null) return false;
            if (!StringEquals(existing.ModId, incoming.ModId)) return false;
            if (existing.SourceKind != incoming.SourceKind) return false;
            if (!StringEquals(PathUtility.NormalizeResourcePath(existing.Source), PathUtility.NormalizeResourcePath(incoming.Source))) return false;

            if (existing.SourceKind == ResourceSourceKind.Bundle)
            {
                return StringEquals(existing.BundlePath, incoming.BundlePath) &&
                       StringEquals(existing.BundleAssetName, incoming.BundleAssetName);
            }

            return StringEquals(NormalizeFullPath(existing.FullSourcePath), NormalizeFullPath(incoming.FullSourcePath));
        }

        private static bool StringEquals(string left, string right)
        {
            return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return path.Trim();
            }
        }

        private static bool IsSupportedImplicitRawFile(string file)
        {
            string extension = Path.GetExtension(file).ToLowerInvariant();
            return extension == ".csv" ||
                   extension == ".json" ||
                   extension == ".txt" ||
                   extension == ".bytes" ||
                   extension == ".png" ||
                   extension == ".jpg" ||
                   extension == ".jpeg" ||
                   extension == ".wav";
        }

        private static ResourceManifest ParseManifest(string jsonText)
        {
            Dictionary<string, object> root = SimpleJson.ParseObject(jsonText);
            ResourceManifest manifest = new ResourceManifest();
            manifest.Resources = new List<ResourceManifestEntry>();

            object formatVersion;
            if (SimpleJson.TryGetValueIgnoreCase(root, "formatVersion", out formatVersion))
            {
                int parsed;
                if (formatVersion != null && int.TryParse(formatVersion.ToString(), out parsed)) manifest.FormatVersion = parsed;
            }

            object resourcesObject;
            if (!SimpleJson.TryGetValueIgnoreCase(root, "resources", out resourcesObject)) resourcesObject = null;

            object[] resourceArray = resourcesObject as object[];
            if (resourceArray != null)
            {
                foreach (object item in resourceArray)
                {
                    Dictionary<string, object> raw = item as Dictionary<string, object>;
                    if (raw == null) continue;

                    ResourceManifestEntry entry = new ResourceManifestEntry();
                    entry.Path = SimpleJson.GetString(raw, "path");
                    entry.Type = SimpleJson.GetString(raw, "type");
                    entry.Source = SimpleJson.GetString(raw, "source");
                    entry.Mode = SimpleJson.GetString(raw, "mode");
                    entry.PixelsPerUnit = SimpleJson.GetNullableFloat(raw, "pixelsPerUnit");
                    entry.PivotX = SimpleJson.GetNullableFloat(raw, "pivotX");
                    entry.PivotY = SimpleJson.GetNullableFloat(raw, "pivotY");
                    manifest.Resources.Add(entry);
                }
            }

            // 解析路径类型规则
            object pathTypeRulesObject;
            if (SimpleJson.TryGetValueIgnoreCase(root, "pathTypeRules", out pathTypeRulesObject))
            {
                object[] rulesArray = pathTypeRulesObject as object[];
                if (rulesArray != null)
                {
                    manifest.PathTypeRules = new List<PathTypeRule>();
                    foreach (object ruleItem in rulesArray)
                    {
                        Dictionary<string, object> ruleRaw = ruleItem as Dictionary<string, object>;
                        if (ruleRaw == null) continue;

                        string prefix = SimpleJson.GetString(ruleRaw, "prefix");
                        if (string.IsNullOrWhiteSpace(prefix)) continue;

                        PathTypeRule rule = new PathTypeRule();
                        rule.Prefix = prefix.Trim();
                        rule.Type = SimpleJson.GetString(ruleRaw, "type");
                        rule.PixelsPerUnit = SimpleJson.GetNullableFloat(ruleRaw, "pixelsPerUnit");
                        rule.PivotX = SimpleJson.GetNullableFloat(ruleRaw, "pivotX");
                        rule.PivotY = SimpleJson.GetNullableFloat(ruleRaw, "pivotY");
                        manifest.PathTypeRules.Add(rule);
                    }
                }
            }

            return manifest;
        }

        private static bool TryGetTextEntry(TextAsset textAsset, out ModResourceEntry entry)
        {
            entry = null;
            if (textAsset == null) return false;

            int instanceId = textAsset.GetInstanceID();
            return TextAssetEntriesByInstanceId.TryGetValue(instanceId, out entry) && IsRawTextEntry(entry);
        }

        private static bool TryReadRawBytes(ModResourceEntry entry, out byte[] bytes)
        {
            bytes = null;
            if (entry == null || string.IsNullOrEmpty(entry.FullSourcePath) || !File.Exists(entry.FullSourcePath)) return false;

            try
            {
                bytes = File.ReadAllBytes(entry.FullSourcePath);
                return true;
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("Failed to read text resource: " + entry.FullSourcePath + " - " + ex.Message);
                return false;
            }
        }

        private static bool IsRawTextEntry(ModResourceEntry entry)
        {
            if (entry == null || entry.SourceKind != ResourceSourceKind.Raw) return false;

            string extension = Path.GetExtension(entry.FullSourcePath ?? entry.Source ?? string.Empty).ToLowerInvariant();
            return extension == ".csv" || extension == ".json" || extension == ".txt" || extension == ".bytes" ||
                   string.Equals(entry.ResourceTypeName, "TextAsset", StringComparison.OrdinalIgnoreCase);
        }

        private static string InferTypeName(string source)
        {
            string extension = Path.GetExtension(source).ToLowerInvariant();
            if (extension == ".csv" || extension == ".json" || extension == ".txt" || extension == ".bytes") return "TextAsset";
            if (extension == ".png" || extension == ".jpg" || extension == ".jpeg") return "Sprite";
            if (extension == ".wav" || extension == ".ogg") return "AudioClip";
            return "Object";
        }

        /// <summary>
        /// 按最长前缀优先匹配路径类型规则。
        /// 返回匹配到的规则，或 null。
        /// </summary>
        private static PathTypeRule MatchPathTypeRule(string virtualPath)
        {
            if (_pathTypeRules == null || _pathTypeRules.Count == 0) return null;
            if (string.IsNullOrEmpty(virtualPath)) return null;

            // 确保 prefix 按长度降序排列（最长匹配优先）
            if (!_pathTypeRulesSorted)
            {
                _pathTypeRules.Sort((a, b) => b.Prefix.Length.CompareTo(a.Prefix.Length));
                _pathTypeRulesSorted = true;
            }

            string normalized = virtualPath.Replace('\\', '/').TrimStart('/');

            for (int i = 0; i < _pathTypeRules.Count; i++)
            {
                PathTypeRule rule = _pathTypeRules[i];
                string prefix = rule.Prefix.Replace('\\', '/').TrimStart('/');

                if (string.IsNullOrEmpty(prefix)) continue;

                if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return rule;
                }
            }

            return null;
        }

        private static int FindBundleSeparator(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return -1;

            int separator = source.IndexOf(':');
            if (separator <= 1) return -1;
            return separator;
        }

        private static string FormatPriority(ModProjectInfo project)
        {
            return project.HasPriority ? project.Priority.ToString() : "missing(lowest)";
        }

        private static string FormatDiscoverySource(ModProjectInfo project)
        {
            return project.UsesBookLoadOrder ? "TheBookOfLong" : "Legacy";
        }

        private static int GetProjectOrder(ModProjectInfo project)
        {
            return project.UsesBookLoadOrder ? project.LoadOrder : project.DiscoveryOrder;
        }

        private static string FormatType(Type type)
        {
            return type == null ? "null" : type.FullName;
        }

        private static string Describe(ModResourceEntry entry)
        {
            return entry.ModId + "(Priority=" + (entry.HasPriority ? entry.Priority.ToString() : "missing") + ", Source=" + entry.Source + ")";
        }
    }
}
