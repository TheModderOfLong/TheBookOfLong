using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 判断指定角色是否拥有恋人。
    /// 格式: [$HaveLover:角色ID/角色名称$]
    /// 角色参数为空时默认使用 sourceInteractHero。
    /// </summary>
    [ConditionQuery("HaveLover")]
    public static class QueryHaveLover
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            string heroId = parts.Length > 1 ? parts[1] : null;
            HeroData hero = CommonHandlers.ResolveHeroId(plotController, heroId, plotController.sourceInteractHero);
            return (hero != null && hero.HaveLover()) ? "1" : "0";
        }
    }
}
