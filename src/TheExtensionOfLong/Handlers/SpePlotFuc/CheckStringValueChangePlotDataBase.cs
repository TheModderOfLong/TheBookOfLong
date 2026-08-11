using Il2Cpp;
using System;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 检查指定Key的值与目标值的关系
    /// 格式: CheckStringValueChangePlotDataBase*TruePlotDataBaseId-FalsePlotDataBaseId(可选)#key#operator#value
    /// 支持运算符：>, <, >=, <=, =, !=
    /// </summary>
    [SpePlotFuc("CheckStringValueChangePlotDataBase")]
    public static class SpePlotFucCheckStringValueChangePlotDataBase
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 4)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*TruePlotDataBaseId-FalsePlotDataBaseId(可选)#key#operator#value]");
                return;
            }

            string[] plotParams = fucParams[0].Split('-');
            if (plotParams.Length < 1 || string.IsNullOrWhiteSpace(plotParams[0]))
            {
                LoggerManager.Warning($"{fucName}: TruePlotDataBaseId不能为空");
                return;
            }

            string key = fucParams[1];
            string op = fucParams[2];
            string targetValue = fucParams[3];

            if (string.IsNullOrWhiteSpace(key))
            {
                LoggerManager.Warning($"{fucName}: Key不能为空或空白");
                if (plotParams.Length >= 2 && !string.IsNullOrWhiteSpace(plotParams[1]))
                    plotController.ChangePlotDataBase(plotParams[1]);
                return;
            }

            PlotEventLogData plotEventLogData = CommonHandlers.GetPlotEventLogData();
            if (plotEventLogData == null)
            {
                LoggerManager.Error($"{fucName}: PlotEventLogData实例不存在，无法调用此指令");
                if (plotParams.Length >= 2 && !string.IsNullOrWhiteSpace(plotParams[1]))
                    plotController.ChangePlotDataBase(plotParams[1]);
                return;
            }

            string actualValue = plotEventLogData.HaveKey(key) ? plotEventLogData.Get(key) : "";
            bool conditionMet = false;

            // 尝试数值比较
            if (double.TryParse(actualValue, out double actualNum) && double.TryParse(targetValue, out double targetNum))
            {
                switch (op)
                {
                    case ">": conditionMet = actualNum > targetNum; break;
                    case "<": conditionMet = actualNum < targetNum; break;
                    case ">=": conditionMet = actualNum >= targetNum; break;
                    case "<=": conditionMet = actualNum <= targetNum; break;
                    case "=":
                    case "==": conditionMet = Math.Abs(actualNum - targetNum) < 0.0001; break;
                    case "!=":
                    case "<>": conditionMet = Math.Abs(actualNum - targetNum) >= 0.0001; break;
                    default: conditionMet = false; break;
                }
            }
            else
            {
                // 字符串比较
                switch (op)
                {
                    case "=":
                    case "==": conditionMet = actualValue == targetValue; break;
                    case "!=":
                    case "<>": conditionMet = actualValue != targetValue; break;
                    default: conditionMet = false; break;
                }
            }

            LoggerManager.Debug($"{fucName}: Key={key}, 检查[{actualValue}{op}{targetValue}], 结果={conditionMet}");

            if (conditionMet)
            {
                plotController.ChangePlotDataBase(plotParams[0]);
            }
            else if (plotParams.Length >= 2 && !string.IsNullOrWhiteSpace(plotParams[1]))
            {
                plotController.ChangePlotDataBase(plotParams[1]);
            }
        }
    }
}
