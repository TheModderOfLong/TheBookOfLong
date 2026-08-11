using Il2Cpp;
using System;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 获取指定闭区间内的随机整数，并记录到存档变量 LAST_RANDOM_INT_VAL。
    /// 格式: [$GetRandomIntVal:下限:上限$]
    /// </summary>
    [ConditionQuery("GetRandomIntVal", Cacheable = false)]
    public static class QueryGetRandomIntVal
    {
        public const string LastRandomIntValKey = "LAST_RANDOM_INT_VAL";

        private static readonly Random Random = new Random();

        public static string TryQuery(PlotController plotController, string[] parts)
        {
            if (parts.Length < 3)
            {
                LoggerManager.Warning("  GetRandomIntVal查询: 参数不足，格式 [GetRandomIntVal:下限:上限]");
                return "";
            }
            if (!int.TryParse(parts[1], out int minVal))
            {
                LoggerManager.Warning($"  GetRandomIntVal查询: 下限\"{parts[1]}\"不是有效整数");
                return "";
            }
            if (!int.TryParse(parts[2], out int maxVal))
            {
                LoggerManager.Warning($"  GetRandomIntVal查询: 上限\"{parts[2]}\"不是有效整数");
                return "";
            }
            if (minVal > maxVal)
            {
                LoggerManager.Warning($"  GetRandomIntVal查询: 下限({minVal}) > 上限({maxVal})，已自动交换");
                int tmp = minVal; minVal = maxVal; maxVal = tmp;
            }
            try
            {
                int result = Random.Next(minVal, maxVal + 1);
                PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
                if (logData != null)
                {
                    logData.Set(LastRandomIntValKey, result.ToString());
                }
                else
                {
                    LoggerManager.Warning("  GetRandomIntVal查询: PlotEventLogData实例不存在，无法记录上次随机数");
                }

                return result.ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  GetRandomIntVal查询 失败: {e.Message}");
                return "";
            }
        }
    }
}
