using System;
using System.Collections.Generic;
using Il2Cpp;

namespace TheExtensionOfLong
{
    public static class TriggerExecutor
    {
        public static bool Evaluate(TriggerType type, bool onlyFirst = false)
        {
            PlotController plotController = PlotController._instance;
            if (plotController == null)
            {
                LoggerManager.Warning("TriggerExecutor: PlotController实例不存在，跳过触发器 " + type);
                return true;
            }

            List<TriggerRule> rules = TriggerRegistry.GetEnabledRules(type);
            if (rules.Count == 0)
                return true;

            Dictionary<string, string> queryCache = null;
            bool handled = false;

            for (int i = 0; i < rules.Count; i++)
            {
                TriggerRule rule = rules[i];
                if (rule == null) continue;

                try
                {
                    if (!IsConditionMatched(plotController, rule, ref queryCache))
                        continue;

                    handled = true;
                    ExecuteRule(plotController, rule);

                    if (onlyFirst)
                        return false;
                }
                catch (Exception ex)
                {
                    LoggerManager.Error("TriggerExecutor: 执行触发器失败 id=" + rule.Id +
                        ", type=" + rule.Type +
                        ", source=" + rule.SourceFile +
                        " - " + ex);
                }
            }

            return !onlyFirst || !handled;
        }

        private static bool IsConditionMatched(PlotController plotController, TriggerRule rule, ref Dictionary<string, string> queryCache)
        {
            string condition = rule.Condition;
            if (string.IsNullOrWhiteSpace(condition))
                return true;

            bool matched = ConditionExpressionEvaluator.Evaluate(plotController, condition, ref queryCache, showDebugLog: false);
            if (matched)
            {
                LoggerManager.Debug("TriggerExecutor: 命中触发器 id=" + rule.Id + ", type=" + rule.Type);
            }

            return matched;
        }

        private static void ExecuteRule(PlotController plotController, TriggerRule rule)
        {
            if (string.IsNullOrWhiteSpace(rule.Functions))
            {
                LoggerManager.Debug("TriggerExecutor: 触发器函数为空 id=" + rule.Id);
                return;
            }

            PlotCommandHandler.ExecuteSendMessageCallbacks(
                plotController,
                rule.Functions,
                allowMultiCallback: true,
                sourceLabel: "TriggerData:" + rule.Id);
        }
    }
}
