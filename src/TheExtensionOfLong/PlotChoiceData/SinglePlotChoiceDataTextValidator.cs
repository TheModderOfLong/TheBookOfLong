using System;
using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// SinglePlotChoiceData 文本格式校验器。
    ///
    /// 只检查会导致本体构造器抛出异常的格式问题，不检查业务合理性。
    /// 调用时机应位于龙之书扩展语法预处理之后，确保校验对象是最终交给本体构造器的文本。
    /// </summary>
    public static class SinglePlotChoiceDataTextValidator
    {
        /// <summary>
        /// 校验改写后的本体选项文本。
        ///
        /// 返回 false 表示发现至少一个会导致本体构造器报错的格式问题。
        /// 本方法只负责提前输出 Error 日志，不阻止本体继续执行，避免引入半初始化选项。
        /// </summary>
        public static bool Validate(string rawChoiceDataText, string effectiveChoiceDataText)
        {
            try
            {
                if (rawChoiceDataText == null)
                {
                    LogError(null, null, "整体文本", "选项数据文本为 null，构造器执行 Split 时将报错", rawChoiceDataText, effectiveChoiceDataText);
                    return false;
                }

                if (effectiveChoiceDataText == null)
                {
                    LogError(null, null, "整体文本", "改写后的选项数据文本为 null，构造器执行 Split 时将报错", rawChoiceDataText, effectiveChoiceDataText);
                    return false;
                }

                string[] parts = effectiveChoiceDataText.Split(';');
                bool valid = true;

                if (parts.Length < 2)
                {
                    LogError(parts, null, "顶层字段数", "至少需要 2 段，避免读取 [0] choiceText 或 [1] callFuc 时越界", rawChoiceDataText, effectiveChoiceDataText);
                    valid = false;
                }

                if (parts.Length > 3 && !string.IsNullOrEmpty(parts[3]))
                {
                    valid &= ValidateCostResource(parts, rawChoiceDataText, effectiveChoiceDataText);
                }

                if (parts.Length > 5 && !string.IsNullOrEmpty(parts[5]))
                {
                    valid &= ValidateRequirement(parts, rawChoiceDataText, effectiveChoiceDataText);
                }

                if (parts.Length > 6 && !string.IsNullOrEmpty(parts[6]))
                {
                    valid &= ValidatePlayerInteractionTimeNeed(parts, rawChoiceDataText, effectiveChoiceDataText);
                }

                return valid;
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"SinglePlotChoiceData 格式校验器异常: {ex.Message}, 原始文本=\"{rawChoiceDataText}\", 改写后文本=\"{effectiveChoiceDataText}\"");
                return false;
            }
        }

        /// <summary>
        /// 校验 [3] costResource。
        /// 本体会按 `资源类型/数量` 拆分，并对前两段执行 int.Parse。
        /// </summary>
        private static bool ValidateCostResource(string[] parts, string rawChoiceDataText, string effectiveChoiceDataText)
        {
            string fieldValue = parts[3];
            string[] costParts = fieldValue.Split('/');

            if (costParts.Length < 2)
            {
                LogError(parts, "[3] costResource", "消耗资源", "格式应为 int/int，当前缺少数量段", rawChoiceDataText, effectiveChoiceDataText);
                return false;
            }

            int ignored;
            if (!int.TryParse(costParts[0], out ignored))
            {
                LogError(parts, "[3] costResource", "消耗资源", "资源类型无法解析为 int", rawChoiceDataText, effectiveChoiceDataText);
                return false;
            }

            if (!int.TryParse(costParts[1], out ignored))
            {
                LogError(parts, "[3] costResource", "消耗资源", "资源数量无法解析为 int", rawChoiceDataText, effectiveChoiceDataText);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 校验 [5] requirements。
        /// 本体会按 `ChoiceRequirementType/数值` 拆分，并执行 Enum.Parse 与 float.Parse。
        /// </summary>
        private static bool ValidateRequirement(string[] parts, string rawChoiceDataText, string effectiveChoiceDataText)
        {
            string fieldValue = parts[5];
            string[] reqParts = fieldValue.Split('/');

            if (reqParts.Length < 2)
            {
                LogError(parts, "[5] requirements", "前置条件", "格式应为 ChoiceRequirementType/数值，当前缺少数值段", rawChoiceDataText, effectiveChoiceDataText);
                return false;
            }

            try
            {
                Enum.Parse(typeof(ChoiceRequirementType), reqParts[0]);
            }
            catch (Exception)
            {
                LogError(parts, "[5] requirements", "前置条件", "条件类型无法解析为 ChoiceRequirementType", rawChoiceDataText, effectiveChoiceDataText);
                return false;
            }

            float ignored;
            if (!float.TryParse(reqParts[1], out ignored))
            {
                LogError(parts, "[5] requirements", "前置条件", "条件数值无法解析为 float", rawChoiceDataText, effectiveChoiceDataText);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 校验 [6] playerInteractionTimeNeed。
        /// 本体会对非空字段执行 PlayerInteractionTimeType 的 Enum.Parse。
        /// </summary>
        private static bool ValidatePlayerInteractionTimeNeed(string[] parts, string rawChoiceDataText, string effectiveChoiceDataText)
        {
            try
            {
                Enum.Parse(typeof(PlayerInteractionTimeType), parts[6]);
                return true;
            }
            catch (Exception)
            {
                LogError(parts, "[6] playerInteractionTimeNeed", "互动次数限制", "无法解析为 PlayerInteractionTimeType", rawChoiceDataText, effectiveChoiceDataText);
                return false;
            }
        }

        /// <summary>
        /// 输出便于定位数据问题的 Error 日志。
        /// </summary>
        private static void LogError(string[] parts, string field, string label, string reason, string rawChoiceDataText, string effectiveChoiceDataText)
        {
            string choiceText = parts != null && parts.Length > 0 ? parts[0] : string.Empty;
            string fieldText = string.IsNullOrEmpty(field) ? label : $"{field} {label}";

            LoggerManager.Error(
                "SinglePlotChoiceData 格式错误: " +
                $"字段={fieldText}, " +
                $"原因={reason}, " +
                $"选项=\"{choiceText}\", " +
                $"原始文本=\"{rawChoiceDataText}\", " +
                $"改写后文本=\"{effectiveChoiceDataText}\"");
        }
    }
}
