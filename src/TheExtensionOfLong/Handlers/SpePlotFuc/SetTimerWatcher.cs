using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 设置定时器在传闻界面的显示状态。
    /// 具体 watcher 保存、EventData 创建和同步逻辑统一委托给 TimerManager。
    /// 格式: SetTimerWatcher*定时器ID#是否显示#标题(显示时必填)#内容(显示时必填)#稀有等级(可选,默认0)
    /// </summary>
    [SpePlotFuc("SetTimerWatcher")]
    public static class SpePlotFucSetTimerWatcher
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            TimerManager.HandleSetTimerWatcher(plotController, fucName, fucParams);
        }
    }
}
