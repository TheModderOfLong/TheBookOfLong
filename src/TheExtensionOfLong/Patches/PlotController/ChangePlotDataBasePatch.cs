using System;
using HarmonyLib;
using Il2Cpp;
using TheBookOfLong;

namespace TheExtensionOfLong
{
    [HarmonyPatch(typeof(PlotController), "ChangePlotDataBase", new[] { typeof(string) })]
    public class ChangePlotDataBasePatch
    {
        [HarmonyPrefix]
        public static bool ChangePlotDataBasePrefix(PlotController __instance, string plotID)
        {
            if (string.IsNullOrEmpty(plotID))
            {
                return true; // 空参数，放行原方法
            }

            if (int.TryParse(plotID, out int plotIntID))
            {
                // 可以解析为int值，放行原方法
                return true;
            }
            else
            {
                if (SymbolicIdService.TryResolveId(plotID, out int assignedId))
                {
                    // 可以解析为Mod ID，直接调用剧情
                    LoggerManager.Debug($"解析剧情ID [{plotID}] 为 {assignedId}，调用剧情 {assignedId}");
                    __instance.ChangePlotDataBase(assignedId);
                    return false;
                }
                else
                {
                    // 失败时处理，放行原方法
                    LoggerManager.Warning($"解析剧情ID [{plotID}]失败，返回原参数");
                    return true;
                }
            }
        }
    }
}