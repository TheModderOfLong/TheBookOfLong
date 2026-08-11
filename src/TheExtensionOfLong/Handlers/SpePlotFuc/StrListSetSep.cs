using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 设置字符串列表展示分隔符。
    /// 格式: StrListSetSep*列表ID#新分隔符
    /// </summary>
    [SpePlotFuc("StrListSetSep")]
    public static class SpePlotFucStrListSetSep
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            StringListManager.HandleSetSeparator(plotController, fucName, fucParams);
        }
    }
}
