using System;
using HarmonyLib;
using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// SinglePlotData.SetChoiceDataTexts 补丁。
    ///
    /// 这是剧情表选项文本进入 SinglePlotChoiceData 构造器前的稳定入口。
    /// 在这里先清理 [5]/[6] 的 ConditionGroup 扩展写法，可避免本体构造器直接 Enum.Parse 扩展文本而报错。
    /// </summary>
    [HarmonyPatch(typeof(SinglePlotData), "SetChoiceDataTexts")]
    public static class SinglePlotDataSetChoiceDataTextsPatch
    {
        /// <summary>
        /// 原方法执行前：
        /// 1. 遍历 choiceDataTexts。
        /// 2. 提取龙之书扩展元数据。
        /// 3. 把列表项改写为本体可解析的 EffectiveText。
        /// 4. 对 EffectiveText 做本体构造器格式校验。
        /// </summary>
        [HarmonyPrefix]
        public static void SetChoiceDataTextsPrefix(
            Il2CppSystem.Collections.Generic.List<string> choiceDataTexts)
        {
            if (choiceDataTexts == null)
                return;

            try
            {
                int count = choiceDataTexts.Count;

                for (int i = 0; i < count; i++)
                {
                    string rawText = choiceDataTexts[i];
                    SinglePlotChoiceDataTextProcessor.ProcessResult result =
                        SinglePlotChoiceDataTextProcessor.Process(rawText);

                    if (!string.Equals(rawText, result.EffectiveText, StringComparison.Ordinal))
                    {
                        choiceDataTexts[i] = result.EffectiveText;
                    }

                    SinglePlotChoiceDataTextValidator.Validate(result.RawText, result.EffectiveText);
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"SinglePlotDataSetChoiceDataTextsPatch: Prefix 异常 - {ex.Message}");
            }
        }
    }
}
