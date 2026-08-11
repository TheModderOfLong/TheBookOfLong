using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 检查剧情事件日志中是否存在指定Key
    /// 格式: HaveStringKeyChangePlotDataBase*TruePlotDataBaseId-FalsePlotDataBaseId(可选)#key
    /// </summary>
    [SpePlotFuc("HaveStringKeyChangePlotDataBase")]
    public static class SpePlotFucHaveStringKeyChangePlotDataBase
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 2)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*TruePlotDataBaseId-FalsePlotDataBaseId(可选)#key]");
                return;
            }

            string[] plotParams = fucParams[0].Split('-');
            if (plotParams.Length < 1 || string.IsNullOrWhiteSpace(plotParams[0]))
            {
                LoggerManager.Warning($"{fucName}: TruePlotDataBaseId不能为空");
                return;
            }

            string key = fucParams[1];
            if (string.IsNullOrWhiteSpace(key))
            {
                LoggerManager.Warning($"{fucName}: Key不能为空或空白");
                if (plotParams.Length >= 2 && !string.IsNullOrWhiteSpace(plotParams[1]))
                    plotController.ChangePlotDataBase(plotParams[1]);
                return;
            }

            PlotEventLogData plotEventLogData = CommonHandlers.GetPlotEventLogData();
            if (plotEventLogData == null)
            {
                LoggerManager.Error($"{fucName}: PlotEventLogData实例不存在，无法调用此指令");
                if (plotParams.Length >= 2 && !string.IsNullOrWhiteSpace(plotParams[1]))
                    plotController.ChangePlotDataBase(plotParams[1]);
                return;
            }

            string value = plotEventLogData.HaveKey(key) ? plotEventLogData.Get(key) : "";
            bool hasKey = !string.IsNullOrEmpty(value);

            LoggerManager.Debug($"{fucName}: Key={key}, 存在={hasKey}");

            if (hasKey)
            {
                plotController.ChangePlotDataBase(plotParams[0]);
            }
            else if (plotParams.Length >= 2 && !string.IsNullOrWhiteSpace(plotParams[1]))
            {
                plotController.ChangePlotDataBase(plotParams[1]);
            }
        }
    }
}
