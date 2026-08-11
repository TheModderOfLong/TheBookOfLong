using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 设置查询指令替换参数，SUBARG关键字在指令解析时替换为实际值
    /// 格式: SetSubArg*设置值
    ///   设置值为空时清除替换参数
    /// 示例: SetSubArg*100          → SUBARG替换为"100"
    ///       SetSubArg*小白         → SUBARG替换为"小白"
    ///       SetSubArg*             → 清除替换参数
    /// </summary>
    [SpePlotFuc("SetSubArg")]
    public static class SpePlotFucSetSubArg
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            PlotEventLogData plotEventLogData = CommonHandlers.GetPlotEventLogData();
            if (plotEventLogData == null)
            {
                LoggerManager.Error($"{fucName}: PlotEventLogData实例不存在");
                return;
            }

            string value = fucParams.Length > 0 ? fucParams[0] : "";

            if (string.IsNullOrEmpty(value))
            {
                plotEventLogData.Set("SUBARG", null);
                LoggerManager.Debug($"{fucName}: 已删除替换参数 SUBARG");
            }
            else
            {
                plotEventLogData.Set("SUBARG", value);
                LoggerManager.Debug($"{fucName}: 已设置替换参数 SUBARG={value}");
            }
        }
    }
}
