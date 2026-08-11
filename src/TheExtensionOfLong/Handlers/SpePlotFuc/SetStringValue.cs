using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 设置剧情事件日志中的字符串变量
    /// 格式: SetStringValue*Key#Value
    ///   Value为空字符串时删除Key
    /// </summary>
    [SpePlotFuc("SetStringValue")]
    public static class SpePlotFucSetStringValue
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 2)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*Key#Value]");
                return;
            }

            string key = fucParams[0];
            string value = fucParams[1];

            if (string.IsNullOrWhiteSpace(key))
            {
                LoggerManager.Warning($"{fucName}: Key不能为空或空白");
                return;
            }

            GameController instance = GameController.Instance;
            if (instance == null)
            {
                LoggerManager.Error($"{fucName}: GameController实例不存在，无法调用此指令");
                return;
            }

            WorldData worldData = instance.worldData;
            if (worldData == null)
            {
                LoggerManager.Error($"{fucName}: WorldData实例不存在，无法调用此指令");
                return;
            }

            PlotEventLogData plotEventLogData = worldData.PlotEventLog;
            if (plotEventLogData == null)
            {
                LoggerManager.Error($"{fucName}: plotEventLogData实例不存在，无法调用此指令");
                return;
            }

            if (string.IsNullOrEmpty(value))
            {
                plotEventLogData.Set(key, null);
                LoggerManager.Debug($"{fucName}: 已删除字符串变量: {key}");
            }
            else
            {
                plotEventLogData.Set(key, value);
                LoggerManager.Debug($"{fucName}: 已设置字符串变量: {key}={value}");
            }
        }
    }
}
