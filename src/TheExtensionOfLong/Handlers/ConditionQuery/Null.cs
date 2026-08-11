using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 空值查询，返回空字符串。
    /// 格式: [$null$]
    /// </summary>
    [ConditionQuery("null")]
    public static class QueryNull
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            return "";
        }
    }
}
