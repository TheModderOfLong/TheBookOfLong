using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// HavePreLover 查询指令。
    /// 格式: HavePreLover:源角色ID/名称-目标角色ID/名称(可选)
    /// 目标角色为空时：判断源角色是否至少有一个准恋人。
    /// 目标角色不为空时：双向判断源角色与目标角色之间是否存在准恋人关系。
    /// </summary>
    [ConditionQuery("HavePreLover")]
    public static class QueryHavePreLover
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
            {
                LoggerManager.Warning("  HavePreLover查询: 参数不足，格式 [HavePreLover:源角色ID/名称-目标角色ID/名称(可选)]");
                return "0";
            }

            string[] args = parts[1].Split('-');

            HeroData source = CommonHandlers.ResolveHeroId(plotController, args[0]);
            if (source == null)
            {
                LoggerManager.Warning($"  HavePreLover查询: 未找到源角色 \"{args[0]}\"");
                return "0";
            }

            string targetIdRaw = args.Length > 1 ? args[1] : null;
            if (string.IsNullOrWhiteSpace(targetIdRaw))
            {
                var preLovers = source.PreLovers;
                if (preLovers != null && preLovers.Count > 0)
                    return "1";

                WorldData worldData = CommonHandlers.GetWorldData();
                if (worldData != null && worldData.Heros != null)
                {
                    int sourceId = source.heroID;
                    foreach (var hero in worldData.Heros)
                    {
                        if (hero != null && hero.HavePrelover(sourceId))
                            return "1";
                    }
                }
                return "0";
            }

            HeroData target = CommonHandlers.ResolveHeroId(plotController, targetIdRaw);
            if (target == null)
            {
                LoggerManager.Warning($"  HavePreLover查询: 未找到目标角色 \"{targetIdRaw}\"");
                return "0";
            }

            return (source.HavePrelover(target.heroID) || target.HavePrelover(source.heroID)) ? "1" : "0";
        }
    }
}
