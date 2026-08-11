using HarmonyLib;
using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 修复 HeroDetailController.TalkButtonClicked() 跳过世界剧情事件检查的问题。
    /// 
    /// 方案B：直接复用 PlotController.ShowHeroInteractUI(hero) 的完整交互流程，
    /// 保证队伍面板对话与场景交互在世界剧情触发、任务匹配和常规对话上的行为一致。
    /// </summary>
    [HarmonyPatch(typeof(HeroDetailController), nameof(HeroDetailController.TalkButtonClicked))]
    public static class TalkButtonClickedPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(HeroDetailController __instance)
        {
            HeroData hero = __instance?.nowShowHero;
            if (hero == null)
                return true;

            PlotController plotController = PlotController._instance;
            if (plotController == null)
                return true;

            plotController.targetInteractHero = hero;
            __instance.UnshowHeroDetail();
            plotController.ShowHeroInteractUI(hero);

            return false;
        }
    }
}
