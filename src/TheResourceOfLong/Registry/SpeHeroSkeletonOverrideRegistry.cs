using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Il2Cpp;
using Il2CppSpine.Unity;
using UnityEngine;

namespace TheResourceOfLong
{
    public static class SpeHeroSkeletonOverrideRegistry
    {
        private static readonly Dictionary<string, SpeHeroSkeletonOverrideEntry> EntriesByHeroId = new Dictionary<string, SpeHeroSkeletonOverrideEntry>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, SpeHeroSkeletonOverrideEntry> EntriesByHeroName = new Dictionary<string, SpeHeroSkeletonOverrideEntry>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> ReportedLoadFailures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _initialized;

        public static int EntryCount { get { return EntriesByHeroId.Count + EntriesByHeroName.Count; } }

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            foreach (MappingRuleEntry rule in MappingRuleRegistry.GetEntries(MappingOverrideType.SpeHeroSkeleton))
            {
                LoadMappingRule(rule);
            }

            LoggerManager.Info("Registered " + EntryCount + " SpeHeroSkeleton override(s).");
        }

        public static void Reload()
        {
            EntriesByHeroId.Clear();
            EntriesByHeroName.Clear();
            ReportedLoadFailures.Clear();
            _initialized = false;
            Initialize();
        }

        public static bool TryGet(int heroId, out SpeHeroSkeletonOverrideEntry entry)
        {
            Initialize();
            return EntriesByHeroId.TryGetValue(heroId.ToString(), out entry);
        }

        public static bool TryGet(HeroData heroData, out SpeHeroSkeletonOverrideEntry entry)
        {
            entry = null;
            Initialize();
            if (heroData == null) return false;

            if (RuntimeSpeHeroSkeletonOverrideRegistry.TryGet(heroData, out entry) && entry != null) return true;

            if (EntriesByHeroId.TryGetValue(heroData.heroID.ToString(), out entry)) return true;

            string heroName = (heroData.heroName ?? string.Empty).Trim();
            if (heroName.Length == 0) return false;

            return EntriesByHeroName.TryGetValue(heroName, out entry);
        }

        public static bool HasOverride(HeroData heroData)
        {
            SpeHeroSkeletonOverrideEntry entry;
            return TryGet(heroData, out entry) && entry != null;
        }

