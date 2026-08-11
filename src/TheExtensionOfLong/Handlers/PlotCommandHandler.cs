using System;
using System.Collections.Generic;
using System.Text;
using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 剧情指令与 SendMessage 回调解析工具。
    /// 统一处理指令参数拆分、(()) 分隔符保护、{{}} 延迟求值保护剥离和多回调执行。
    /// </summary>
    public static class PlotCommandHandler
    {
        /// <summary>
        /// 解析指令字符串为指令名和参数数组。
        /// 按 fucSplit 分隔指令名与参数部分，按 paramSplit 分隔各参数（跳过 ((...)) 内的分隔符），
        /// 并统一剥离 {{}} 和 (()) 包裹。
        /// 若指令名为 null 或空字符串则返回 null（非有效自定义指令）。
        /// </summary>
        public static (string fucName, string[] fucParams)? ParseCommandParams(string param, char fucSplit, char paramSplit)
        {
            int starIdx = param.IndexOf(fucSplit);
            string fucName = starIdx < 0 ? param : param.Substring(0, starIdx);
            if (string.IsNullOrEmpty(fucName)) return null;

            string paramPart = starIdx < 0 ? "" : param.Substring(starIdx + 1);
            string[] rawParams = string.IsNullOrEmpty(paramPart) ? new string[0] : SplitRespectingBraces(paramPart, paramSplit);

            for (int i = 0; i < rawParams.Length; i++)
            {
                rawParams[i] = StripBraces(StripParens(rawParams[i]));
            }

            return (fucName, rawParams);
        }

        /// <summary>
        /// 按指定分隔符拆分字符串，但跳过 ((...)) 内的分隔符。
        /// (()) 是分隔符保护符，保护内部的 #、;、- 等分隔符不被拆分。
        /// {{}} 仅是延迟求值保护符，不保护分隔符。
        /// </summary>
        public static string[] SplitRespectingBraces(string input, char separator)
        {
            var parts = new List<string>();
            int parenDepth = 0;
            int start = 0;

            for (int i = 0; i < input.Length; i++)
            {
                if (i + 1 < input.Length && input[i] == '(' && input[i + 1] == '(')
                {
                    parenDepth++;
                    i++;
                }
                else if (i + 1 < input.Length && input[i] == ')' && input[i + 1] == ')')
                {
                    parenDepth--;
                    i++;
                }
                else if (input[i] == separator && parenDepth == 0)
                {
                    parts.Add(input.Substring(start, i - start));
                    start = i + 1;
                }
            }

            parts.Add(input.Substring(start));
            return parts.ToArray();
        }

        /// <summary>
        /// 拆分 clickCallFuc 多回调列表，支持 "|" 和真实换行作为回调分隔符。
        /// 分隔符位于 ((...)) 内部时不会被拆分；缩进、空格、Tab 不会被视为分隔符。
        /// Windows 换行 "\r\n" 会按一次换行处理，单独的 "\r" 不作为分隔符。
        /// </summary>
        public static string[] SplitClickCallFucCallbacks(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return new string[0];
            }

            var parts = new List<string>();
            int parenDepth = 0;
            int start = 0;

            for (int i = 0; i < input.Length; i++)
            {
                if (i + 1 < input.Length && input[i] == '(' && input[i + 1] == '(')
                {
                    parenDepth++;
                    i++;
                }
                else if (i + 1 < input.Length && input[i] == ')' && input[i + 1] == ')')
                {
                    parenDepth--;
                    i++;
                }
                else if (parenDepth == 0 && input[i] == '|')
                {
                    parts.Add(input.Substring(start, i - start));
                    start = i + 1;
                }
                else if (parenDepth == 0 && input[i] == '\n')
                {
                    parts.Add(input.Substring(start, i - start));
                    start = i + 1;
                }
                else if (parenDepth == 0 && i + 1 < input.Length && input[i] == '\r' && input[i + 1] == '\n')
                {
                    parts.Add(input.Substring(start, i - start));
                    i++;
                    start = i + 1;
                }
            }

            parts.Add(input.Substring(start));
            return parts.ToArray();
        }

        /// <summary>
        /// 剥离外层 {{...}} 包裹（延迟求值保护符，如果存在）。
        /// </summary>
        public static string StripBraces(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            if (input.StartsWith("{{") && input.EndsWith("}}") && input.Length > 4)
            {
                return input.Substring(2, input.Length - 4);
            }
            return input;
        }

        /// <summary>
        /// 剥离 ((...)) 分隔符保护符。
        /// 支持保护段出现在字符串任意位置；嵌套保护段仅剥离最外层，不成对内容原样保留。
        /// </summary>
        public static string StripParens(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            if (input.IndexOf("((") < 0 || input.IndexOf("))") < 0) return input;

            // 旧逻辑：只剥离整个参数外层的 ((...))。
            // if (input.StartsWith("((") && input.EndsWith("))") && input.Length > 4)
            // {
            //     return input.Substring(2, input.Length - 4);
            // }

            StringBuilder sb = new StringBuilder(input.Length);
            int copyStart = 0;
            int segmentStart = -1;
            int depth = 0;

            for (int i = 0; i < input.Length; i++)
            {
                if (i + 1 < input.Length && input[i] == '(' && input[i + 1] == '(')
                {
                    if (depth == 0)
                    {
                        segmentStart = i;
                    }

                    depth++;
                    i++;
                    continue;
                }

                if (i + 1 < input.Length && input[i] == ')' && input[i + 1] == ')' && depth > 0)
                {
                    depth--;
                    if (depth == 0 && segmentStart >= 0)
                    {
                        sb.Append(input, copyStart, segmentStart - copyStart);
                        sb.Append(input, segmentStart + 2, i - segmentStart - 2);
                        i++;
                        copyStart = i + 1;
                        segmentStart = -1;
                    }
                    else
                    {
                        i++;
                    }
                }
            }

            sb.Append(input, copyStart, input.Length - copyStart);
            return sb.ToString();
        }

        /// <summary>
        /// 判断 SendMessage 目标函数是否会自行解析并消费 (()) 分隔符保护符。
        /// 这些函数在转发层不能提前 StripParens，否则会破坏其内部参数分隔符保护。
        /// </summary>
        public static bool SendMessageTargetConsumesProtectedParens(string methodName)
        {
            if (string.IsNullOrWhiteSpace(methodName))
                return false;

            switch (methodName.Trim().ToLowerInvariant())
            {
                case "speplotfuc":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 准备 SendMessage 参数。
        /// 对会自行解析 (()) 的目标函数保留原参数；其他目标维持转发层剥离保护符的旧行为。
        /// </summary>
        public static string PrepareSendMessageArg(string methodName, string methodArg)
        {
            string safeArg = methodArg ?? "";
            return SendMessageTargetConsumesProtectedParens(methodName)
                ? safeArg
                : StripParens(safeArg);
        }

        /// <summary>
        /// 执行单条 SendMessage 回调。回调格式: MethodName 或 MethodName;MethodArg。
        /// 返回值表示本次调用是否切换了 nowSinglePlot。
        /// </summary>
        public static bool ExecuteSendMessageCallback(PlotController plotController, string singleCallBack, string sourceLabel)
        {
            if (plotController == null || plotController.gameObject == null)
            {
                LoggerManager.Warning($"{sourceLabel}: PlotController或GameObject为空，无法执行回调");
                return false;
            }

            string raw = singleCallBack == null ? "" : singleCallBack.Trim();
            if (raw.Length == 0)
                return false;

            SinglePlotData savedSinglePlot = plotController.nowSinglePlot;

            string resolved = ConditionQueryHandlers.ResolveAllCommands(plotController, raw);
            if (resolved != raw)
            {
                LoggerManager.Debug($"{sourceLabel}: 回调解析 \"{raw}\" → \"{resolved}\"");
                raw = resolved;
            }

            string[] parts = SplitRespectingBraces(raw, ';');
            if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
            {
                LoggerManager.Warning($"{sourceLabel}: 跳过无效回调片段: {raw}");
                return false;
            }

            string methodName = parts[0].Trim();
            if (parts.Length < 2)
            {
                LoggerManager.Debug($"{sourceLabel}: SendMessage(\"{methodName}\")");
                plotController.gameObject.SendMessage(methodName);
            }
            else
            {
                string methodArg = PrepareSendMessageArg(methodName, parts[1]);
                LoggerManager.Debug($"{sourceLabel}: SendMessage(\"{methodName}\", \"{methodArg}\")");
                plotController.gameObject.SendMessage(methodName, methodArg);
            }

            bool contextChanged = savedSinglePlot != plotController.nowSinglePlot;
            if (contextChanged)
            {
                LoggerManager.Debug($"{sourceLabel}: 回调导致剧情节点切换");
            }

            return contextChanged;
        }

        /// <summary>
        /// 执行 SendMessage 回调列表。allowMultiCallback=true 时按 clickCallFuc 规则拆分多回调。
        /// 返回值表示任一回调是否切换了 nowSinglePlot。
        /// </summary>
        public static bool ExecuteSendMessageCallbacks(PlotController plotController, string callbackText, bool allowMultiCallback, string sourceLabel)
        {
            if (string.IsNullOrWhiteSpace(callbackText))
                return false;

            string[] callbacks = allowMultiCallback
                ? SplitClickCallFucCallbacks(callbackText)
                : new[] { callbackText };

            bool contextChanged = false;
            for (int i = 0; i < callbacks.Length; i++)
            {
                if (ExecuteSendMessageCallback(plotController, callbacks[i], $"{sourceLabel}[{i}]"))
                {
                    contextChanged = true;
                }
            }

            return contextChanged;
        }
    }
}
