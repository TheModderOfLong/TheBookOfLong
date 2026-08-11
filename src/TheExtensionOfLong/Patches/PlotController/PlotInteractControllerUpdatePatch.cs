using System;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 选项按钮刷新补丁。
    /// 
    /// 本体的 Update 仍然负责：
    /// - requirements
    /// - costResource
    /// - playerInteractionTimeNeed
    /// 
    /// 这里补做两类扩展判定：
    /// - 扩展前置条件：不满足时禁用按钮
    /// - 扩展互动条件：不满足时禁用按钮，并写入限制说明
    /// </summary>
    [HarmonyPatch(typeof(PlotInteractController), "Update")]
    public static class PlotInteractControllerUpdatePatch
    {
        /// <summary>
        /// Postfix。
        /// 
        /// 逻辑顺序：
        /// 1. 取到当前按钮绑定的 choiceData。
        /// 2. 从元数据缓存里取出扩展条件。
        /// 3. 分别判断前置条件和互动条件。
        /// 4. 只要任一扩展条件不满足，就把按钮置为不可交互。
        /// </summary>
        [HarmonyPostfix]
        public static void UpdatePostfix(PlotInteractController __instance)
        {
            if (__instance == null || __instance.choiceData == null) return;

            try
            {
                SinglePlotChoiceData choice = __instance.choiceData;
                PlotController plotController = PlotController._instance;
                if (plotController == null) return;

                PlotChoiceMetaData meta = PlotChoiceMetaDataManager.GetOrParse(choice);
                if (meta == null)
                    return;

                Button button = __instance.GetComponent<Button>();
                if (button == null) return;

                Transform requireTf = __instance.transform.Find("Require");
                if (requireTf == null)
                {
                    Transform labelTf = __instance.transform.Find("Label");
                    if (labelTf != null)
                        requireTf = labelTf.Find("Require");
                }

                bool requirementMet = true;
                if (!string.IsNullOrEmpty(meta.RequirementCondition))
                {
                    requirementMet = PlotChoiceMetaDataManager.TryEvaluateCondition(plotController, meta.RequirementCondition, "PlotInteractController.Update.Requirement", choice.choiceText);
                    if (requireTf != null)
                    {
                        Outline outline = requireTf.GetComponent<Outline>();
                        if (outline != null)
                        {
                            outline.effectColor = requirementMet ? GlobalData.MostDarkPositiveColor : GlobalData.DeepDarkNegativeColor;
                        }
                    }

                    if (!requirementMet)
                    {
                        button.interactable = false;
                    }
                }

                bool interactionMet = true;
                if (!string.IsNullOrEmpty(meta.InteractionCondition))
                {
                    interactionMet = PlotChoiceMetaDataManager.TryEvaluateCondition(plotController, meta.InteractionCondition, "PlotInteractController.Update.Interaction", choice.choiceText);
                    Transform interactTimeTf = __instance.transform.Find("InteractTime");
                    if (interactTimeTf != null)
                    {
                        Text interactText = interactTimeTf.GetComponent<Text>();
                        if (interactText != null)
                        {
                            LTLocalization.SetText(interactText, interactionMet ? string.Empty : (meta.InteractionDescription ?? string.Empty));
                        }
                    }

                    if (!interactionMet)
                    {
                        button.interactable = false;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"PlotInteractControllerUpdatePatch: Postfix 异常 - {ex.Message}");
            }
        }
    }
}
