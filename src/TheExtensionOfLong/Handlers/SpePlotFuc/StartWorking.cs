using Il2Cpp;
using System;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 启动本体 WorkingUI 工作流程。
    /// 格式: StartWorking*工作名称#工作天数#每日回调函数(可选)#每日回调参数(可选)#完成回调函数(可选)#完成回调参数(可选)#禁止取消(可选)
    /// 示例: StartWorking*闭关#3#SpePlotFuc#((ShowTextOnMouse*修炼一日))#SpePlotFuc#((ShowTextOnMouse*闭关完成))#true
    /// </summary>
    [SpePlotFuc("StartWorking")]
    public static class SpePlotFucStartWorking
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams == null || fucParams.Length < 2)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*工作名称#工作天数#每日回调函数(可选)#每日回调参数(可选)#完成回调函数(可选)#完成回调参数(可选)#禁止取消(可选)]");
                return;
            }

            string workName = fucParams[0] ?? "";
            if (!int.TryParse(fucParams[1], out int dayNum) || dayNum <= 0)
            {
                LoggerManager.Warning($"{fucName}: 工作天数无效，必须为正整数，当前值=\"{fucParams[1]}\"");
                return;
            }

            string callFuc = GetStringArg(fucParams, 2);
            string callFucParam = GetStringArg(fucParams, 3);
            string finishCallFuc = GetStringArg(fucParams, 4);
            string finishCallFucParam = GetStringArg(fucParams, 5);
            bool noCancel = CommonHandlers.GetBoolArg(fucParams, 6, false);

            WorkingUIController controller = WorkingUIController.Instance;
            if (controller == null)
            {
                LoggerManager.Error($"{fucName}: WorkingUIController实例不存在，无法启动工作流程");
                return;
            }

            try
            {
                controller.StartWorking(workName, dayNum, callFuc, callFucParam, finishCallFuc, finishCallFucParam, noCancel);
                LoggerManager.Debug($"{fucName}: 已启动工作流程 workName=\"{workName}\", dayNum={dayNum}, callFuc=\"{callFuc}\", finishCallFuc=\"{finishCallFuc}\", noCancel={noCancel}");
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"{fucName}: 启动工作流程失败 - {ex}");
            }
        }

        private static string GetStringArg(string[] args, int index)
        {
            return args != null && args.Length > index ? args[index] ?? "" : "";
        }
    }
}
