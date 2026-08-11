using System;

namespace TheExtensionOfLong
{
    /// <summary>
    /// SinglePlotChoiceData 文本预处理器。
    ///
    /// 负责在本体构造选项前识别龙之书扩展字段，并将扩展元数据编码进 [4] describe。
    /// EffectiveText 是可以安全交给本体 SinglePlotChoiceData 构造器的文本。
    /// </summary>
    public static class SinglePlotChoiceDataTextProcessor
    {
        private const string ConditionGroupKeyword = "ConditionGroup";

        /// <summary>
        /// 单条选项文本的预处理结果。
        /// </summary>
        public sealed class ProcessResult
        {
            public string RawText;
            public string EffectiveText;
            public string[] RawParts;
            public string[] EffectiveParts;
            public bool HasRequirementField;
            public bool HasInteractionField;
            public bool RequirementIsExtension;
            public bool InteractionIsExtension;
            public PlotChoiceMetaData Meta;
        }

        /// <summary>
        /// 处理单条选项文本。
        /// </summary>
        public static ProcessResult Process(string choiceDataText)
        {
            ProcessResult result = new ProcessResult();
            result.RawText = choiceDataText;
            result.EffectiveText = choiceDataText;
            result.Meta = new PlotChoiceMetaData();

            if (choiceDataText == null)
                return result;

            string[] rawParts = choiceDataText.Split(';');
            string[] effectiveParts = (string[])rawParts.Clone();
            result.RawParts = rawParts;
            result.EffectiveParts = effectiveParts;
            bool changed = false;

            if (rawParts.Length > 5 && !string.IsNullOrEmpty(rawParts[5]))
            {
                result.HasRequirementField = true;
                string payload;
                if (TryExtractConditionGroupPayload(rawParts[5], out payload))
                {
                    string condition;
                    string description;
                    bool expressionMissing;
                    if (PlotChoiceMetaDataManager.TryParseConditionDescriptor(payload, out condition, out description, out expressionMissing))
                    {
                        result.Meta.RequirementCondition = condition;
                        result.Meta.RequirementDescription = description;
                        result.RequirementIsExtension = true;
                        effectiveParts[5] = string.Empty;
                        changed = true;
                    }
                }
            }

            if (rawParts.Length > 6 && !string.IsNullOrEmpty(rawParts[6]))
            {
                result.HasInteractionField = true;
                string payload;
                if (TryExtractConditionGroupPayload(rawParts[6], out payload))
                {
                    string condition;
                    string description;
                    bool expressionMissing;
                    if (PlotChoiceMetaDataManager.TryParseConditionDescriptor(payload, out condition, out description, out expressionMissing))
                    {
                        result.Meta.InteractionCondition = condition;
                        result.Meta.InteractionDescription = description;
                        result.InteractionIsExtension = true;
                        effectiveParts[6] = string.Empty;
                        changed = true;
                    }
                }
            }

            if (rawParts.Length > 7 && !string.IsNullOrEmpty(rawParts[7]))
            {
                result.Meta.ShowCondition = rawParts[7];
                effectiveParts[7] = string.Empty;
                changed = true;
            }

            if (rawParts.Length > 8 && !string.IsNullOrEmpty(rawParts[8]))
            {
                if (!result.RequirementIsExtension && !result.HasRequirementField)
                {
                    string condition;
                    string description;
                    bool expressionMissing;
                    if (PlotChoiceMetaDataManager.TryParseConditionDescriptor(rawParts[8], out condition, out description, out expressionMissing))
                    {
                        result.Meta.RequirementCondition = condition;
                        result.Meta.RequirementDescription = description;
                    }
                }

                effectiveParts[8] = string.Empty;
                changed = true;
            }

            if (rawParts.Length > 9 && !string.IsNullOrEmpty(rawParts[9]))
            {
                if (!result.InteractionIsExtension && !result.HasInteractionField)
                {
                    string condition;
                    string description;
                    bool expressionMissing;
                    if (PlotChoiceMetaDataManager.TryParseConditionDescriptor(rawParts[9], out condition, out description, out expressionMissing))
                    {
                        result.Meta.InteractionCondition = condition;
                        result.Meta.InteractionDescription = description;
                    }
                }

                effectiveParts[9] = string.Empty;
                changed = true;
            }

            bool hasEffectiveMeta = result.Meta != null && !result.Meta.IsEmpty;
            if (hasEffectiveMeta && effectiveParts.Length > 4)
            {
                effectiveParts[4] = PlotChoiceMetaTagHelper.Encode(result.Meta, effectiveParts[4]);
                changed = true;
            }

            if (changed)
            {
                result.EffectiveText = string.Join(";", effectiveParts);
            }

            return result;
        }

        /// <summary>
        /// 解析 [5]/[6] 字段中的扩展关键字。
        ///
        /// 字段采用 `/` 作为子参数分隔符，因此这里先拆出首个子参数作为关键字：
        /// - `ConditionGroup/表达式/说明` -> keyword=ConditionGroup, payload=表达式/说明
        /// - `ConditionGroup` -> keyword=ConditionGroup, payload=空字符串
        /// </summary>
        private static bool TryExtractConditionGroupPayload(string fieldValue, out string payload)
        {
            payload = string.Empty;

            if (string.IsNullOrEmpty(fieldValue))
                return false;

            string[] parts = fieldValue.Split(new[] { '/' }, 2);
            string keyword = parts[0].Trim();
            if (!string.Equals(keyword, ConditionGroupKeyword, StringComparison.OrdinalIgnoreCase))
                return false;

            payload = parts.Length > 1 ? parts[1] : string.Empty;
            return true;
        }
    }
}
