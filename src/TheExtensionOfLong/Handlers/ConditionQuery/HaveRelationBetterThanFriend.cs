using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 判断两个角色之间是否存在高于朋友的关系。
    /// 格式: [$HaveRelationBetterThanFriend:源角色ID/名称:目标角色ID/名称:检查师徒(可选):检查结义(可选)$]
    /// </summary>
    [ConditionQuery("HaveRelationBetterThanFriend")]
    public static class QueryHaveRelationBetterThanFriend
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            string sourceId = parts.Length > 1 ? parts[1] : null;
            string targetId = parts.Length > 2 ? parts[2] : null;
            bool checkTeacher = parts.Length <= 3 || (parts[3] != "FALSE" && parts[3] != "0");
            bool checkBrother = parts.Length <= 4 || (parts[4] != "FALSE" && parts[4] != "0");
            HeroData source = CommonHandlers.ResolveHeroId(plotController, sourceId, plotController.sourceInteractHero);
            HeroData target = CommonHandlers.ResolveHeroId(plotController, targetId, plotController.targetInteractHero);
            return (source != null && target != null && source.HaveRelationBetterThanFriend(target.heroID, checkTeacher, checkBrother)) ? "1" : "0";
        }
    }
}
