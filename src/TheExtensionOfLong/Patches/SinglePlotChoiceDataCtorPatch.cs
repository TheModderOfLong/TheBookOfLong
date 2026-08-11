using System;
using HarmonyLib;
using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// SinglePlotChoiceData 构造器 Prefix 补丁。
    ///
    /// 该补丁是选项扩展条件的兜底预处理入口：
    /// - 识别 [5]/[6] 中的 ConditionGroup 扩展语法。
    /// - 将扩展条件编码进 [4] describe。
    /// - 清空会导致本体构造器 Enum.Parse 报错的扩展字段。
    ///
    /// 主入口仍是 SinglePlotData.SetChoiceDataTexts；这里用于兼容直接 new SinglePlotChoiceData(string) 的路径。
    /// </summary>
    [HarmonyPatch(typeof(SinglePlotChoiceData), MethodType.Constructor, typeof(string))]
    public static class SinglePlotChoiceDataCtorPatch
    {
        /// <summary>
        /// 原构造器执行前改写选项文本，让本体只看到可解析字段。
        /// </summary>
        [HarmonyPrefix]
        public static void CtorPrefix(ref string choiceDataText)
        {
            try
            {
                SinglePlotChoiceDataTextProcessor.ProcessResult result =
                    SinglePlotChoiceDataTextProcessor.Process(choiceDataText);

                choiceDataText = result.EffectiveText;
                SinglePlotChoiceDataTextValidator.Validate(result.RawText, result.EffectiveText);
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"SinglePlotChoiceDataCtorPatch: Prefix 异常 - {ex.Message}");
            }
        }
    }
}
