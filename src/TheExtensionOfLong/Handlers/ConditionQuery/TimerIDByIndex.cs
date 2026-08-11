using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 按索引查询定时器 ID。
    /// 格式: [$GetTimerIDByIndex:索引$]
    /// 兼容旧格式: [$TimerIDByIndex:索引$]
    /// </summary>
    [ConditionQuery("GetTimerIDByIndex")]
    [ConditionQuery("TimerIDByIndex")]
    public static class QueryTimerIDByIndex
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            if (parts.Length < 2)
            {
                LoggerManager.Warning($"  {parts[0]}查询: 参数不足，格式 [${parts[0]}:索引$]");
                return "";
            }

            if (!int.TryParse(parts[1], out int index))
            {
                LoggerManager.Warning($"  {parts[0]}查询: 索引无效 {parts[1]}");
                return "";
            }

            TimerManager.TimerData timer = TimerManager.GetTimerByIndex(index);
            return timer != null ? timer.Id ?? "" : "";
        }
    }
}
