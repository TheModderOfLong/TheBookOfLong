using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 查询当前定时器数量。
    /// 格式: [$GetTimerCount$]
    /// 兼容旧格式: [$TimerCount$]
    /// </summary>
    [ConditionQuery("GetTimerCount")]
    [ConditionQuery("TimerCount")]
    public static class QueryTimerCount
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            return TimerManager.GetTimerCount().ToString();
        }
    }
}