        public static bool TryLoadTexture(SpeHeroSkeletonOverrideEntry entry, out Texture2D texture)
        {
            texture = null;
            if (entry == null || entry.LoadFailed) return false;
            if (entry.CachedTexture != null)
            {
                texture = entry.CachedTexture;
                return true;
            }

            if (!IsSupportedImage(entry.ResourcePath)) return false;
            if (string.IsNullOrEmpty(entry.FullSourcePath) || !File.Exists(entry.FullSourcePath))
            {
                entry.LoadFailed = true;
                WarnLoadFailedOnce(entry, "SpeHeroSkeleton image file not found. expected=" + Safe(entry.FullSourcePath) +
                    ", resource=" + Safe(entry.ResourcePath) +
                    ", hint=MappingRules resource path is relative to Res/Mapping.");
                return false;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(entry.FullSourcePath);
                Texture2D loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(loaded, bytes))
                {
                    UnityEngine.Object.Destroy(loaded);
                    entry.LoadFailed = true;
                    WarnLoadFailedOnce(entry, "Failed to decode SpeHeroSkeleton image: " + entry.FullSourcePath);
                    return false;
                }

                loaded.name = "TheResourceOfLong_SpeHeroSkeleton_" + Path.GetFileNameWithoutExtension(entry.ResourcePath);
                loaded.wrapMode = TextureWrapMode.Clamp;
                loaded.filterMode = FilterMode.Bilinear;
                entry.CachedTexture = loaded;
                texture = loaded;
                return true;
            }
            catch (Exception ex)
            {
                entry.LoadFailed = true;
                WarnLoadFailedOnce(entry, "Failed to load SpeHeroSkeleton image: Mapping/" + entry.ResourcePath + " - " + ex.Message);
                return false;
            }
        }

        public static bool TryLoadPrefab(SpeHeroSkeletonOverrideEntry entry, out GameObject prefab)
        {
            prefab = null;
            if (entry == null || entry.LoadFailed) return false;
            if (!IsPrefab(entry.ResourcePath)) return false;
            if (entry.CachedPrefab != null)
            {
                prefab = entry.CachedPrefab;
                return true;
            }

            UnityEngine.Object asset;
            if (!ModResourceRegistry.TryLoad(entry.VirtualResourcePath, typeof(GameObject), out asset) || asset == null)
            {
                entry.LoadFailed = true;
                WarnLoadFailedOnce(entry, "Failed to load SpeHeroSkeleton prefab resource: " + entry.SourceDescription +
                    ", virtualPath=" + Safe(entry.VirtualResourcePath));
                return false;
            }

            prefab = asset as GameObject;
            if (prefab == null)
            {
                try
                {
                    prefab = asset.TryCast<GameObject>();
                }
                catch
                {
                    prefab = null;
                }
            }

            if (prefab == null)
            {
                entry.LoadFailed = true;
                WarnLoadFailedOnce(entry, "SpeHeroSkeleton prefab resource is not GameObject: " + entry.SourceDescription);
                return false;
            }

            entry.CachedPrefab = prefab;
            return true;
        }

        public static bool TryLoadSkeletonDataAsset(SpeHeroSkeletonOverrideEntry entry, out SkeletonDataAsset skeletonDataAsset)
        {
            skeletonDataAsset = null;
            if (entry == null || entry.LoadFailed) return false;
            if (IsSupportedImage(entry.ResourcePath) || IsPrefab(entry.ResourcePath)) return false;
            if (entry.CachedSkeletonDataAsset != null)
            {
                skeletonDataAsset = entry.CachedSkeletonDataAsset;
                return true;
            }

            UnityEngine.Object asset;
            if (entry.Rule == null)
            {
                asset = UnityEngine.Resources.Load(entry.DirectResourcePath);
            }
            else if (!ModResourceRegistry.TryLoad(entry.VirtualResourcePath, typeof(UnityEngine.Object), out asset))
            {
                asset = null;
            }

            if (asset == null)
            {
                entry.LoadFailed = true;
                WarnLoadFailedOnce(entry, "Failed to load SpeHeroSkeleton skeleton resource: " + entry.SourceDescription +
                    ", virtualPath=" + Safe(entry.VirtualResourcePath) +
                    ", directPath=" + Safe(entry.DirectResourcePath));
                return false;
            }

            skeletonDataAsset = asset as SkeletonDataAsset;
            if (skeletonDataAsset == null)
            {
                try
                {
                    skeletonDataAsset = asset.TryCast<SkeletonDataAsset>();
                }
                catch
                {
                    skeletonDataAsset = null;
                }
            }

            if (skeletonDataAsset == null)
            {
                entry.LoadFailed = true;
                WarnLoadFailedOnce(entry, "SpeHeroSkeleton skeleton resource is not SkeletonDataAsset: " + entry.SourceDescription);
                return false;
            }

            entry.CachedSkeletonDataAsset = skeletonDataAsset;
            return true;
        }

        public static bool IsSupportedImage(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return extension == ".png" || extension == ".jpg" || extension == ".jpeg";
        }

        public static bool IsPrefab(string path)
        {
            return string.Equals(Path.GetExtension(path), ".prefab", StringComparison.OrdinalIgnoreCase);
        }

        private static void LoadMappingRule(MappingRuleEntry rule)
        {
            if (rule == null) return;

            string target = (rule.Target ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                LoggerManager.Warning("Skipped SpeHeroSkeleton mapping rule with empty target in " + rule.ModId);
                return;
            }

            SpeHeroSkeletonOverrideEntry entry = BuildEntry(rule);

            int heroId;
            if (int.TryParse(target, out heroId))
            {
                string key = heroId.ToString();
                if (EntriesByHeroId.ContainsKey(key))
                {
                    LoggerManager.Warning("SpeHeroSkeleton override conflict: heroID=" + key + ". Keeping first rule, skipped " + rule.ModId + "/Mapping/" + rule.ResourcePath);
                    return;
                }

                EntriesByHeroId[key] = entry;
                LoggerManager.Debug("Registered SpeHeroSkeleton override: heroID=" + key + " -> " + rule.ModId + "/Mapping/" + rule.ResourcePath);
                return;
            }

            if (EntriesByHeroName.ContainsKey(target))
            {
                LoggerManager.Warning("SpeHeroSkeleton override conflict: heroName=" + target + ". Keeping first rule, skipped " + rule.ModId + "/Mapping/" + rule.ResourcePath);
                return;
            }

            EntriesByHeroName[target] = entry;
            LoggerManager.Debug("Registered SpeHeroSkeleton override: heroName=" + target + " -> " + rule.ModId + "/Mapping/" + rule.ResourcePath);
        }

        public static SpeHeroSkeletonOverrideEntry BuildEntry(MappingRuleEntry rule)
        {
            SpeHeroSkeletonOverrideEntry entry = new SpeHeroSkeletonOverrideEntry();
            entry.Rule = rule;
            entry.DisplayPath = rule == null ? string.Empty : rule.ModId + "/Mapping/" + rule.ResourcePath;
            entry.FitMode = GetEnum(rule, "fitMode", SpeHeroSkeletonFitMode.FitHeight);
            entry.ApplyWhen = GetEnum(rule, "applyWhen", SpeHeroSkeletonApplyWhen.UseSpeSkeleton);
            entry.Scale = GetFloat(rule, "scale", 1f);
            entry.HasAnchorX = TryGetFloat(rule, "anchorX", out entry.AnchorX);
            entry.HasAnchorY = TryGetFloat(rule, "anchorY", out entry.AnchorY);
            entry.AnchorX = Clamp01(entry.AnchorX);
            entry.AnchorY = Clamp01(entry.AnchorY);
            entry.OffsetX = GetFloat(rule, "offsetX", 0f);
            entry.OffsetY = GetFloat(rule, "offsetY", 0f);
            entry.FlipX = GetBool(rule, "flipX", false);
            entry.SceneCameraZoom = Clamp(GetFloat(rule, "sceneCameraZoom", 1f), 0.1f, 10f);
            entry.SceneRenderScale = Clamp(GetFloat(rule, "sceneRenderScale", 1f), 0.5f, 4f);
            entry.HasScenePadding = TryGetFloat(rule, "scenePadding", out entry.ScenePadding);
            entry.HasSceneCameraOffsetX = TryGetFloat(rule, "sceneCameraOffsetX", out entry.SceneCameraOffsetX);
            entry.HasSceneCameraOffsetY = TryGetFloat(rule, "sceneCameraOffsetY", out entry.SceneCameraOffsetY);
            entry.ScenePadding = Clamp(entry.ScenePadding, 0f, 2f);
            entry.SceneCameraOffsetX = Clamp(entry.SceneCameraOffsetX, -2f, 2f);
            entry.SceneCameraOffsetY = Clamp(entry.SceneCameraOffsetY, -2f, 2f);
            return entry;
        }

        private static bool GetBool(MappingRuleEntry rule, string key, bool defaultValue)
        {
            string value;
            if (rule == null || rule.Parameters == null || !rule.Parameters.TryGetValue(key, out value) || string.IsNullOrWhiteSpace(value)) return defaultValue;

            string normalized = value.Trim();
            if (normalized == "1") return true;
            if (normalized == "0") return false;
            if (normalized.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (normalized.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;

            LoggerManager.Warning("Invalid boolean Mapping parameter ignored: " + key + "=" + value +
                                  " in " + (rule.ModId ?? string.Empty) + "/Mapping/" + (rule.ResourcePath ?? string.Empty) +
                                  ". Supported values: 0, 1, true, false.");
            return defaultValue;
        }

        private static TEnum GetEnum<TEnum>(MappingRuleEntry rule, string key, TEnum defaultValue) where TEnum : struct
        {
            string value;
            if (rule.Parameters == null || !rule.Parameters.TryGetValue(key, out value) || string.IsNullOrWhiteSpace(value)) return defaultValue;

            int numeric;
            if (int.TryParse(value, out numeric) && Enum.IsDefined(typeof(TEnum), numeric))
            {
                return (TEnum)Enum.ToObject(typeof(TEnum), numeric);
            }

            TEnum parsed;
            if (Enum.TryParse(value, true, out parsed)) return parsed;
            return defaultValue;
        }

        private static float GetFloat(MappingRuleEntry rule, string key, float defaultValue)
        {
            float parsed;
            return TryGetFloat(rule, key, out parsed) ? parsed : defaultValue;
        }

        private static bool TryGetFloat(MappingRuleEntry rule, string key, out float parsed)
        {
            parsed = 0f;
            string value;
            if (rule.Parameters != null &&
                rule.Parameters.TryGetValue(key, out value) &&
                float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                return true;
            }

            if (rule.Parameters != null &&
                rule.Parameters.TryGetValue(key, out value) &&
                float.TryParse(value, out parsed))
            {
                return true;
            }

            return false;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static void WarnLoadFailedOnce(SpeHeroSkeletonOverrideEntry entry, string message)
        {
            string key = GetFailureKey(entry, message);
            if (!ReportedLoadFailures.Add(key)) return;
            LoggerManager.Warning(message);
        }

        private static string GetFailureKey(SpeHeroSkeletonOverrideEntry entry, string message)
        {
            if (entry == null) return message ?? string.Empty;
            return Safe(entry.VirtualResourcePath) + "|" +
                   Safe(entry.FullSourcePath) + "|" +
                   Safe(entry.DirectResourcePath) + "|" +
                   Safe(entry.ResourcePath) + "|" +
                   (message ?? string.Empty);
        }

        private static string Safe(string value)
        {
            return value ?? string.Empty;
        }
    }
}

