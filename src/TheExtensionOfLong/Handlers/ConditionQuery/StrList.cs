using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 字符串列表查询。
    /// 格式: [$StrList:字段名=参数:列表ID$]
    /// </summary>
    [ConditionQuery("StrList")]
    public static class QueryStrList
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            return StringListManager.Query(plotController, parts);
        }
    }
}
