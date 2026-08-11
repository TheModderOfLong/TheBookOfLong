using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppDG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 对 PlotController.ShowSinglePlot() 的 HarmonyPrefix 补丁
    /// 
    /// 当 targetPlot.plotText 包含 "NOTEXT" 标记时，跳过文本显示（打字机动画、立绘、语音、背景图等），
    /// 直接调用 ChangeNextPlot() 以正常执行其 clickCallFuc 回调。
    /// 
    /// 这样可以实现"纯回调"型剧情节点——无需显示任何文字，仅执行回调函数后自动推进到下一剧情。
    /// 
    /// 前置逻辑（与原方法一致，必须执行）：
    ///   1. 停止隐藏动画
    ///   2. 记录 nowSinglePlot
    ///   3. 解析 sourceInteractHero / targetInteractHero
    ///   4. 清空剧情文字
    /// </summary>
    [HarmonyPatch(typeof(PlotController), "ShowSinglePlot")]
    public static class ShowSinglePlotSkipEmptyTextPatch
    {
        [HarmonyPrefix]
        public static bool ShowSinglePlotPrefix(PlotController __instance, SinglePlotData targetPlot)
        {
            if (__instance == null || targetPlot == null) return true;

            // plotText 不包含 "NOTEXT" 标记则走原方法
            if (targetPlot.plotText == null || !targetPlot.plotText.Contains("NOTEXT"))
                return true;

            try
            {
                // 1. 如果正在隐藏UI，停止隐藏动画
                if (__instance.isHiding)
                {
                    __instance.isHiding = false;
                    DOTween.Kill("HidePlotPanel", false);
                }

                // 2. 记录当前SinglePlotData
                __instance.nowSinglePlot = targetPlot;

                // 3. 根据plotSource/plotTarget解析sourceInteractHero和targetInteractHero
                __instance.sourceInteractHero = __instance.GetHeroData(targetPlot.plotSource, targetPlot.sourceName, __instance.sourceInteractHero);
                __instance.targetInteractHero = __instance.GetHeroData(targetPlot.plotTarget, targetPlot.targetName, __instance.targetInteractHero);

                // 4. 清空剧情文字
                // Transform plotTextTrans = __instance.plotPanel.transform.Find("PlotTextBack").Find("PlotText");
                // Text plotTextComp = plotTextTrans.GetComponent<Text>();
                // LTLocalization.SetText(plotTextComp, "");

                // plotText 包含 "NOTEXT" 标记，跳过显示，直接推进
                //var currentPlotDatas = __instance.nowPlot?.plotDatas;
                //LoggerManager.Debug($"ShowSinglePlot: plotText为空，跳过文本显示并调用函数 " +
                //    $"(plotDatas.Count={currentPlotDatas?.Count ?? -1}, " +
                //    $"plotText={Truncate(targetPlot.plotText, 50)}, " +
                //    $"clickCallFuc={Truncate(targetPlot.clickCallFuc, 80)})");
                __instance.ChangeNextPlot();
                return false; // 跳过原方法
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"ShowSinglePlotPatch 异常: {ex}");
                return true; // 异常时回退到原方法
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
