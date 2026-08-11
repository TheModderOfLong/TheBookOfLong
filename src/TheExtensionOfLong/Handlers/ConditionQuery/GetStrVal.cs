using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 从 PlotEventLogData 中读取字符串变量。
    /// 格式: [$GetStrVal:Key$]
    /// </summary>
    [ConditionQuery("GetStrVal")]
    public static class QueryGetStrVal
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            if (parts.Length < 2) return "";
            string key = parts[1];
            PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
            return logData != null && logData.HaveKey(key) ? logData.Get(key) : "";
        }
    }
}
