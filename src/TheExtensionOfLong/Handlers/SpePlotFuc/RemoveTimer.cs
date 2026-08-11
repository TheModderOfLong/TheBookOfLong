using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 移除指定存档定时器。
    /// 具体查找、删除和持久化逻辑统一委托给 TimerManager。
    /// 格式: RemoveTimer*定时器ID
    /// </summary>
    [SpePlotFuc("RemoveTimer")]
    public static class SpePlotFucRemoveTimer
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            TimerManager.HandleRemoveTimer(plotController, fucName, fucParams);
        }
    }
}
