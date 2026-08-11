using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace TheResourceOfLong
{
    [HarmonyPatch(typeof(BGMController), "LoadAudio", typeof(string))]
    internal static class BGMControllerLoadAudioPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(string path, ref AudioClip __result)
        {
            if (string.IsNullOrWhiteSpace(path)) return true;

            UnityEngine.Object asset;
            if (!ModResourceRegistry.TryLoad(path, typeof(AudioClip), out asset)) return true;

            AudioClip clip = asset as AudioClip;
            if (clip == null)
            {
                LoggerManager.Warning("BGM override is not AudioClip: " + path + ", actual=" + asset.GetType().FullName);
                return true;
            }

            __result = clip;
            // Enable when diagnosing BGM replacement hits.
            // LoggerManager.Info("Loaded BGM override: " + path);
            return false;
        }
    }
}
