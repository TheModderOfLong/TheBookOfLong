using System;
using HarmonyLib;
using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 每日推进完成后触发 ChangeDay 类型的 TriggerData 规则。
    /// 仅挂接无参 ChangeDay()，避免 ChangeDay(int) 循环调用时重复触发。
    /// </summary>
    [HarmonyPatch(typeof(GameController), "ChangeDay", new Type[] { })]
    public static class TriggerChangeDayPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        public static void Postfix()
        {
            try
            {
                TriggerExecutor.Evaluate(TriggerType.ChangeDay, onlyFirst: false);
            }
            catch (Exception ex)
            {
                LoggerManager.Error("TriggerData: ChangeDay 触发失败: " + ex);
            }
        }
    }
}
