using System;
using System.Text;
using HarmonyLib;
using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 对 GlobalData.ReplaceSpeString 的 HarmonyPostfix 补丁
    /// 在原始占位符替换完成后，额外处理条件显示文本、[$查询$] 和 [&算术&] 占位符
    ///
    /// 语法：
    ///   <cdt='条件表达式'>显示文本</cdt> → 条件成立时保留显示文本，不成立时删除整段
    ///   [$查询类型:参数1:参数2...$]  → 调用 ConditionQueryHandlers.ExecuteQuery 获取字符串值
    ///   [&算术表达式&]              → 调用 ConditionExpressionEvaluator.ParseArithExpr 求值后转为字符串
    ///
    /// 示例：
    ///   "<cdt='[$GetStrVal:TEMP取姓$][<>][$null$]'>已有姓氏</cdt>" → 条件成立时显示"已有姓氏"
    ///   "你的好感度为[$GetFlotVal:HeroFavor:100$]"  → "你的好感度为75"
    ///   "总计[&10+20*3&]点伤害"                      → "总计70点伤害"
    ///   "[$HeroData:GetMoney:targetInteractHero$]两" → "5000两"
    ///   "[&[$HeroData:GetMoney:player$]+[$HeroData:GetMoney:sourceInteractHero$]&]两" → "8000两"
    /// </summary>
    [HarmonyPatch(typeof(GlobalData), "ReplaceSpeString")]
    public class GlobalDataReplaceSpeStringPatch
    {
        private const string ConditionalTextStart = "<cdt=";
        private const string ConditionalTextEnd = "</cdt>";

        [HarmonyPostfix]
        public static void ReplaceSpeStringPostfix(ref string __result, string targetText, int sourceHeroID)
        {
            try
            {
                // 快速跳过：不含任何可解析指令语法则无需处理
                bool hasConditionalText = HasConditionalTextSyntax(__result);
                bool hasParseableSyntax = ConditionQueryHandlers.ContainsParseableSyntax(__result);
                if (!hasConditionalText && !hasParseableSyntax)
                    return;

                PlotController pc = PlotController._instance;
                string original = __result;
                if (hasConditionalText)
                {
                    __result = ResolveConditionalTextBlocks(pc, __result);
                }

                if (ConditionQueryHandlers.ContainsParseableSyntax(__result))
                {
                    __result = ConditionQueryHandlers.ResolveAllCommands(pc, __result);
                }

                if (__result != original)
                {
                    LoggerManager.Debug($"ReplaceSpeString扩展: \"{TruncateForLog(original, 500)}\" → \"{TruncateForLog(__result, 500)}\"");
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"ReplaceSpeString扩展: Postfix整体异常: {ex.Message}\n{ex.StackTrace}");
                // 不修改 __result，保留原始替换结果
            }
        }

        /// <summary>
        /// 判断剧情文本中是否包含条件显示文本语法：<cdt='条件表达式'>显示文本</cdt>
        /// </summary>
        private static bool HasConditionalTextSyntax(string text)
        {
            return !string.IsNullOrEmpty(text) && text.Contains(ConditionalTextStart);
        }

        /// <summary>
        /// 处理条件显示文本块。
        /// 条件成立时保留中间文本，条件不成立时删除整段；保留下来的文本随后再统一解析查询/算术指令。
        /// 初版仅保证平级条件块，若需要嵌套条件块，可在后续改为栈式解析。
        /// </summary>
        private static string ResolveConditionalTextBlocks(PlotController pc, string text)
        {
            if (string.IsNullOrEmpty(text) || !HasConditionalTextSyntax(text))
                return text;

            StringBuilder builder = new StringBuilder(text.Length);
            int cursor = 0;

            while (cursor < text.Length)
            {
                int startIndex = text.IndexOf(ConditionalTextStart, cursor, StringComparison.Ordinal);
                if (startIndex < 0)
                {
                    builder.Append(text, cursor, text.Length - cursor);
                    break;
                }

                builder.Append(text, cursor, startIndex - cursor);

                int quoteIndex = startIndex + ConditionalTextStart.Length;
                if (quoteIndex >= text.Length || !IsConditionQuote(text[quoteIndex]))
                {
                    LoggerManager.Warning($"ReplaceSpeString扩展: 条件显示文本缺少条件引号，保留原文: \"{TruncateForLog(text.Substring(startIndex), 300)}\"");
                    builder.Append(text, startIndex, text.Length - startIndex);
                    break;
                }

                char quote = text[quoteIndex];
                int conditionStart = quoteIndex + 1;
                int conditionEnd = text.IndexOf(quote + ">", conditionStart, StringComparison.Ordinal);
                if (conditionEnd < 0)
                {
                    LoggerManager.Warning($"ReplaceSpeString扩展: 条件显示文本缺少开头标签结束符，保留原文: \"{TruncateForLog(text.Substring(startIndex), 300)}\"");
                    builder.Append(text, startIndex, text.Length - startIndex);
                    break;
                }

                int bodyStart = conditionEnd + 2;
                int endIndex = text.IndexOf(ConditionalTextEnd, bodyStart, StringComparison.Ordinal);
                if (endIndex < 0)
                {
                    LoggerManager.Warning($"ReplaceSpeString扩展: 条件显示文本缺少闭合标签 </cdt>，保留原文: \"{TruncateForLog(text.Substring(startIndex), 300)}\"");
                    builder.Append(text, startIndex, text.Length - startIndex);
                    break;
                }

                string expression = text.Substring(conditionStart, conditionEnd - conditionStart);
                string body = text.Substring(bodyStart, endIndex - bodyStart);

                bool conditionMet;
                try
                {
                    conditionMet = ConditionExpressionEvaluator.Evaluate(pc, expression);
                }
                catch (Exception ex)
                {
                    LoggerManager.Error($"ReplaceSpeString扩展: 条件显示文本求值异常，保留原文。条件=\"{TruncateForLog(expression, 300)}\", 异常: {ex.Message}");
                    builder.Append(text, startIndex, endIndex + ConditionalTextEnd.Length - startIndex);
                    cursor = endIndex + ConditionalTextEnd.Length;
                    continue;
                }

                LoggerManager.Debug($"ReplaceSpeString扩展: 条件显示文本 [{TruncateForLog(expression, 300)}] → {conditionMet}");
                if (conditionMet)
                {
                    builder.Append(body);
                }

                cursor = endIndex + ConditionalTextEnd.Length;
            }

            return builder.ToString();
        }

        private static bool IsConditionQuote(char c)
        {
            return c == '\'' || c == '"';
        }

        /// <summary>
        /// 截断字符串用于日志输出，避免超长文本淹没日志
        /// </summary>
        private static string TruncateForLog(string text, int maxLength)
        {
            if (text == null) return "null";
            if (text.Length <= maxLength) return text;
            return text.Substring(0, maxLength) + $"...(总长{text.Length})";
        }
    }
}
