using System;
using HarmonyLib;
using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 对 WorldEventController.CreateWorldEvent(WorldEventDataBase) 的 Postfix 补丁
    ///
    /// 原方法在 eventRandomArea==2 时将 startCallPlot 作为无参方法名通过
    /// PlotController._instance.SendMessage(startCallPlot) 发送，不支持带参数调用。
    ///
    /// 本补丁扩展支持：startCallPlot 首段为 "CALL_ALL_FUC" 时，用 "|" 分隔多个回调，
    /// 每个回调用 ";" 分隔方法名和参数（参考 PlotControllerChangeNextPlotPatch.cs）
    /// 示例: "CALL_ALL_FUC|ChangePlotDataBase;101|SomeOtherFuc;arg1|FuncNoParam"
    ///       → SendMessage("ChangePlotDataBase", "101")
    ///       → SendMessage("SomeOtherFuc", "arg1")
    ///       → SendMessage("FuncNoParam")
    ///
    /// 分隔逻辑参考 ChangeNextPlotPatch，由 PlotCommandHandler.ExecuteSendMessageCallback 统一处理。
    /// </summary>
    [HarmonyPatch(typeof(WorldEventController), "CreateWorldEvent", new[] { typeof(WorldEventDataBase) })]
    public static class CreateWorldEventPatch
    {
        [HarmonyPostfix]
        public static void CreateWorldEventPostfix(
            WorldEventDataBase targetWorldEventDataBase)
        {
            try
            {
                if (targetWorldEventDataBase == null) return;

                string startCallPlot = targetWorldEventDataBase.startCallPlot;
                if (string.IsNullOrEmpty(startCallPlot)) return;

                PlotController plotController = PlotController._instance;
                if (plotController == null)
                {
                    LoggerManager.Warning("WorldEventController.CreateWorldEvent Postfix: PlotController实例为空，无法发送SendMessage");
                    return;
                }

                // 先按 clickCallFuc 规则分割，首段为 CALL_ALL_FUC 则进入多函数模式
                string[] callBacks = PlotCommandHandler.SplitClickCallFucCallbacks(startCallPlot);

                if (callBacks.Length == 0 || callBacks[0] != "CALL_ALL_FUC") return;

                // 多函数依次调用模式
                // 格式: CALL_ALL_FUC|func1;param1|func2;param2|func3
                // callBacks[0] == "CALL_ALL_FUC"，跳过
                for (int i = 1; i < callBacks.Length; i++)
                {
                    PlotCommandHandler.ExecuteSendMessageCallback(plotController, callBacks[i], $"WorldEventController.CreateWorldEvent[{i}]");
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"WorldEventController.CreateWorldEvent Postfix 异常: {ex}");
            }
        }
    }
}
