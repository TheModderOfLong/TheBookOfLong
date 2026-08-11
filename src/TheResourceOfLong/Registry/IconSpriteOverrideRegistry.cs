using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace TheResourceOfLong
{
    public static class IconSpriteOverrideRegistry
    {
        private static readonly Dictionary<string, IconSpriteOverrideEntry> EntriesByKey = new Dictionary<string, IconSpriteOverrideEntry>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<IconSpriteOverrideEntry> PendingSymbolicEntries = new List<IconSpriteOverrideEntry>();
        private static bool _initialized;
        private static MethodInfo _tryResolveSymbolicIdMethod;

        public static int EntryCount { get { return EntriesByKey.Count; } }

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            int resourceOrder = 0;
            foreach (MappingRuleEntry rule in MappingRuleRegistry.GetEntries(MappingOverrideType.AtlasSprite))
            {
                LoadMappingRule(rule, ref resourceOrder);
            }

            LoggerManager.Info("Registered " + EntriesByKey.Count + " atlas sprite override(s), pending symbolic target(s): " + PendingSymbolicEntries.Count + ".");
        }

        public static void Reload()
        {
            ClearRuntimeCache();
            EntriesByKey.Clear();
            PendingSymbolicEntries.Clear();
            _initialized = false;
            Initialize();
        }

        public static bool TryLoad(string atlasPath, string spriteName, out Sprite sprite)
        {
            sprite = null;
            Initialize();

            string normalizedAtlasPath = PathUtility.NormalizeResourcePath(atlasPath);
            if (string.IsNullOrEmpty(normalizedAtlasPath) || string.IsNullOrWhiteSpace(spriteName)) return false;

            IconSpriteOverrideEntry entry;
            if (!EntriesByKey.TryGetValue(BuildKey(normalizedAtlasPath, spriteName), out entry))
            {
                TryResolvePendingSymbolicEntries();
                if (!EntriesByKey.TryGetValue(BuildKey(normalizedAtlasPath, spriteName), out entry)) return false;
            }

            if (entry.LoadFailed) return false;

            if (entry.CachedSprite != null)
            {
                sprite = entry.CachedSprite;
                return true;
            }

            try
            {
                sprite = LoadSprite(entry);
                if (sprite == null)
                {
                    entry.LoadFailed = true;
                    return false;
                }

                entry.CachedSprite = sprite;
                LoggerManager.Debug("Resolved atlas sprite override: " + entry.AtlasPath + "/" + entry.SpriteName + " -> " + entry.ModId + "/" + entry.Source);
                return true;
            }
            catch (Exception ex)
            {
                entry.LoadFailed = true;
                LoggerManager.Warning("Failed to load atlas sprite override: " + Describe(entry) + " - " + ex.Message);
                return false;
            }
        }

        public static void ClearRuntimeCache()
        {
            foreach (IconSpriteOverrideEntry entry in EntriesByKey.Values)
            {
                entry.CachedSprite = null;
                entry.LoadFailed = false;
            }
        }

        private static void LoadMappingRule(MappingRuleEntry rule, ref int resourceOrder)
        {
            if (rule == null) return;

            string atlasPath;
            string spriteName;
            if (!TrySplitAtlasTarget(rule.Target, out atlasPath, out spriteName))
            {
                LoggerManager.Warning("Skipped AtlasSprite mapping rule with invalid target: " + rule.Target + " in " + rule.ModId);
                return;
            }

            if (!IsSupportedImage(rule.ResourcePath))
            {
                LoggerManager.Warning("Skipped AtlasSprite mapping rule with unsupported image: Mapping/" + rule.ResourcePath);
                return;
            }

            IconSpriteOverrideEntry entry = new IconSpriteOverrideEntry();
            entry.ModId = rule.ModId;
            entry.AtlasPath = PathUtility.NormalizeResourcePath(atlasPath);
            entry.Source = "Mapping/" + rule.ResourcePath;
            entry.FullSourcePath = rule.FullSourcePath;
            entry.Priority = rule.Priority;
            entry.HasPriority = rule.HasPriority;
            entry.ProjectOrder = rule.ProjectOrder;
            entry.ResourceOrder = resourceOrder++;
            entry.PixelsPerUnit = GetFloat(rule, "pixelsPerUnit", 100f);
            entry.PivotX = GetFloat(rule, "pivotX", 0.5f);
            entry.PivotY = GetFloat(rule, "pivotY", 0.5f);

            int assignedId;
            if (TryResolveSymbolicSpriteName(spriteName, out assignedId))
            {
                entry.SpriteName = assignedId.ToString();
                entry.SymbolicSpriteName = spriteName;
                LoggerManager.Debug("Resolved symbolic atlas sprite target at registration: " + entry.AtlasPath + "/" + spriteName + " -> " + entry.SpriteName);
                Register(entry);
                return;
            }

            if (LooksLikeSymbolicId(spriteName))
            {
                entry.SpriteName = spriteName;
                entry.SymbolicSpriteName = spriteName;
                PendingSymbolicEntries.Add(entry);
                LoggerManager.Debug("Queued symbolic atlas sprite target for lazy resolution: " + entry.AtlasPath + "/" + spriteName + " -> " + Describe(entry));
                return;
            }

            entry.SpriteName = spriteName;
            Register(entry);
        }

        private static bool Register(IconSpriteOverrideEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.AtlasPath) || string.IsNullOrWhiteSpace(entry.SpriteName)) return false;

            string key = BuildKey(entry.AtlasPath, entry.SpriteName);
            IconSpriteOverrideEntry existing;
            if (EntriesByKey.TryGetValue(key, out existing))
            {
                LoggerManager.Warning("Atlas sprite override conflict: " + entry.AtlasPath + "/" + entry.SpriteName + ". Keeping " + Describe(existing) + ", skipped " + Describe(entry));
                return false;
            }

            EntriesByKey[key] = entry;
            LoggerManager.Debug("Registered atlas sprite override: " + entry.AtlasPath + "/" + entry.SpriteName + " -> " + Describe(entry));
            return true;
        }

        private static Sprite LoadSprite(IconSpriteOverrideEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.FullSourcePath) || !File.Exists(entry.FullSourcePath)) return null;

            byte[] bytes = File.ReadAllBytes(entry.FullSourcePath);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, bytes))
            {
                UnityEngine.Object.Destroy(texture);
                LoggerManager.Warning("Failed to decode atlas sprite override image: " + entry.FullSourcePath);
                return null;
            }

            texture.name = "TheResourceOfLong_" + entry.AtlasPath.Replace('/', '_') + "_" + entry.SpriteName;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Rect rect = new Rect(0f, 0f, texture.width, texture.height);
            Vector2 pivot = new Vector2(entry.PivotX, entry.PivotY);
            Sprite sprite = Sprite.Create(texture, rect, pivot, entry.PixelsPerUnit <= 0f ? 100f : entry.PixelsPerUnit);
            if (sprite != null) sprite.name = texture.name;
            return sprite;
        }

        private static string BuildKey(string atlasPath, string spriteName)
        {
            return PathUtility.NormalizeResourcePath(atlasPath) + "\n" + spriteName.Trim();
        }

        private static bool IsSupportedImage(string file)
        {
            string extension = Path.GetExtension(file).ToLowerInvariant();
            return extension == ".png" || extension == ".jpg" || extension == ".jpeg";
        }

        private static bool TrySplitAtlasTarget(string target, out string atlasPath, out string spriteName)
        {
            atlasPath = null;
            spriteName = null;
            if (string.IsNullOrWhiteSpace(target)) return false;

            int separator = target.IndexOf('/');
            if (separator <= 0 || separator >= target.Length - 1) return false;

            atlasPath = target.Substring(0, separator).Trim();
            spriteName = target.Substring(separator + 1).Trim();
            return !string.IsNullOrWhiteSpace(atlasPath) && !string.IsNullOrWhiteSpace(spriteName);
        }

        private static void TryResolvePendingSymbolicEntries()
        {
            if (PendingSymbolicEntries.Count == 0) return;

            for (int i = PendingSymbolicEntries.Count - 1; i >= 0; i--)
            {
                IconSpriteOverrideEntry entry = PendingSymbolicEntries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.SymbolicSpriteName))
                {
                    PendingSymbolicEntries.RemoveAt(i);
                    continue;
                }

                int assignedId;
                if (!TryResolveSymbolicSpriteName(entry.SymbolicSpriteName, out assignedId)) continue;

                entry.SpriteName = assignedId.ToString();
                Register(entry);
                PendingSymbolicEntries.RemoveAt(i);
                LoggerManager.Debug("Resolved symbolic atlas sprite target lazily: " + entry.AtlasPath + "/" + entry.SymbolicSpriteName + " -> " + entry.SpriteName);
            }
        }

        private static bool TryResolveSymbolicSpriteName(string spriteName, out int assignedId)
        {
            assignedId = 0;
            if (!LooksLikeSymbolicId(spriteName)) return false;

            MethodInfo method = GetTryResolveSymbolicIdMethod();
            if (method == null) return false;

            try
            {
                object[] args = new object[] { spriteName, 0 };
                object result = method.Invoke(null, args);
                if (!(result is bool) || !(bool)result) return false;

                assignedId = (int)args[1];
                return true;
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("Failed to resolve symbolic atlas sprite target '" + spriteName + "': " + ex.Message);
                return false;
            }
        }

        private static MethodInfo GetTryResolveSymbolicIdMethod()
        {
            if (_tryResolveSymbolicIdMethod != null) return _tryResolveSymbolicIdMethod;

            Type type = Type.GetType("TheBookOfLong.SymbolicIdService, TheBookOfLong", false);
            if (type == null) return null;

            _tryResolveSymbolicIdMethod = type.GetMethod(
                "TryResolveId",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new Type[] { typeof(string), typeof(int).MakeByRefType() },
                null);

            return _tryResolveSymbolicIdMethod;
        }

        private static bool LooksLikeSymbolicId(string value)
        {
            string text = (value ?? string.Empty).Trim();
            return text.Length > 3 && text.StartsWith("mod", StringComparison.OrdinalIgnoreCase);
        }

        private static float GetFloat(MappingRuleEntry rule, string key, float defaultValue)
        {
            string value;
            float parsed;
            if (rule.Parameters != null &&
                rule.Parameters.TryGetValue(key, out value) &&
                float.TryParse(value, out parsed))
            {
                return parsed;
            }

            return defaultValue;
        }

        private static string Describe(IconSpriteOverrideEntry entry)
        {
            return entry.ModId + "(Priority=" + (entry.HasPriority ? entry.Priority.ToString() : "missing") + ", Source=" + entry.Source + ")";
        }
    }
}
