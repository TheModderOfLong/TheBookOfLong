using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 从 PlotEventLogData 中读取上一次通过 GetRandomIntVal 生成的随机整数。
    /// 格式: [$GetLastRandomIntVal$]
    /// </summary>
    [ConditionQuery("GetLastRandomIntVal", Cacheable = false)]
    public static class QueryGetLastRandomIntVal
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
            if (logData != null && logData.HaveKey(QueryGetRandomIntVal.LastRandomIntValKey))
                return logData.Get(QueryGetRandomIntVal.LastRandomIntValKey) ?? "";
            return "";
        }
    }
}
