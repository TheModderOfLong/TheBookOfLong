using System;
using HarmonyLib;
using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 开始新游戏后触发 StartNewGame 类型的 TriggerData 规则。
    /// </summary>
    [HarmonyPatch(typeof(GameController), "StartNewGame")]
    public static class TriggerStartNewGamePatch
    {
        [HarmonyPostfix]
        public static void Postfix(GameController __instance)
        {
            try
            {
                TimerManager.InitializeCacheAfterGameLoaded(__instance?.worldData);
                TriggerRegistry.Reload();
                TriggerExecutor.Evaluate(TriggerType.StartNewGame, onlyFirst: false);
            }
            catch (Exception ex)
            {
                LoggerManager.Error("TriggerData: StartNewGame 触发失败: " + ex);
            }
        }
    }
}
