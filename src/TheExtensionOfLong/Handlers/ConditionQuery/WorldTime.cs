using Il2Cpp;

namespace TheExtensionOfLong
{
    internal static class QueryWorldTimeHelper
    {
        public static TimeData GetCurrentWorldTime()
        {
            TimeData time = CommonHandlers.GetWorldData()?.worldTime;
            return time ?? TimerManager.FromAbsDay(TimerManager.GetCurrentAbsDay());
        }
    }

    /// <summary>
    /// 查询当前游戏年份。
    /// 格式: [$GetWorldTimeYear$]
    /// </summary>
    [ConditionQuery("GetWorldTimeYear")]
    public static class QueryGetWorldTimeYear
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            return QueryWorldTimeHelper.GetCurrentWorldTime().year.ToString();
        }
    }

    /// <summary>
    /// 查询当前游戏月份。
    /// 格式: [$GetWorldTimeMonth$]
    /// </summary>
    [ConditionQuery("GetWorldTimeMonth")]
    public static class QueryGetWorldTimeMonth
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            return QueryWorldTimeHelper.GetCurrentWorldTime().month.ToString();
        }
    }

    /// <summary>
    /// 查询当前游戏日期。
    /// 格式: [$GetWorldTimeDay$]
    /// </summary>
    [ConditionQuery("GetWorldTimeDay")]
    public static class QueryGetWorldTimeDay
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            return QueryWorldTimeHelper.GetCurrentWorldTime().day.ToString();
        }
    }

    /// <summary>
    /// 查询距离下月的天数。
    /// 格式: [$GetNextMonthDays$]
    /// </summary>
    [ConditionQuery("GetNextMonthDays")]
    public static class QueryGetNextMonthDays
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            TimeData time = QueryWorldTimeHelper.GetCurrentWorldTime();
            return (31 - time.day).ToString();
        }
    }

    /// <summary>
    /// 查询距离下年的天数。
    /// 格式: [$GetNextYearDays$]
    /// </summary>
    [ConditionQuery("GetNextYearDays")]
    public static class QueryGetNextYearDays
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            TimeData time = QueryWorldTimeHelper.GetCurrentWorldTime();
            int dayOfYear = (time.month - 1) * 30 + (time.day - 1);
            return (360 - dayOfYear).ToString();
        }
    }
}
