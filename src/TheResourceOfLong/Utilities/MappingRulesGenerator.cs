using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TheResourceOfLong
{
    public static class MappingRulesGenerator
    {
        private const string MappingDirectoryName = "Mapping";
        private const string MappingRulesFileName = "MappingRules.csv";
        private const string ManifestFileName = "res_manifest.json";
        private const string BundleDirectoryName = "Bundles";
        private const string BundleManifestSearchPattern = "*.manifest.json";
        private const string HeaderV2 = "\u7f16\u53f7,\u542f\u7528,\u8d44\u6e90\u8def\u5f84,\u8986\u76d6\u7c7b\u578b,\u76ee\u6807,\u53c2\u6570,\u5907\u6ce8";
        private const string Header = "启用,资源路径,覆盖类型,目标,参数,备注";

        private static bool _initialized;

        public static void Initialize(string gameRoot, string modsOfLongRoot)
        {
            if (_initialized) return;
            _initialized = true;

            ResourceProbeConfig config = UserConfigManager.LoadOrCreate(gameRoot);
            if (config == null || !config.EnableMappingRulesGenerator) return;

            List<ModProjectInfo> projects = ModDiscovery.DiscoverProjects(modsOfLongRoot);
            int generatedCount = 0;
            foreach (ModProjectInfo project in projects)
            {
                if (TryGenerate(project)) generatedCount++;
            }

            LoggerManager.Info("MappingRules generator finished. Generated file count: " + generatedCount);
        }

        private static bool TryGenerate(ModProjectInfo project)
        {
            string mappingRoot = MappingRulesPathResolver.GetMappingRoot(project);
            if (!Directory.Exists(mappingRoot) && !HasManifestMappingResources(project)) return false;

            if (MappingRulesPathResolver.HasExistingRules(project)) return false;

            string rulesPath = MappingRulesPathResolver.GetDefaultOutputPath(project);

            try
            {
                Directory.CreateDirectory(mappingRoot);

                SortedDictionary<string, GeneratedRuleCandidate> candidates = new SortedDictionary<string, GeneratedRuleCandidate>(StringComparer.OrdinalIgnoreCase);
                AddFileCandidates(mappingRoot, candidates);
                AddManifestCandidates(project, candidates);

                StringBuilder builder = new StringBuilder();
                builder.AppendLine(HeaderV2);
                foreach (GeneratedRuleCandidate candidate in candidates.Values)
                {
                    builder.AppendLine(string.Join(",",
                        Csv(string.Empty),
                        Csv("0"),
                        Csv(candidate.ResourcePath),
                        Csv(candidate.OverrideType),
                        Csv(candidate.Target),
                        Csv(candidate.Parameters),
                        Csv(candidate.Remark)));
                }

                File.WriteAllText(rulesPath, builder.ToString(), new UTF8Encoding(true));
                LoggerManager.Info("Generated MappingRules.csv: " + rulesPath + " candidate count=" + candidates.Count);
                return true;
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("Failed to generate MappingRules.csv for " + project.ModId + ": " + ex.Message);
                return false;
            }
        }

        private static void AddFileCandidates(string mappingRoot, SortedDictionary<string, GeneratedRuleCandidate> candidates)
        {
            if (!Directory.Exists(mappingRoot)) return;

            string[] files = Directory.GetFiles(mappingRoot, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
            {
                string relative = PathUtility.NormalizeResourcePath(PathUtility.GetRelativePath(mappingRoot, file));
                if (ShouldSkipMappingFile(relative, file)) continue;

                AddCandidate(relative, candidates);
            }
        }

        private static void AddManifestCandidates(ModProjectInfo project, SortedDictionary<string, GeneratedRuleCandidate> candidates)
        {
            List<string> manifestPaths = ReadManifestMappingResourcePaths(project);
            foreach (string manifestPath in manifestPaths)
            {
                string resourcePath = manifestPath.Substring((MappingDirectoryName + "/").Length);
                AddCandidate(resourcePath, candidates);
            }
        }

        private static bool HasManifestMappingResources(ModProjectInfo project)
        {
            return ReadManifestMappingResourcePaths(project).Count > 0;
        }

        private static List<string> ReadManifestMappingResourcePaths(ModProjectInfo project)
        {
            List<string> result = new List<string>();
            string manifestPath = Path.Combine(project.ResDirectoryPath, ManifestFileName);
            ReadManifestMappingResourcePaths(manifestPath, result);

            string bundleRoot = Path.Combine(project.ResDirectoryPath, BundleDirectoryName);
            if (Directory.Exists(bundleRoot))
            {
                string[] bundleManifestPaths = Directory.GetFiles(bundleRoot, BundleManifestSearchPattern, SearchOption.TopDirectoryOnly);
                Array.Sort(bundleManifestPaths, StringComparer.OrdinalIgnoreCase);
                foreach (string bundleManifestPath in bundleManifestPaths)
                {
                    ReadManifestMappingResourcePaths(bundleManifestPath, result);
                }
            }

            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        private static void ReadManifestMappingResourcePaths(string manifestPath, List<string> result)
        {
            if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath)) return;
            try
            {
                Dictionary<string, object> root = SimpleJson.ParseObject(File.ReadAllText(manifestPath));
                object resourcesObject;
                if (!SimpleJson.TryGetValueIgnoreCase(root, "resources", out resourcesObject)) return;

                object[] resources = resourcesObject as object[];
                if (resources == null) return;

                foreach (object item in resources)
                {
                    Dictionary<string, object> resource = item as Dictionary<string, object>;
                    if (resource == null) continue;

                    string path = PathUtility.NormalizeResourcePath(SimpleJson.GetString(resource, "path"));
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    if (!path.StartsWith(MappingDirectoryName + "/", StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(path, MappingDirectoryName + "/" + MappingRulesFileName, StringComparison.OrdinalIgnoreCase)) continue;

                    result.Add(path);
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("Failed to inspect manifest for MappingRules generator: " + manifestPath + " - " + ex.Message);
            }
        }

        private static bool ShouldSkipMappingFile(string relativePath, string fullPath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return true;
            string fileName = Path.GetFileName(relativePath);
            if (string.Equals(fileName, MappingRulesFileName, StringComparison.OrdinalIgnoreCase)) return true;
            if (fileName.StartsWith("~", StringComparison.Ordinal)) return true;
            if (fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) return true;
            if (fileName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)) return true;
            if (fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) return true;
            FileAttributes attributes = File.GetAttributes(fullPath);
            return (attributes & FileAttributes.Directory) != 0;
        }

        private static void AddCandidate(string resourcePath, SortedDictionary<string, GeneratedRuleCandidate> candidates)
        {
            string normalized = PathUtility.NormalizeResourcePath(resourcePath);
            if (string.IsNullOrWhiteSpace(normalized)) return;
            if (candidates.ContainsKey(normalized)) return;

            GeneratedRuleCandidate candidate = InferCandidate(normalized);
            candidates[normalized] = candidate;
        }

        private static GeneratedRuleCandidate InferCandidate(string resourcePath)
        {
            GeneratedRuleCandidate candidate = new GeneratedRuleCandidate();
            candidate.ResourcePath = resourcePath;
            candidate.OverrideType = string.Empty;
            candidate.Target = string.Empty;
            candidate.Parameters = string.Empty;
            candidate.Remark = "自动生成";

            string withoutExtension = PathUtility.RemoveExtensionFromResourcePath(resourcePath);
            string extension = Path.GetExtension(resourcePath).ToLowerInvariant();

            if (IsSpeHeroSkeletonPath(resourcePath))
            {
                candidate.OverrideType = MappingOverrideType.SpeHeroSkeleton.ToString();
                candidate.Target = Path.GetFileNameWithoutExtension(resourcePath);
                candidate.Parameters = IsImageExtension(extension)
                    ? "fitMode=FitHeight;scale=1;anchorX=0.5;anchorY=0.1;offsetX=0;offsetY=0;applyWhen=UseSpeSkeleton"
                    : "scale=1;anchorX=0.5;anchorY=0.1;offsetX=0;offsetY=0;applyWhen=UseSpeSkeleton";
                return candidate;
            }

            if (IsImageExtension(extension))
            {
                candidate.OverrideType = MappingOverrideType.AtlasSprite.ToString();
                candidate.Target = withoutExtension;
            }

            return candidate;
        }

        private static bool IsSpeHeroSkeletonPath(string resourcePath)
        {
            if (!resourcePath.StartsWith("Skeleton/SpeHero/", StringComparison.OrdinalIgnoreCase)) return false;

            string extension = Path.GetExtension(resourcePath).ToLowerInvariant();
            return IsImageExtension(extension) || string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsImageExtension(string extension)
        {
            return string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase);
        }

        private static string Csv(string value)
        {
            if (value == null) value = string.Empty;
            bool mustQuote = value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0 || value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0;
            value = value.Replace("\"", "\"\"");
            return mustQuote ? "\"" + value + "\"" : value;
        }

        private sealed class GeneratedRuleCandidate
        {
            public string ResourcePath;
            public string OverrideType;
            public string Target;
            public string Parameters;
            public string Remark;
        }
    }
}
