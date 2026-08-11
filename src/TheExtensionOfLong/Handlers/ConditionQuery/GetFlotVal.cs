using Il2Cpp;
using System;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 从 PlotEventLogData 中读取浮点数变量。
    /// 格式: [$GetFlotVal:Key$]
    /// </summary>
    [ConditionQuery("GetFloatVal")]
    [ConditionQuery("GetFlotVal")]
    public static class QueryGetFlotVal
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            if (parts.Length < 2) return "0.0";
            string key = parts[1];
            PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
            if (logData == null) return "0.0";
            string rawVal = logData.HaveKey(key) ? logData.Get(key) : "0.0";
            if (double.TryParse(rawVal, out double numVal))
                return numVal.ToString("G");
            return rawVal;
        }
    }
}
