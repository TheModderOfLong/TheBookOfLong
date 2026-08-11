using System;
using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 选项 describe 扩展标记解析工具。
    ///
    /// 扩展条件会写入 SinglePlotChoiceData.describe 的开头，并跟随本体 Clone 流转：
    /// - {{显示条件:条件表达式}}
    /// - {{前置条件:条件表达式/前置描述}}
    /// - {{互动条件:互动条件表达式/限制描述}}
    ///
    /// 旧写法 {{条件表达式}} 会继续按显示条件处理。
    /// </summary>
    public static class PlotChoiceMetaTagHelper
    {
        private const string OpenTag = "{{";
        private const string CloseTag = "}}";
        private const string ShowConditionPrefix = "显示条件:";
        private const string RequirementConditionPrefix = "前置条件:";
        private const string InteractionConditionPrefix = "互动条件:";

        /// <summary>
        /// 从 describe 开头连续解析扩展标记，并返回剥离标记后的纯描述。
        ///
        /// 只消费开头连续、完整闭合的 {{...}}。正文中间出现的 {{...}} 会作为普通描述保留。
        /// </summary>
        public static PlotChoiceMetaParseResult Parse(string describe)
        {
            PlotChoiceMetaParseResult result = new PlotChoiceMetaParseResult();
            result.Meta = new PlotChoiceMetaData();
            result.CleanDescribe = describe ?? string.Empty;

            if (string.IsNullOrEmpty(describe) || !describe.StartsWith(OpenTag))
                return result;

            int cursor = 0;
            while (cursor + OpenTag.Length <= describe.Length && describe.IndexOf(OpenTag, cursor, OpenTag.Length, StringComparison.Ordinal) == cursor)
            {
                int contentStart = cursor + OpenTag.Length;
                int closeIndex = describe.IndexOf(CloseTag, contentStart, StringComparison.Ordinal);
                if (closeIndex < 0)
                    break;

                string content = describe.Substring(contentStart, closeIndex - contentStart);
                ApplyTag(result.Meta, content);
                cursor = closeIndex + CloseTag.Length;
                result.HasTag = true;
            }

            if (result.HasTag)
                result.CleanDescribe = describe.Substring(cursor);

            return result;
        }

        /// <summary>
        /// 将扩展元数据编码到 describe 开头。
        ///
        /// 代码生成时使用稳定顺序，便于日志排查；运行时解析不要求制作者手写时遵循该顺序。
        /// </summary>
        public static string Encode(PlotChoiceMetaData meta, string originalDescribe)
        {
            string describe = originalDescribe ?? string.Empty;
            if (meta == null || meta.IsEmpty)
                return describe;

            string prefix = string.Empty;

            if (!string.IsNullOrEmpty(meta.ShowCondition))
                prefix += BuildTag("显示条件", meta.ShowCondition);

            if (!string.IsNullOrEmpty(meta.RequirementCondition) || !string.IsNullOrEmpty(meta.RequirementDescription))
                prefix += BuildTag("前置条件", BuildConditionDescriptor(meta.RequirementCondition, meta.RequirementDescription));

            if (!string.IsNullOrEmpty(meta.InteractionCondition) || !string.IsNullOrEmpty(meta.InteractionDescription))
                prefix += BuildTag("互动条件", BuildConditionDescriptor(meta.InteractionCondition, meta.InteractionDescription));

            return prefix + describe;
        }

        /// <summary>
        /// 快速判断 describe 开头是否存在可解析扩展标记。
        /// </summary>
        public static bool HasLeadingTag(string describe)
        {
            return !string.IsNullOrEmpty(describe) && describe.StartsWith(OpenTag);
        }

        private static void ApplyTag(PlotChoiceMetaData meta, string content)
        {
            if (meta == null)
                return;

            if (content == null)
            {
                meta.ShowCondition = string.Empty;
                return;
            }

            if (content.StartsWith(ShowConditionPrefix, StringComparison.Ordinal))
            {
                meta.ShowCondition = content.Substring(ShowConditionPrefix.Length).Trim();
                return;
            }

            if (content.StartsWith(RequirementConditionPrefix, StringComparison.Ordinal))
            {
                string expression;
                string description;
                bool expressionMissing;
                PlotChoiceMetaDataManager.TryParseConditionDescriptor(
                    content.Substring(RequirementConditionPrefix.Length),
                    out expression,
                    out description,
                    out expressionMissing);

                meta.RequirementCondition = expression;
                meta.RequirementDescription = description;
                return;
            }

            if (content.StartsWith(InteractionConditionPrefix, StringComparison.Ordinal))
            {
                string expression;
                string description;
                bool expressionMissing;
                PlotChoiceMetaDataManager.TryParseConditionDescriptor(
                    content.Substring(InteractionConditionPrefix.Length),
                    out expression,
                    out description,
                    out expressionMissing);

                meta.InteractionCondition = expression;
                meta.InteractionDescription = description;
                return;
            }

            meta.ShowCondition = content.Trim();
        }

        private static string BuildTag(string name, string content)
        {
            return "{{" + name + ":" + (content ?? string.Empty) + "}}";
        }

        private static string BuildConditionDescriptor(string expression, string description)
        {
            string safeExpression = expression ?? string.Empty;
            string safeDescription = description ?? string.Empty;

            if (safeDescription.Length == 0)
                return safeExpression;

            return safeExpression + "/" + safeDescription;
        }
    }

    /// <summary>
    /// describe 扩展标记解析结果。
    /// </summary>
    public sealed class PlotChoiceMetaParseResult
    {
        /// <summary>是否解析到至少一个开头扩展标记。</summary>
        public bool HasTag;

        /// <summary>从标记中解析出的扩展元数据。</summary>
        public PlotChoiceMetaData Meta;

        /// <summary>剥离开头扩展标记后的纯描述。</summary>
        public string CleanDescribe;
    }

    /// <summary>
    /// 旧 ConditionGroup 工具的兼容入口。
    ///
    /// 新逻辑统一由 PlotChoiceMetaTagHelper 处理，这里保留方法名，避免旧调用点或后续排查失去线索。
    /// </summary>
    public static class PlotChoiceConditionGroupHelper
    {
        public static string ExtractConditionGroup(string describe)
        {
            string conditionGroup;
            if (TryExtractConditionGroup(describe, out conditionGroup))
                return conditionGroup;
            return null;
        }

        public static bool TryExtractConditionGroup(string describe, out string conditionGroup)
        {
            PlotChoiceMetaParseResult result = PlotChoiceMetaTagHelper.Parse(describe);
            conditionGroup = result.Meta != null ? result.Meta.ShowCondition : null;
            return result.HasTag && conditionGroup != null;
        }

        public static string StripConditionGroup(string describe)
        {
            return PlotChoiceMetaTagHelper.Parse(describe).CleanDescribe;
        }

        public static string EncodeConditionGroup(string conditionGroup, string originalDescribe)
        {
            return PlotChoiceMetaTagHelper.Encode(new PlotChoiceMetaData { ShowCondition = conditionGroup }, originalDescribe);
        }

        public static bool EvaluateConditionGroup(PlotController plotController, SinglePlotChoiceData choice)
        {
            if (choice == null) return true;

            PlotChoiceMetaParseResult result = PlotChoiceMetaTagHelper.Parse(choice.describe);
            return PlotChoiceMetaDataManager.TryEvaluateCondition(
                plotController,
                result.Meta != null ? result.Meta.ShowCondition : string.Empty,
                "ConditionGroupHelper",
                choice.choiceText);
        }
    }
}
