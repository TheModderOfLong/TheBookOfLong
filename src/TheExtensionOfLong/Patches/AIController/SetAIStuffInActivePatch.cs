using System;
using HarmonyLib;
using Il2Cpp;

namespace TheExtensionOfLong
{
    [HarmonyPatch(typeof(AIController), "SetAIStuff", new[] { typeof(HeroData), typeof(HeroAIData), typeof(bool) })]
    public static class SetAIStuffInActivePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(HeroData hero, HeroAIData aiData, bool setInteractTarget)
        {
            try
            {
                if (!HeroInActiveManager.IsInActive(hero))
                    return true;

                if (HeroInActiveManager.IsAllowedAutoAIData(aiData))
                    return true;

                HeroAIData safeData = HeroInActiveManager.CreateSafeAIData(hero);
                hero.SetHeroAIData(safeData);

                LoggerManager.Debug(
                    $"inActive: 替换AI行为 hero={DescribeHero(hero)}, " +
                    $"blocked={DescribeAIData(aiData)}, safe={DescribeAIData(safeData)}, " +
                    $"setInteractTarget={setInteractTarget}");

                return false;
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"inActive: SetAIStuff Patch 异常，放行原逻辑: {ex}");
                return true;
            }
        }

        private static string DescribeAIData(HeroAIData aiData)
        {
            if (aiData == null)
                return "<null>";

            return $"{aiData.aiStuffType}, target={aiData.aiStuffTarget}, bigMapTargetID={aiData.bigMapTargetID}";
        }

        private static string DescribeHero(HeroData hero)
        {
            if (hero == null)
                return "<null>";

            return $"{hero.heroName}(ID={hero.heroID})";
        }
    }
}
