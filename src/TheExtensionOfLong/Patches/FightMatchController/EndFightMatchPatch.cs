using System;
using HarmonyLib;
using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 对 FightMatchController.EndFightMatch 的 Postfix 补丁
    ///
    /// 原方法通过 PlotController._instance.gameObject.SendMessage(endMatchCallPlot) 发送消息，
    /// 不支持带参数调用。当 endMatchCallPlot 格式为 "函数名;参数" 时，原 SendMessage 会因
    /// 找不到名为 "函数名;参数" 的方法而静默失败。
    ///
    /// 本补丁扩展支持：当 endMatchCallPlot 按 ";" 分隔后第0个参数为 "ChangePlotDataBase" 时，
    /// 以 SendMessage(函数名, 参数) 的方式调用，使比赛结束时可带参数切换剧情数据库。
    ///
    /// 示例: endMatchCallPlot = "ChangePlotDataBase;101"
    ///       → SendMessage("ChangePlotDataBase", "101")
    /// </summary>
    [HarmonyPatch(typeof(FightMatchController), "EndFightMatch")]
    public static class EndFightMatchPatch
    {
        [HarmonyPostfix]
        public static void EndFightMatchPostfix(FightMatchController __instance)
        {
            try
            {
                if (__instance == null) return;

                PlotController plotController = PlotController._instance;
                if (plotController == null)
                {
                    LoggerManager.Warning("FightMatchController.EndFightMatch Postfix: PlotController实例为空，无法发送SendMessage");
                    return;
                }

                string endMatchCallPlot = __instance.endMatchCallPlot;
                if (string.IsNullOrEmpty(endMatchCallPlot)) return;
                if (!endMatchCallPlot.Contains(";")) return;

                string resolved = ConditionQueryHandlers.ResolveAllCommands(plotController, endMatchCallPlot);
                if (resolved != endMatchCallPlot)
                {
                    LoggerManager.Debug($"FightMatchController.SendMessage解析: \"{endMatchCallPlot}\" → \"{resolved}\"");
                    endMatchCallPlot = resolved;
                }

                string[] parts = PlotCommandHandler.SplitRespectingBraces(endMatchCallPlot, ';');
                if (parts.Length < 2) return;

                if (parts[0] != "ChangePlotDataBase") return;

                string methodName = parts[0];
                string methodArg = PlotCommandHandler.StripParens(parts[1]);
                plotController.gameObject.SendMessage(methodName, methodArg);
                LoggerManager.Debug($"FightMatchController.EndFightMatch Postfix: SendMessage(\"{methodName}\", \"{methodArg}\")");
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"FightMatchController.EndFightMatch Postfix 异常: {ex}");
            }
        }
    }
}
