using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 以函数子程序方式调用指定数据库剧情，只执行 plotCallFuc 和各 SinglePlotData.clickCallFuc。
    /// 不播放剧情、不显示 UI、不主动修改 nowPlot/nowSinglePlot/plotHappen/plotHappened。
    /// 格式: CallPlotDataBaseFuc*剧情ID
    /// </summary>
    [SpePlotFuc("CallPlotDataBaseFuc")]
    public static class SpePlotFucCallPlotDataBaseFuc
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            CallPlotDataBaseFucHandler.Handle(plotController, fucName, fucParams);
        }
    }
}
