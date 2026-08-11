using HarmonyLib;
using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 新游戏开始后初始化定时器缓存，避免沿用上一次游戏的运行时状态。
    /// </summary>
    [HarmonyPatch(typeof(GameController), "StartNewGame")]
    public static class StartNewGameTimerCachePatch
    {
        /// <summary>
        /// 在新世界数据创建完成后重新加载定时器缓存和传闻镜像。
        /// </summary>
        [HarmonyPostfix]
        public static void Postfix(GameController __instance)
        {
            TimerManager.InitializeCacheAfterGameLoaded(__instance?.worldData);
        }
    }

    /// <summary>
    /// 读档后初始化定时器缓存，使存档中的定时器和 watcher 配置恢复到运行时。
    /// </summary>
    [HarmonyPatch(typeof(GameController), "StartLoadGame")]
    public static class StartLoadGameTimerCachePatch
    {
        /// <summary>
        /// 在存档世界数据载入完成后重新加载定时器缓存和传闻镜像。
        /// </summary>
        [HarmonyPostfix]
        public static void Postfix(GameController __instance)
        {
            TimerManager.InitializeCacheAfterGameLoaded(__instance?.worldData);
        }
    }
}
