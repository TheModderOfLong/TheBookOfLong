using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 判断两个角色是否为夫妻关系。
    /// 格式: [$IsLover:源角色ID/名称:目标角色ID/名称$]
    /// 角色参数为空时分别默认使用 sourceInteractHero 与 targetInteractHero。
    /// </summary>
    [ConditionQuery("IsLover")]
    public static class QueryIsLover
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            string sourceId = parts.Length > 1 ? parts[1] : null;
            string targetId = parts.Length > 2 ? parts[2] : null;
            HeroData source = CommonHandlers.ResolveHeroId(plotController, sourceId, plotController.sourceInteractHero);
            HeroData target = CommonHandlers.ResolveHeroId(plotController, targetId, plotController.targetInteractHero);
            return (source != null && target != null && (source.Lover == target.heroID || target.Lover == source.heroID)) ? "1" : "0";
        }
    }
}
