using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 根据条件表达式结果执行对应的函数调用
    /// 格式: CheckConditionCallFuc*条件表达式#TrueCallFucName#TrueCallFucParam(可选)#FalseCallFucName(可选)#FalseCallFucParam(可选)
    ///   CallFucParam 含 # 时必须用 {{}} 包裹，如 {{SetStringValue*key#value}}
    ///   通过 SendMessage 调用 PlotController 上的方法，可执行任意方法（SpePlotFuc、ChangePlotDataBase 等）
    /// 示例:
    ///   CheckConditionCallFuc*[$HeroData:Favor:小白$][>=]50#SpePlotFuc#{{SetStringValue*达标#1}}
    ///   CheckConditionCallFuc*[$HeroData:Favor:小白$][>=]50#SpePlotFuc#{{SetStringValue*达标#1}}#SpePlotFuc#{{SetStringValue*不足#0}}
    ///   CheckConditionCallFuc*[$HeroData:isFemale$][=]1#ChangePlotDataBase#女线剧情#ChangePlotDataBase#男线剧情
    /// </summary>
    [SpePlotFuc("CheckConditionCallFuc")]
    public static class SpePlotFucCheckConditionCallFuc
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 2 || string.IsNullOrWhiteSpace(fucParams[1]))
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*条件表达式#TrueCallFucName#TrueCallFucParam(可选)#FalseCallFucName(可选)#FalseCallFucParam(可选)]");
                return;
            }

            string expression = fucParams[0];

            // 条件求值
            bool result = ConditionExpressionEvaluator.Evaluate(plotController, expression);
            LoggerManager.Debug($"{fucName}: 条件求值结果={result}, 表达式={expression}");

            // 根据条件结果选择分支
            string callFucName;
            string callFucParam = "";

            if (result)
            {
                callFucName = fucParams[1];
                callFucParam = fucParams.Length > 2 ? fucParams[2] : "";
            }
            else
            {
                // FalseCallFucName 在 fucParams[3]，可选
                if (fucParams.Length < 4 || string.IsNullOrWhiteSpace(fucParams[3]))
                {
                    LoggerManager.Debug($"{fucName}: 条件=false, 无FalseCallFuc可执行");
                    return;
                }
                callFucName = fucParams[3];
                callFucParam = fucParams.Length > 4 ? fucParams[4] : "";
            }

            LoggerManager.Debug($"{fucName}: 执行CallFuc: SendMessage(\"{callFucName}\", \"{callFucParam}\")");

            // 通过 SendMessage 执行，与 ChangeNextPlot 方式一致
            plotController.gameObject.SendMessage(callFucName, callFucParam);
        }
    }
}
