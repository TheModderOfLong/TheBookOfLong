using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 直接获取当前替换参数 SUBARG 的值，等同于 [$GetStrVal:SUBARG$]。
    /// 格式: [$GetSubArg$]
    /// </summary>
    [ConditionQuery("GetSubArg")]
    public static class QueryGetSubArg
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
            if (logData != null && logData.HaveKey("SUBARG"))
                return logData.Get("SUBARG") ?? "";
            return "";
        }
    }
}
