using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 复合条件判断指令入口
    /// 格式: CheckConditionChangePlotDataBase*条件表达式#TruePlotId-FalsePlotId(可选)
    /// 条件表达式支持 [$查询$]、[&算术&]、[关系运算符]、[AND]、[OR]、()
    /// </summary>
    [SpePlotFuc("CheckConditionChangePlotDataBase")]
    public static class SpePlotFucCheckConditionChangePlotDataBase
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 2)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*条件表达式#TruePlotId-FalsePlotId(可选)]");
                return;
            }

            string expression = fucParams[0];
            string[] plotParams = fucParams[1].Split('-');
            if (plotParams.Length < 1 || string.IsNullOrWhiteSpace(plotParams[0]))
            {
                LoggerManager.Warning($"{fucName}: TruePlotId不能为空");
                return;
            }

            LoggerManager.Debug($"{fucName}: 开始求值表达式: {expression}");

            bool result = ConditionExpressionEvaluator.Evaluate(plotController, expression);

            if (result)
            {
                LoggerManager.Debug($"{fucName}: 求值结果=true, 跳转至{plotParams[0]}");
                plotController.ChangePlotDataBase(plotParams[0]);
            }
            else if (plotParams.Length >= 2 && !string.IsNullOrWhiteSpace(plotParams[1]))
            {
                LoggerManager.Debug($"{fucName}: 求值结果=false, 跳转至{plotParams[1]}");
                plotController.ChangePlotDataBase(plotParams[1]);
            }
            else
            {
                LoggerManager.Debug($"{fucName}: 求值结果=false, 无FalsePlotId，不做跳转");
            }
        }
    }
}
