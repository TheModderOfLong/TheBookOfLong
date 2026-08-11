using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 设置触发器启用状态。
    /// 格式: SetTriggerEnabled*编号#1/0/true/false
    /// </summary>
    [SpePlotFuc("SetTriggerEnabled")]
    public static class SpePlotFucSetTriggerEnabled
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams == null || fucParams.Length < 2)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*编号#1/0/true/false]");
                return;
            }

            string id = fucParams[0];
            if (string.IsNullOrWhiteSpace(id))
            {
                LoggerManager.Warning($"{fucName}: 触发器编号不能为空");
                return;
            }

            string enabledRaw = fucParams[1];
            string normalized = (enabledRaw ?? "").Trim();
            if (!normalized.Equals("1") &&
                !normalized.Equals("0") &&
                !normalized.Equals("true", System.StringComparison.OrdinalIgnoreCase) &&
                !normalized.Equals("false", System.StringComparison.OrdinalIgnoreCase))
            {
                LoggerManager.Warning($"{fucName}: 启用状态无效 {enabledRaw}，仅支持 1/0/true/false");
                return;
            }

            bool enabled = CommonHandlers.ParseBool(normalized, false);
            TriggerStateManager.SetEnabled(id, enabled);
        }
    }
}
