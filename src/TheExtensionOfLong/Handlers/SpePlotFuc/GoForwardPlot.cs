using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppSystem.Collections.Generic;
using System.Reflection;
using System;

namespace TheExtensionOfLong
{
    [SpePlotFuc("GoForwardPlot")]
    public static class SpePlotFucGoForwardPlot
    {
        /// <summary>
        /// 尝试调用"GoForwardPlot"功能，将当前Plot向前推进 Step 个单对话
        /// Step=1 等同于 GoNextPlot 的推进目标，Step=2 表示跳过下一个并显示再后一个
        /// 格式: GoForwardPlot*Step
        /// </summary>
        public static void TryCall(PlotController __instance, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 1)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*Step]");
                return;
            }

            string stepStr = fucParams[0];
            if (!int.TryParse(stepStr, out int step))
            {
                LoggerManager.Warning($"{fucName}: Step不是有效整数 - {stepStr}");
                return;
            }

            PlotData nowPlot = __instance.nowPlot;
            if (nowPlot == null)
            {
                return;
            }

            List<SinglePlotData> plotDatas = nowPlot.plotDatas;
            if (plotDatas == null)
            {
                return;
            }

            if (step < 1 || step >= plotDatas.Count)
            {
                LoggerManager.Debug($"{fucName}: Step越界 {step}，范围[1,{plotDatas.Count})，结束当前Plot");
                plotDatas.Clear();
                __instance.nowSinglePlot = null;
                __instance.HideInteractUI();
                return;
            }

            // PlotController.GoNextPlot 会始终移除 plotDatas[0] 再显示新的 plotDatas[0]。
            // 本指令与 GoNextPlot 相对：Step=1 时移除当前节点并显示下一个节点，
            // Step=N 时移除当前节点及其后 N-1 个节点，让目标节点成为当前队列头。
            for (int i = 0; i < step; i++)
            {
                plotDatas.RemoveAt(0);
            }

            __instance.ShowSinglePlot(plotDatas[0]);
        }
    }
}
