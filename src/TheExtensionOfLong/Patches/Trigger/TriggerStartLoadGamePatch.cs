using System;
using HarmonyLib;
using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 载入存档后触发 StartLoadGame 类型的 TriggerData 规则。
    /// </summary>
    [HarmonyPatch(typeof(GameController), "StartLoadGame")]
    public static class TriggerStartLoadGamePatch
    {
        [HarmonyPostfix]
        public static void Postfix(GameController __instance)
        {
            try
            {
                TimerManager.InitializeCacheAfterGameLoaded(__instance?.worldData);
                TriggerRegistry.Reload();
                TriggerExecutor.Evaluate(TriggerType.StartLoadGame, onlyFirst: false);
            }
            catch (Exception ex)
            {
                LoggerManager.Error("TriggerData: StartLoadGame 触发失败: " + ex);
            }
        }
    }
}
