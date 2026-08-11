using Il2Cpp;
using System;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 从 PlotEventLogData 中读取整数变量。
    /// 格式: [$GetIntVal:Key$]
    /// </summary>
    [ConditionQuery("GetIntVal")]
    public static class QueryGetIntVal
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            if (parts.Length < 2) return "0";
            string key = parts[1];
            PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
            if (logData == null) return "0";
            if (!logData.HaveKey(key)) return "0";
            try
            {
                return logData.GetInt(key).ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  GetIntVal查询({key}) 失败: {e.Message}");
                return "0";
            }
        }
    }
}
