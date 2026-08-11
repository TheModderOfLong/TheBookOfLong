using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 清空字符串列表。
    /// 格式: StrListClear*列表ID
    /// </summary>
    [SpePlotFuc("StrListClear")]
    public static class SpePlotFucStrListClear
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            StringListManager.HandleClear(plotController, fucName, fucParams);
        }
    }
}
