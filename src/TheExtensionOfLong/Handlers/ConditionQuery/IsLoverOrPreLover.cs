using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 判断两个角色是否存在夫妻或恋人关系（满足任一即可）。
    /// 格式: [$IsLoverOrPreLover:源角色ID/名称:目标角色ID/名称$]
    /// 角色参数为空时分别默认使用 sourceInteractHero 与 targetInteractHero。
    /// </summary>
    [ConditionQuery("IsLoverOrPreLover")]
    public static class QueryIsLoverOrPreLover
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            // 直接复用已有逻辑（parts[0] 在被调用方中不使用，透传安全）
            return (QueryIsLover.TryQuery(plotController, parts) == "1" ||
                    QueryIsPreLover.TryQuery(plotController, parts) == "1") ? "1" : "0";
        }
    }
}
