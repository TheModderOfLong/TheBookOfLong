using System;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace TheResourceOfLong
{
    [HarmonyPatch(typeof(BGMController), "SetPlotBgm", typeof(string))]
    internal static class BGMControllerSetPlotBgmPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(BGMController __instance, string name)
        {
            if (__instance == null || string.IsNullOrWhiteSpace(name)) return true;
            if (HasVanillaBgm(__instance, name)) return true;

            string musicPath = string.IsNullOrEmpty(__instance.MusicPath) ? "Sound/Music/" : __instance.MusicPath;
            string path = PathUtility.NormalizeResourcePath(musicPath + name);

            ModResourceEntry entry;
            if (!ModResourceRegistry.TryGetEntry(path, out entry)) return true;

            AudioClipPrefab prefab = new AudioClipPrefab();
            prefab.audioClip = name;
            prefab.volume = 1f;

            __instance.plotBgm = prefab;
            if (__instance.gameBGM != null)
            {
                __instance.gameBGM.loop = false;
            }
            LoggerManager.Info("Set custom plot BGM target: " + path + " (" + entry.ModId + ")");
            return false;
        }

        private static bool HasVanillaBgm(BGMController controller, string name)
        {
            try
            {
                if (controller.AllBGM == null) return false;

                int count = controller.AllBGM.Count;
                for (int i = 0; i < count; i++)
                {
                    AudioClipPrefab prefab = controller.AllBGM[i];
                    if (prefab == null) continue;
                    if (string.Equals(prefab.audioClip, name, StringComparison.Ordinal)) return true;
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Debug("Failed to inspect vanilla BGM list: " + ex.Message);
            }

            return false;
        }
    }
}
