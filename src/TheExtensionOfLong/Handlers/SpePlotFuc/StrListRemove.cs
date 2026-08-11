using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 从字符串列表移除元素。
    /// 格式: StrListRemove*元素#列表ID#移除模式(可选)
    /// </summary>
    [SpePlotFuc("StrListRemove")]
    public static class SpePlotFucStrListRemove
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            StringListManager.HandleRemove(plotController, fucName, fucParams);
        }
    }
}
