using System;
using HarmonyLib;
using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 对 PlotInteractController.OnClick() 的 HarmonyPrefix + HarmonyPostfix 补丁
    /// 在原方法执行前先检查龙之书扩展前置/互动条件，避免不可用选项被误触发；
    ///
    /// 在原方法执行前解析 choiceData.callFuc 和 choiceData.callParam 中的查询指令和算术表达式，
    /// 执行后恢复原始值（防止数据持久化）
    ///
    /// 解析由 ConditionQueryHandlers.ResolveAllCommands 统一处理，
    /// 支持 [$查询$]、[&算术&] 及嵌套 [&[$A$]+[$B$]&]
    /// </summary>
    [HarmonyPatch(typeof(PlotInteractController), "OnClick")]
    public class PlotInteractControllerOnClickPatch
    {
        // 保存原始值，用于 Postfix 恢复
        [ThreadStatic]
        private static string _originalCallFuc;

        [ThreadStatic]
        private static string _originalCallParam;

        // 保存被修改的 choiceData 引用，用于 Postfix 精准恢复
        [ThreadStatic]
        private static SinglePlotChoiceData _modifiedChoiceData;

        [HarmonyPrefix]
        public static bool OnClickPrefix(PlotInteractController __instance)
        {
            _modifiedChoiceData = null;
            _originalCallFuc = null;
            _originalCallParam = null;

            SinglePlotChoiceData choiceData = __instance.choiceData;
            if (choiceData == null) return true;

            if (!CanClickChoice(choiceData))
                return false;

            string callFuc = choiceData.callFuc;
            string callParam = choiceData.callParam;

            bool hasFucToResolve = !string.IsNullOrEmpty(callFuc) && ConditionQueryHandlers.ContainsParseableSyntax(callFuc);
            bool hasParamToResolve = !string.IsNullOrEmpty(callParam) && ConditionQueryHandlers.ContainsParseableSyntax(callParam);

            if (!hasFucToResolve && !hasParamToResolve) return true;

            // 保存原值 + 引用
            _originalCallFuc = callFuc;
            _originalCallParam = callParam;
            _modifiedChoiceData = choiceData;

            PlotController pc = PlotController._instance;
            bool modified = false;

            // 解析 callFuc
            if (hasFucToResolve)
            {
                string resolved = ConditionQueryHandlers.ResolveAllCommands(pc, callFuc);
                if (resolved != callFuc)
                {
                    choiceData.callFuc = resolved;
                    LoggerManager.Debug($"PlotInteractController.OnClick callFuc解析: \"{Truncate(callFuc)}\" → \"{Truncate(resolved)}\"");
                    modified = true;
                }
            }

            // 解析 callParam
            if (hasParamToResolve)
            {
                string resolved = ConditionQueryHandlers.ResolveAllCommands(pc, callParam);
                if (resolved != callParam)
                {
                    choiceData.callParam = resolved;
                    LoggerManager.Debug($"PlotInteractController.OnClick callParam解析: \"{Truncate(callParam)}\" → \"{Truncate(resolved)}\"");
                    modified = true;
                }
            }

            if (!modified)
            {
                _modifiedChoiceData = null;
                _originalCallFuc = null;
                _originalCallParam = null;
            }

            return true; // 让原方法执行
        }

        [HarmonyPostfix]
        public static void OnClickPostfix()
        {
            if (_modifiedChoiceData == null) return;

            // 恢复原始值
            if (_originalCallFuc != null)
                _modifiedChoiceData.callFuc = _originalCallFuc;
            if (_originalCallParam != null)
                _modifiedChoiceData.callParam = _originalCallParam;

            LoggerManager.Debug("PlotInteractController.OnClick: 已恢复原始callFuc/callParam");

            _modifiedChoiceData = null;
            _originalCallFuc = null;
            _originalCallParam = null;
        }

        private static string Truncate(string text, int maxLength = 1000)
        {
            if (text == null) return "null";
            if (text.Length <= maxLength) return text;
            return text.Substring(0, maxLength) + $"...(总长{text.Length})";
        }

        /// <summary>
        /// 点击前兜底检查扩展前置条件和互动条件。
        ///
        /// Update 负责 UI 置灰，这里负责防止按钮状态被其它逻辑覆盖后仍进入原版点击流程。
        /// </summary>
        private static bool CanClickChoice(SinglePlotChoiceData choiceData)
        {
            PlotController plotController = PlotController._instance;
            if (plotController == null)
                return true;

            PlotChoiceMetaData meta = PlotChoiceMetaDataManager.GetOrParse(choiceData);
            if (meta == null)
                return true;

            bool requirementMet = PlotChoiceMetaDataManager.TryEvaluateCondition(
                plotController,
                meta.RequirementCondition,
                "PlotInteractController.OnClick.Requirement",
                choiceData.choiceText);

            if (!requirementMet)
                return false;

            bool interactionMet = PlotChoiceMetaDataManager.TryEvaluateCondition(
                plotController,
                meta.InteractionCondition,
                "PlotInteractController.OnClick.Interaction",
                choiceData.choiceText);

            return interactionMet;
        }
    }
}
