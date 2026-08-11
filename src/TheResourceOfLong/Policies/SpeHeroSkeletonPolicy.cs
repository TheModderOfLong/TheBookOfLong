using System;
using Il2Cpp;
using Il2CppSpine.Unity;

namespace TheResourceOfLong
{
    public static class SpeHeroSkeletonPolicy
    {
        private const string HideSpeSkeletonSuffix = "hideSpeSkeleton";

        public static bool IsPlayerSpeSkeletonEnabled(HeroData heroData)
        {
            if (!HasSpeHeroFlag(heroData) && !RuntimeSpeHeroSkeletonOverrideRegistry.HasOverride(heroData)) return false;

            string heroName = GetHeroName(heroData);
            return !IsHiddenByPlayerPreference(heroName);
        }

        public static bool HasSpeHeroPortraitFeature(HeroData heroData)
        {
            if (RuntimeSpeHeroSkeletonOverrideRegistry.HasOverride(heroData)) return true;
            if (!HasSpeHeroFlag(heroData)) return false;
            if (SpeHeroSkeletonOverrideRegistry.HasOverride(heroData)) return true;
            return ShouldUseVanillaSpeHeroSkeleton(heroData);
        }

        public static bool ShouldUseVanillaSpeHeroSkeleton(HeroData heroData)
        {
            if (!HasSpeHeroFlag(heroData)) return false;
            return HasVanillaSpeHeroSkeletonResource(heroData);
        }

        public static bool HasCompatibleSpeSkeletonBranchResource(HeroData heroData)
        {
            if (HasDynamicSkeletonDataAsset(heroData)) return true;
            if (!HasSpeHeroFlag(heroData)) return false;
            if (HasMappedSkeletonDataAsset(heroData)) return true;
            return HasVanillaSpeHeroSkeletonResource(heroData);
        }

        public static string GetVanillaSpeHeroSkeletonPath(HeroData heroData)
        {
            if (heroData == null) return string.Empty;
            return "Skeleton/SpeHero/" + heroData.heroID + "/skeleton_SkeletonData";
        }

        private static bool HasSpeHeroFlag(HeroData heroData)
        {
            if (heroData == null) return false;
            if (heroData.heroID == 0) return false;
            if (!heroData.speHero) return false;
            return GetHeroName(heroData).Length > 0;
        }

        private static string GetHeroName(HeroData heroData)
        {
            return heroData == null ? string.Empty : (heroData.heroName ?? string.Empty).Trim();
        }

        private static bool IsHiddenByPlayerPreference(string heroName)
        {
            try
            {
                if (GameDataController.playerPrefData == null || GameDataController.playerPrefData.playerPrefData == null) return false;

                string key = heroName + HideSpeSkeletonSuffix;
                return GameDataController.playerPrefData.playerPrefData.GetInt(key) == 1;
            }
            catch (Exception ex)
            {
                LoggerManager.Debug("Failed to read hideSpeSkeleton preference for heroName=" + heroName + ": " + ex.Message);
                return false;
            }
        }

        private static bool HasVanillaSpeHeroSkeletonResource(HeroData heroData)
        {
            try
            {
                string path = GetVanillaSpeHeroSkeletonPath(heroData);
                if (path.Length == 0) return false;

                UnityEngine.Object asset = UnityEngine.Resources.Load(path);
                return IsSkeletonDataAsset(asset);
            }
            catch (Exception ex)
            {
                LoggerManager.Debug("Failed to probe vanilla SpeHeroSkeleton resource for heroName=" + GetHeroName(heroData) + ": " + ex.Message);
                return false;
            }
        }

        private static bool HasMappedSkeletonDataAsset(HeroData heroData)
        {
            try
            {
                SpeHeroSkeletonOverrideEntry entry;
                if (!SpeHeroSkeletonOverrideRegistry.TryGet(heroData, out entry) || entry == null) return false;

                if (SpeHeroSkeletonOverrideRegistry.IsSupportedImage(entry.ResourcePath) ||
                    SpeHeroSkeletonOverrideRegistry.IsPrefab(entry.ResourcePath))
                {
                    return false;
                }

                SkeletonDataAsset skeletonDataAsset;
                return SpeHeroSkeletonOverrideRegistry.TryLoadSkeletonDataAsset(entry, out skeletonDataAsset) && skeletonDataAsset != null;
            }
            catch (Exception ex)
            {
                LoggerManager.Debug("Failed to probe mapped SpeHeroSkeleton resource for heroName=" + GetHeroName(heroData) + ": " + ex.Message);
                return false;
            }
        }

        private static bool HasDynamicSkeletonDataAsset(HeroData heroData)
        {
            try
            {
                SpeHeroSkeletonOverrideEntry entry;
                if (!RuntimeSpeHeroSkeletonOverrideRegistry.TryGet(heroData, out entry) || entry == null) return false;

                if (SpeHeroSkeletonOverrideRegistry.IsSupportedImage(entry.ResourcePath) ||
                    SpeHeroSkeletonOverrideRegistry.IsPrefab(entry.ResourcePath))
                {
                    return false;
                }

                SkeletonDataAsset skeletonDataAsset;
                return SpeHeroSkeletonOverrideRegistry.TryLoadSkeletonDataAsset(entry, out skeletonDataAsset) && skeletonDataAsset != null;
            }
            catch (Exception ex)
            {
                LoggerManager.Debug("Failed to probe runtime SpeHeroSkeleton resource for heroName=" + GetHeroName(heroData) + ": " + ex.Message);
                return false;
            }
        }

        private static bool IsSkeletonDataAsset(UnityEngine.Object asset)
        {
            if (asset == null) return false;
            if (asset is SkeletonDataAsset) return true;

            try
            {
                return asset.TryCast<SkeletonDataAsset>() != null;
            }
            catch
            {
                return false;
            }
        }
    }
}

