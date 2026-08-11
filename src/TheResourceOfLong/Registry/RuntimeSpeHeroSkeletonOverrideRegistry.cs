using System;
using Il2Cpp;

namespace TheResourceOfLong
{
    internal static class RuntimeSpeHeroSkeletonOverrideRegistry
    {
        private const string KeyPrefix = "SpeHeroSkeleton_";

        public static bool TryGet(HeroData heroData, out SpeHeroSkeletonOverrideEntry entry)
        {
            entry = null;
            if (heroData == null) return false;

            PlotEventLogData logData = GetPlotEventLogData();
            if (logData == null) return false;

            string key = KeyPrefix + heroData.heroID;
            if (!logData.HaveKey(key)) return false;

            string rawValue = logData.Get(key);
            if (string.IsNullOrWhiteSpace(rawValue)) return false;

            string[] parts = rawValue.Split(new[] { '#' }, 2);
            if (parts.Length < 2)
            {
                LoggerManager.Debug("Invalid runtime SpeHeroSkeleton value: key=" + key + ", value=" + rawValue);
                return false;
            }

            int sourceType;
            if (!int.TryParse(parts[0], out sourceType))
            {
                LoggerManager.Debug("Invalid runtime SpeHeroSkeleton type: key=" + key + ", value=" + rawValue);
                return false;
            }

            string sourceParam = (parts[1] ?? string.Empty).Trim();
            if (sourceType == 1)
            {
                return TryBuildVanillaSpeHeroEntry(key, sourceParam, out entry);
            }

            if (sourceType == 2)
            {
                return TryBuildMappingEntry(key, sourceParam, out entry);
            }

            LoggerManager.Debug("Unsupported runtime SpeHeroSkeleton type: key=" + key + ", value=" + rawValue);
            return false;
        }

        public static bool HasOverride(HeroData heroData)
        {
            SpeHeroSkeletonOverrideEntry entry;
            return TryGet(heroData, out entry) && entry != null;
        }

        private static bool TryBuildVanillaSpeHeroEntry(string key, string sourceParam, out SpeHeroSkeletonOverrideEntry entry)
        {
            entry = null;
            int sourceHeroId;
            if (!int.TryParse(sourceParam, out sourceHeroId))
            {
                LoggerManager.Debug("Invalid runtime SpeHeroSkeleton SpeHero parameter: key=" + key + ", parameter=" + sourceParam);
                return false;
            }

            string path = "Skeleton/SpeHero/" + sourceHeroId + "/skeleton_SkeletonData";
            entry = new SpeHeroSkeletonOverrideEntry
            {
                DirectResourcePath = path,
                DisplayPath = "Resources/" + path,
                FitMode = SpeHeroSkeletonFitMode.FitHeight,
                ApplyWhen = SpeHeroSkeletonApplyWhen.UseSpeSkeleton,
                Scale = 1f,
                HasAnchorX = false,
                HasAnchorY = false,
                AnchorX = 0.5f,
                AnchorY = 0.5f,
                OffsetX = 0f,
                OffsetY = 0f
            };
            return true;
        }

        private static bool TryBuildMappingEntry(string key, string sourceParam, out SpeHeroSkeletonOverrideEntry entry)
        {
            entry = null;
            MappingRuleEntry rule;
            if (!MappingRuleRegistry.TryGetById(sourceParam, out rule) || rule == null)
            {
                LoggerManager.Debug("Runtime SpeHeroSkeleton mapping id not found: key=" + key + ", id=" + sourceParam);
                return false;
            }

            if (rule.OverrideType != MappingOverrideType.SpeHeroSkeleton)
            {
                LoggerManager.Debug("Runtime SpeHeroSkeleton mapping id type mismatch: key=" + key + ", id=" + sourceParam + ", type=" + rule.OverrideType);
                return false;
            }

            entry = SpeHeroSkeletonOverrideRegistry.BuildEntry(rule);
            return entry != null;
        }

        private static PlotEventLogData GetPlotEventLogData()
        {
            try
            {
                GameController instance = GameController.Instance;
                if (instance == null || instance.worldData == null) return null;
                return instance.worldData.PlotEventLog;
            }
            catch
            {
                return null;
            }
        }
    }
}

