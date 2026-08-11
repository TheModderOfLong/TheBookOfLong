using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 向字符串列表添加元素。
    /// 格式: StrListAdd*元素#列表ID#添加模式(可选)
    /// </summary>
    [SpePlotFuc("StrListAdd")]
    public static class SpePlotFucStrListAdd
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            StringListManager.HandleAdd(plotController, fucName, fucParams);
        }
    }
}
