using Il2Cpp;
using Il2CppInterop.Runtime;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 将指定剧情数据库加入剧情队列，并为本次加入队列的剧情替换 plotCallFuc。
    /// 只修改克隆后的 PlotData，不影响 GameDataController.PlotDataBase 中的原始剧情数据。
    /// 格式: AddPlotDataBase*剧情ID#plotCallFuc函数名#plotCallFuc参数(可选)
    /// </summary>
    [SpePlotFuc("AddPlotDataBase")]
    public static class SpePlotFucAddPlotDataBase
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 2 || string.IsNullOrWhiteSpace(fucParams[0]) || string.IsNullOrWhiteSpace(fucParams[1]))
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*剧情ID#plotCallFuc函数名#plotCallFuc参数(可选)]");
                return;
            }

            if (plotController == null)
            {
                LoggerManager.Error($"{fucName}: PlotController实例不存在");
                return;
            }

            if (plotController.plotQueue == null)
            {
                LoggerManager.Error($"{fucName}: plotQueue为空，无法加入剧情队列");
                return;
            }

            if (!CommonHandlers.TryResolvePlotDataBaseId(fucParams[0], out int plotID))
            {
                LoggerManager.Warning($"{fucName}: 无法解析剧情ID [{fucParams[0]}]");
                return;
            }

            Il2CppSystem.Collections.Generic.Dictionary<int, PlotData> plotDataBase = GameDataController._instance?.PlotDataBase;
            if (!CommonHandlers.TryGetPlotDataByPlotId(plotDataBase, plotID, out PlotData plotData))
            {
                LoggerManager.Warning($"{fucName}: 剧情ID不存在 [{plotID}]");
                return;
            }

            PlotData clonedPlot = plotData.Clone().Cast<PlotData>();
            if (clonedPlot == null)
            {
                LoggerManager.Error($"{fucName}: 剧情ID [{plotID}] 克隆失败");
                return;
            }

            string plotCallFucName = fucParams[1].Trim();
            string plotCallFuc = plotCallFucName;
            if (fucParams.Length >= 3 && !string.IsNullOrWhiteSpace(fucParams[2]))
            {
                plotCallFuc = $"{plotCallFucName};{fucParams[2]}";
            }

            clonedPlot.plotCallFuc = plotCallFuc;
            plotController.plotQueue.Add(clonedPlot);

            LoggerManager.Debug($"{fucName}: 已加入剧情队列 plotID={plotID}, plotName={clonedPlot.plotName}, plotCallFuc=\"{plotCallFuc}\"");
        }

    }
}
