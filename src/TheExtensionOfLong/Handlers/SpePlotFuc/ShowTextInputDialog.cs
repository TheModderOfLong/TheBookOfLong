using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppSystem.Collections.Generic;
using System.Reflection;
using System;

namespace TheExtensionOfLong
{
    [SpePlotFuc("ShowTextInputDialog")]
    public static class SpePlotFucShowTextInputDialog
    {
        /// <summary>
        /// 显示文本输入弹窗，确认后将输入值保存到指定变量
        /// 格式: ShowTextInputDialog*标题#存储变量key#ConfirmPlotId(可选)-CancelPlotId(可选)
        /// 确认：将输入文本保存到 PlotEventLogData 的指定 key，跳转 ConfirmPlotId（可选）
        /// 取消：跳转 CancelPlotId（可选）
        /// </summary>
        public static void TryCall(PlotController __instance, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 2)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*标题#存储变量key#ConfirmPlotId(可选)-CancelPlotId(可选)]");
                return;
            }

            string title = fucParams[0];
            string saveKey = fucParams[1];

            if (string.IsNullOrWhiteSpace(saveKey))
            {
                LoggerManager.Warning($"{fucName}: 存储变量key不能为空");
                return;
            }

            string[] plotParams = fucParams.Length > 2 ? fucParams[2].Split('-') : new string[0];
            string confirmPlotId = plotParams.Length >= 1 ? plotParams[0] : null;
            string cancelPlotId = plotParams.Length >= 2 ? plotParams[1] : null;

            string defText = "";
            PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
            if (logData != null && logData.HaveKey(saveKey))
            {
                defText = logData.Get(saveKey) ?? "";
            }

            TextInputDialog.Show(title, defText, (text) =>
            {
                // 确认：保存变量
                PlotEventLogData currentLogData = CommonHandlers.GetPlotEventLogData();
                if (currentLogData == null)
                {
                    LoggerManager.Error($"{fucName}: PlotEventLogData实例不存在，无法保存输入文本");
                    return;
                }

                if (string.IsNullOrEmpty(text))
                {
                    currentLogData.Set(saveKey, null);
                    LoggerManager.Debug($"{fucName}: 已删除字符串变量: {saveKey}");
                }
                else
                {
                    currentLogData.Set(saveKey, text);
                    LoggerManager.Debug($"{fucName}: 已设置字符串变量: {saveKey}={text}");
                }

                if (!string.IsNullOrWhiteSpace(confirmPlotId))
                {
                    __instance.ChangePlotDataBase(confirmPlotId);
                }
            },
            () =>
            {
                // 取消
                if (!string.IsNullOrWhiteSpace(cancelPlotId))
                {
                    __instance.ChangePlotDataBase(cancelPlotId);
                }
            });
        }
    }
}
