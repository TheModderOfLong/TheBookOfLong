using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 查询指定定时器字段。
    /// 格式: [$Timer:字段名:定时器ID(可选)$]
    /// </summary>
    [ConditionQuery("Timer")]
    public static class QueryTimer
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            if (parts.Length < 2)
            {
                LoggerManager.Warning("  Timer查询: 参数不足，格式 [$Timer:字段名:定时器ID(可选)$]");
                return "";
            }

            string fieldName = parts[1];
            string timerRef = parts.Length > 2 ? parts[2] : "";
            return TimerManager.QueryTimer(fieldName, timerRef);
        }
    }
}
