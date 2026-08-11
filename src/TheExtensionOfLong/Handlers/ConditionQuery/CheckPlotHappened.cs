using Il2Cpp;
using System;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 查询指定ID的剧情是否已触发过。
    /// 格式: [$CheckPlotHappened:剧情intID$]
    /// </summary>
    [ConditionQuery("CheckPlotHappened")]
    public static class QueryCheckPlotHappened
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            if (parts.Length < 2 || !int.TryParse(parts[1], out int plotID))
            {
                LoggerManager.Warning($"  CheckPlotHappened查询: 参数格式为 CheckPlotHappened:剧情intID，当前参数=\"{(parts.Length > 1 ? parts[1] : "")}\"");
                return "0";
            }
            try
            {
                WorldData worldData = CommonHandlers.GetWorldData();
                if (worldData == null || worldData.plotHappened == null)
                {
                    LoggerManager.Warning("  CheckPlotHappened查询: WorldData或plotHappened为空");
                    return "0";
                }
                return worldData.plotHappened.ContainsKey(plotID) ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  CheckPlotHappened查询({plotID}) 失败: {e.Message}");
                return "0";
            }
        }
    }
}
