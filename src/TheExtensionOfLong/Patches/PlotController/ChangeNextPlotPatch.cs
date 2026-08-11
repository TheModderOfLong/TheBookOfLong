using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 对 PlotController.ChangeNextPlot() 的全面替换 Patch
    ///
    /// 原方法流程：
    ///   1. oldSinglePlot = nowSinglePlot
    ///   2. 获取 firstPlot = nowPlot.plotDatas[0]
    ///   3. 若 clickCallFuc 非空：
    ///      a. "ScreenBlack" → ScreenBlack() 并返回
    ///      b. 用 '|' 分割多个回调，每个用 ';' 分割方法名和参数
    ///      c. 通过 SendMessage 执行，每次检测剧情是否切换（oldSinglePlot != nowSinglePlot）
    ///      d. 若发生切换，noAutoJump=true → ScreenBlack() 并返回
    ///   4. 若 noAutoJump=false → GoNextPlot()
    ///
    /// 注意：已有的 ChangeNextPlotQueryResolvePatch 仅做查询指令解析（Prefix修改、Postfix恢复），
    ///       本 Patch 完全接管原方法，两者同时存在时 ChangeNextPlotQueryResolvePatch 的 Prefix
    ///       会先执行（解析查询指令），然后本 Patch 的 Prefix 执行（return false 阻止原方法），
    ///       原 ChangeNextPlot 不会执行。
    ///       如需整合，可将查询解析逻辑合并到本 Patch 中。
    /// </summary>
    [HarmonyPatch(typeof(PlotController), "ChangeNextPlot")]
    public static class ChangeNextPlotPatch
    {
        [HarmonyPrefix]
        public static bool ChangeNextPlotPrefix(PlotController __instance)
        {
            try
            {
                // LoggerManager.Debug($"ChangeNextPlot 调用栈: {Environment.StackTrace}");

                // 1. 保存当前 SinglePlotData（用局部变量，防止嵌套调用覆盖实例字段）
                SinglePlotData savedSinglePlot = __instance.nowSinglePlot;
                __instance.oldSinglePlot = savedSinglePlot;

                // 2. 获取 firstPlot
                var nowPlot = __instance.nowPlot;
                if (nowPlot == null) return false;
                var plotDatas = nowPlot.plotDatas;
                if (plotDatas == null || plotDatas.Count == 0) return false;
                SinglePlotData firstPlot = plotDatas[0];
                if (firstPlot == null) return false;

                // 3. 读取 noAutoJump 标志
                bool noAutoJump = firstPlot.noAutoJump;

                // 4. 处理 clickCallFuc
                string clickCallFuc = firstPlot.clickCallFuc;
                if (!string.IsNullOrEmpty(clickCallFuc))
                {
                    // 4a. 特殊值 "ScreenBlack"
                    if (clickCallFuc == "ScreenBlack")
                    {
                       __instance.ScreenBlack();
                       return false;
                    }

                    // 4b. 执行 clickCallFuc 多回调，通用解析逻辑统一处理 | / 换行 / ; / (())。
                    if (PlotCommandHandler.ExecuteSendMessageCallbacks(__instance, clickCallFuc, true, "ChangeNextPlot"))
                    {
                        noAutoJump = true;
                    }

                    // 4c. SendMessage 导致剧情切换 → 黑屏过渡
                    if (noAutoJump)
                    {
                        // __instance.ScreenBlack();
                        return false;
                    }
                }

                //var currentPlotDatas = __instance.nowPlot?.plotDatas;
                //LoggerManager.Debug($"ChangeNextPlot: plotText为空，跳过文本显示并调用函数 " +
                //    $"(plotDatas.Count={currentPlotDatas?.Count ?? -1}, " +
                //    $"plotText={Truncate(__instance.nowSinglePlot?.plotText, 50)}, " +
                //    $"clickCallFuc={Truncate(__instance.nowSinglePlot?.clickCallFuc, 80)})");

                // 5. noAutoJump == false 时自动推进（再次检查 Count，防止嵌套调用已清空 plotDatas）
                if (!noAutoJump)
                {
                    __instance.GoNextPlot();
                    //var currentDatas = __instance.nowPlot?.plotDatas;
                    //if (currentDatas != null && currentDatas.Count > 0)
                    //{
                    //    __instance.GoNextPlot();
                    //}
                    //else
                    //{
                    //    __instance.HideInteractUI();
                    //}
                }

                return false; // 阻止原方法执行
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"ChangeNextPlotPatch 异常: {ex}");
                var currentDatas = __instance.nowPlot?.plotDatas;
                if (currentDatas != null && currentDatas.Count > 0)
                {
                    __instance.GoNextPlot();
                }
                else
                {
                    __instance.HideInteractUI();
                }
                return false; // 异常时不放行原方法，避免在已损坏的状态上二次崩溃
            }
        }

        //private static string Truncate(string text, int maxLength)
        //{
        //    if (text == null) return "null";
        //    if (text.Length <= maxLength) return text;
        //    return text.Substring(0, maxLength) + $"...(总长{text.Length})";
        //}
    }
}
