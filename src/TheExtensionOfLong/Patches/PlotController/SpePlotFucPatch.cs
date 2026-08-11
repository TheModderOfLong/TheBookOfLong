using HarmonyLib;
using Il2Cpp;

namespace TheExtensionOfLong
{
    [HarmonyPatch(typeof(PlotController), "SpePlotFuc")]
    public class HarmonyPatchSpePlotFuc
    {
        [HarmonyPrefix]
        public static bool SpePlotFucPrefix(PlotController __instance, ref string param)
        {
            return SpePlotFucHandlers.TryHandle(__instance, ref param);
        }
    }
}
