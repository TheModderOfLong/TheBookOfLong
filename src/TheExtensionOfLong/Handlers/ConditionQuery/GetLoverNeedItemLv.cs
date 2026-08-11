using Il2Cpp;
using System;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 查询指定角色结缘所需道具品级。
    /// 格式: [$GetLoverNeedItemLv:角色ID/名称$]
    /// </summary>
    [ConditionQuery("GetLoverNeedItemLv")]
    public static class QueryGetLoverNeedItemLv
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            string heroIdRaw = parts.Length > 1 ? parts[1] : null;
            HeroData hero = CommonHandlers.ResolveHeroId(plotController, heroIdRaw);
            if (hero == null)
            {
                LoggerManager.Warning($"  GetLoverNeedItemLv查询: 未找到角色 (id=\"{heroIdRaw}\")");
                return "";
            }
            try
            {
                return plotController.GetLoverNeedItemLv(hero).ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  GetLoverNeedItemLv查询({heroIdRaw}) 失败: {e.Message}");
                return "";
            }
        }
    }
}
