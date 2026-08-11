using System;
using HarmonyLib;
using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 挂接每日推进流程，在 GameController.ChangeDay 完成后检查并触发到期定时器。
    /// </summary>
    [HarmonyPatch(typeof(GameController), "ChangeDay", new Type[] { })]
    public static class ChangeDayTimerPatch
    {
        /// <summary>
        /// 原版每日推进成功结束后执行定时器检查，保证定时器与游戏天数推进集中同步。
        /// </summary>
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                TimerManager.CheckTimers();
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"SetTimer: ChangeDay 后检查定时器失败: {ex}");
            }
        }

        /// <summary>
        /// 记录原版 ChangeDay 内部异常的发生时间，并将异常交还给原版/框架继续处理。
        /// </summary>
        [HarmonyFinalizer]
        public static Exception Finalizer(GameController __instance, Exception __exception)
        {
            if (__exception == null)
            {
                return null;
            }

            LoggerManager.Error($"SetTimer: GameController.ChangeDay 原版执行异常，time={DescribeWorldTime(__instance)}: {__exception.GetType().FullName}: {__exception.Message}\n{__exception}");
            return __exception;
        }

        /// <summary>
        /// 安全读取当前世界时间，用于 ChangeDay 异常日志。
        /// </summary>
        private static string DescribeWorldTime(GameController gameController)
        {
            try
            {
                TimeData time = gameController?.worldData?.worldTime;
                if (time == null) return "<null>";
                return $"{time.year}-{time.month}-{time.day}";
            }
            catch (Exception ex)
            {
                return $"<describe failed: {ex.Message}>";
            }
        }
    }
}
