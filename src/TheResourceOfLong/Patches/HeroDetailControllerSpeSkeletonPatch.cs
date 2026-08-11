using System;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;

namespace TheResourceOfLong
{
    [HarmonyPatch(typeof(HeroDetailController), "FreshNowHeroDetail")]
    internal static class HeroDetailControllerFreshNowHeroDetailPatch
    {
        [HarmonyPostfix]
        public static void Postfix(HeroDetailController __instance)
        {
            HeroDetailSpeSkeletonUiPolicy.Apply(__instance);
        }
    }

    [HarmonyPatch(typeof(HeroDetailController), "RefreshHeroSkeleton")]
    internal static class HeroDetailControllerRefreshHeroSkeletonPatch
    {
        [HarmonyPostfix]
        public static void Postfix(HeroDetailController __instance)
        {
            HeroDetailSpeSkeletonUiPolicy.Apply(__instance);
        }
    }

    internal static class HeroDetailSpeSkeletonUiPolicy
    {
        public static void Apply(HeroDetailController controller)
        {
            try
            {
                if (controller == null || controller.heroDetailPanel == null) return;

                HeroData hero = controller.nowShowHero;
                Transform toggleTransform = controller.heroDetailPanel.transform.Find("UseSpeSkeleton");
                if (toggleTransform == null)
                {
                    LoggerManager.Debug("HeroDetail SpeSkeleton UI policy: UseSpeSkeleton toggle not found. heroID=" +
                        (hero == null ? -1 : hero.heroID) + ", heroName=" + SafeHeroName(hero));
                    return;
                }

                bool runtimeOverride = RuntimeSpeHeroSkeletonOverrideRegistry.HasOverride(hero);
                bool hasFeature = SpeHeroSkeletonPolicy.HasSpeHeroPortraitFeature(hero);
                bool isEnabled = hasFeature && SpeHeroSkeletonPolicy.IsPlayerSpeSkeletonEnabled(hero);
                GameObject toggleObject = toggleTransform.gameObject;
                bool activeBefore = toggleObject.activeSelf;
                Toggle toggle = toggleTransform.GetComponent<Toggle>();
                if (toggle != null)
                {
                    toggle.SetIsOnWithoutNotify(isEnabled);
                }

                if (toggleObject.activeSelf != hasFeature) toggleObject.SetActive(hasFeature);
                LoggerManager.Debug("HeroDetail SpeSkeleton UI policy: heroID=" + (hero == null ? -1 : hero.heroID) +
                    ", heroName=" + SafeHeroName(hero) +
                    ", speHero=" + (hero != null && hero.speHero) +
                    ", runtimeOverride=" + runtimeOverride +
                    ", hasFeature=" + hasFeature +
                    ", enabled=" + isEnabled +
                    ", toggleFound=true" +
                    ", toggleActiveBefore=" + activeBefore +
                    ", toggleActiveAfter=" + toggleObject.activeSelf +
                    ", toggleHasComponent=" + (toggle != null));
            }
            catch (Exception ex)
            {
                LoggerManager.Debug("Failed to apply HeroDetail SpeSkeleton UI policy: " + ex.Message);
            }
        }

        private static string SafeHeroName(HeroData hero)
        {
            if (hero == null || hero.heroName == null) return string.Empty;
            return hero.heroName;
        }
    }
}
