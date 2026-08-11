using HarmonyLib;
using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 修正无地点传闻的地点文本，允许 TimerWatcher 生成空 areaID 的纯展示传闻。
    /// </summary>
    [HarmonyPatch(typeof(EventData), "GetPosText")]
    public static class EventDataGetPosTextPatch
    {
        /// <summary>
        /// 当 EventData 没有关联资源点、附近区域和区域列表时，直接返回“无”作为地点文本。
        /// </summary>
        [HarmonyPrefix]
        public static bool Prefix(EventData __instance, ref string __result)
        {
            if (__instance != null
                && __instance.resourcePointID < 0
                && __instance.nearAreaID == -1
                && (__instance.areaID == null || __instance.areaID.Count == 0))
            {
                __result = "无";
                return false;
            }

            return true;
        }
    }
}
