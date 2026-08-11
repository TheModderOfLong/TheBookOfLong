using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 查询当前时间描述文本。
    /// 格式: [$GetWorldTimeDescribe$]
    /// 兼容旧格式: [$NowTimeDescribe$]
    /// </summary>
    [ConditionQuery("GetWorldTimeDescribe")]
    [ConditionQuery("NowTimeDescribe")]
    public static class QueryNowTimeDescribe
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            return TimerManager.FormatAbsDay(TimerManager.GetCurrentAbsDay());
        }
    }
}
