using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;

namespace TheExtensionOfLong
{
    /// <summary>
    /// PlotTextShowFinished 的 Prefix 补丁
    /// 
    /// 在选项按钮创建前，根据 describe 内嵌的显示条件过滤不满足条件的选项。
    /// 同时解析当前选项批次的扩展前置/互动条件，供按钮刷新与点击兜底使用。
    /// </summary>
    [HarmonyPatch(typeof(PlotController), "PlotTextShowFinished")]
    public static class PlotTextShowFinishedConditionPatch
    {
        /// <summary>
        /// 选项按钮创建前的 Prefix。
        /// 
        /// 这里做两件事：
        /// 1. 按显示条件过滤掉不该出现的选项。
        /// 2. 清理旧的 {{...}} 标记，确保原始按钮文本不会带着条件壳子进入 UI。
        /// 
        /// 之所以放在按钮创建前，是因为这一步还没有真正实例化按钮，
        /// 直接删掉 choices 比事后隐藏按钮更干净。
        /// </summary>
        [HarmonyPrefix]
        public static void PlotTextShowFinishedPrefix(
            PlotController __instance,
            out Dictionary<SinglePlotChoiceData, string> __state)
        {
            __state = null;
            if (__instance == null) return;

            try
            {
                SinglePlotData nowPlot = __instance.nowSinglePlot;
                if (nowPlot == null || nowPlot.choices == null) return;

                var choices = nowPlot.choices;
                int removedCount = 0;
                PlotChoiceMetaDataManager.ClearAll();

                // 反向遍历，安全移除不满足条件的选项
                for (int i = choices.Count - 1; i >= 0; i--)
                {
                    SinglePlotChoiceData choice = choices[i];
                    if (choice == null) continue;

                    PlotChoiceMetaParseResult parseResult = PlotChoiceMetaTagHelper.Parse(choice.describe);
                    PlotChoiceMetaData meta = parseResult.Meta ?? new PlotChoiceMetaData();

                    if (!PlotChoiceMetaDataManager.TryEvaluateCondition(__instance, meta.ShowCondition, "PlotTextShowFinishedPatch", choice.choiceText))
                    {
                        choices.RemoveAt(i);
                        removedCount++;
                        PlotChoiceMetaDataManager.Register(choice, null);
                        continue;
                    }

                    if (parseResult.HasTag && !string.Equals(choice.describe, parseResult.CleanDescribe, StringComparison.Ordinal))
                    {
                        if (__state == null)
                            __state = new Dictionary<SinglePlotChoiceData, string>();
                        __state[choice] = choice.describe;
                        choice.describe = parseResult.CleanDescribe;
                    }

                    PlotChoiceMetaDataManager.Register(choice, meta);
                }

                if (removedCount > 0)
                {
                    LoggerManager.Debug($"PlotTextShowFinishedPatch: 移除了 {removedCount} 个不满足条件的选项");
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"PlotTextShowFinishedPatch: Prefix 异常 - {ex.Message}");
            }
        }

        /// <summary>
        /// 选项按钮创建后的 Postfix。
        /// 
        /// 本体会在这一步把“需要...”和“本月已用”类文本写到按钮上。
        /// 此处复用本体前置条件文本位置，直接显示制作者配置的扩展前置条件说明。
        /// </summary>
        [HarmonyPostfix]
        public static void PlotTextShowFinishedPostfix(
            PlotController __instance,
            Dictionary<SinglePlotChoiceData, string> __state)
        {
            if (__instance == null || __instance.plotPanel == null)
            {
                RestoreChoiceDescribe(__state);
                return;
            }

            try
            {
                Transform interactGrid = __instance.plotPanel.transform.Find("InteractGrid");
                if (interactGrid == null) return;

                for (int i = 0; i < interactGrid.childCount; i++)
                {
                    Transform child = interactGrid.GetChild(i);
                    if (child == null) continue;

                    PlotInteractController interactController = child.GetComponent<PlotInteractController>();
                    if (interactController == null || interactController.choiceData == null) continue;

                    SinglePlotChoiceData choice = interactController.choiceData;
                    string requirementDescription = PlotChoiceMetaDataManager.GetRequirementDescription(choice);
                    if (string.IsNullOrEmpty(requirementDescription))
                        continue;

                    Transform requireTf = child.Find("Label");
                    if (requireTf != null)
                    {
                        requireTf = requireTf.Find("Require");
                    }

                    if (requireTf == null)
                    {
                        requireTf = child.Find("Require");
                    }

                    if (requireTf == null) continue;

                    Text requireText = requireTf.GetComponent<Text>();
                    if (requireText == null) continue;

                    LTLocalization.SetText(requireText, FormatRequirementDescription(requirementDescription));
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"PlotTextShowFinishedPatch: Postfix 异常 - {ex.Message}");
            }
            finally
            {
                RestoreChoiceDescribe(__state);
            }
        }

        /// <summary>
        /// 扩展前置描述沿用本体需求文本的显示习惯，显示时自动包一层括号。
        /// </summary>
        private static string FormatRequirementDescription(string requirementDescription)
        {
            if (string.IsNullOrEmpty(requirementDescription))
                return string.Empty;

            return "(" + requirementDescription + ")";
        }

        /// <summary>
        /// 还原 Prefix 中为本体 UI 临时剥离过的 describe。
        ///
        /// 这样按钮文本显示的是干净描述，而 SinglePlotChoiceData 自身仍保留扩展标记，
        /// 后续 Clone 或兜底解析仍能从对象自身取回元数据。
        /// </summary>
        private static void RestoreChoiceDescribe(Dictionary<SinglePlotChoiceData, string> originalDescribeMap)
        {
            if (originalDescribeMap == null || originalDescribeMap.Count == 0)
                return;

            foreach (KeyValuePair<SinglePlotChoiceData, string> pair in originalDescribeMap)
            {
                if (pair.Key != null)
                    pair.Key.describe = pair.Value;
            }
        }
    }
}
