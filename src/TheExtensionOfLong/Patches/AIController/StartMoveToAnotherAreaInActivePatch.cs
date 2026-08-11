using System;
using HarmonyLib;
using Il2Cpp;

namespace TheExtensionOfLong
{
    [HarmonyPatch(typeof(AIController), "StartMoveToAnotherArea", new[] { typeof(HeroData), typeof(int) })]
    public static class StartMoveToAnotherAreaInActivePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(HeroData hero, int targetID)
        {
            try
            {
                if (!HeroInActiveManager.IsInActive(hero))
                    return true;

                LoggerManager.Debug($"inActive: 阻止换区 hero={DescribeHero(hero)}, targetArea={targetID}");
                return false;
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"inActive: StartMoveToAnotherArea Patch 异常，放行原逻辑: {ex}");
                return true;
            }
        }

        private static string DescribeHero(HeroData hero)
        {
            if (hero == null)
                return "<null>";

            return $"{hero.heroName}(ID={hero.heroID})";
        }
    }
}
