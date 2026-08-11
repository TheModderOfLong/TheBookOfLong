using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 查询当前游戏累计天数。
    /// 格式: [$GetWorldTimeTotalDays$]
    /// 兼容旧格式: [$NowAbsDay$]
    /// </summary>
    [ConditionQuery("GetWorldTimeTotalDays")]
    [ConditionQuery("NowAbsDay")]
    public static class QueryNowAbsDay
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            return TimerManager.GetCurrentAbsDay().ToString();
        }
    }
}
