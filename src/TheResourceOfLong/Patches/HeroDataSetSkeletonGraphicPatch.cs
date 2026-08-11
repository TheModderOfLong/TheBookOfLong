using System;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace TheResourceOfLong
{
    internal sealed class SpeHeroPreferenceSuppressionState
    {
        public PlayerPrefDictionary PlayerPrefData;
        public string Key;
        public bool HadKey;
        public int OriginalValue;
        public bool Restored;
    }

    [HarmonyPatch(typeof(HeroData), "SetSkeletonGraphic")]
    internal static class HeroDataSetSkeletonGraphicPatch
    {
        [HarmonyPrefix]
        public static void Prefix(HeroData __instance, Transform targetSkeletonParent, out SpeHeroPreferenceSuppressionState __state)
        {
            __state = null;

            try
            {
                SpeHeroSkeletonLoadContext.Begin(__instance);
                ContainerProbe.Log(__instance, targetSkeletonParent, SpeHeroSkeletonOverrideRenderer.GetProbeReferenceRect(targetSkeletonParent), "Prefix");
                SpeHeroSkeletonOverrideRenderer.Cleanup(targetSkeletonParent);
                __state = SuppressVanillaSpeSkeletonBranchIfNeeded(__instance);
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("Failed to prepare SpeHeroSkeleton override: " + ex.Message);
            }
        }

        [HarmonyPostfix]
        public static void Postfix(HeroData __instance, Transform targetSkeletonParent, SpeHeroPreferenceSuppressionState __state)
        {
            try
            {
                RestoreSuppressedPreference(__state);
                ContainerProbe.Log(__instance, targetSkeletonParent, SpeHeroSkeletonOverrideRenderer.GetProbeReferenceRect(targetSkeletonParent), "Postfix");
                if (targetSkeletonParent != null)
                {
                    Transform speSkeleton = targetSkeletonParent.Find("SpeSkeleton");
                    if (speSkeleton != null)
                    {
                        ContainerProbe.LogSpeSkeletonLayout(__instance, targetSkeletonParent, speSkeleton, "Postfix", "VanillaOrStandardSpeSkeleton");
                    }
                }

                SpeHeroSkeletonOverrideRenderer.TryApply(__instance, targetSkeletonParent);
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("Failed to apply SpeHeroSkeleton override: " + ex.Message);
            }
            finally
            {
                SpeHeroSkeletonLoadContext.End(__instance);
            }
        }

        [HarmonyFinalizer]
        public static Exception Finalizer(Exception __exception, HeroData __instance, Transform targetSkeletonParent, SpeHeroPreferenceSuppressionState __state)
        {
            RestoreSuppressedPreference(__state);
            SpeHeroSkeletonLoadContext.End(__instance);
            if (__exception == null) return null;

            try
            {
                if (!IsKnownSpeSkeletonRenderingException(__exception))
                {
                    LoggerManager.Debug("SpeHeroSkeleton fallback skipped for unrelated SetSkeletonGraphic exception: " + GetExceptionSummary(__exception));
                    return __exception;
                }

                ContainerProbe.Log(__instance, targetSkeletonParent, SpeHeroSkeletonOverrideRenderer.GetProbeReferenceRect(targetSkeletonParent), "Finalizer");
                if (SpeHeroSkeletonOverrideRenderer.TryApplyAfterOriginalFailure(__instance, targetSkeletonParent, __exception))
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("Failed to apply SpeHeroSkeleton fallback after original exception: " + ex.Message);
            }

            return __exception;
        }

        private static SpeHeroPreferenceSuppressionState SuppressVanillaSpeSkeletonBranchIfNeeded(HeroData heroData)
        {
            if (heroData == null) return null;
            if (!SpeHeroSkeletonPolicy.IsPlayerSpeSkeletonEnabled(heroData)) return null;
            if (SpeHeroSkeletonPolicy.HasCompatibleSpeSkeletonBranchResource(heroData)) return null;

            RePlayerPrefData rePlayerPrefData = GameDataController.playerPrefData;
            if (rePlayerPrefData == null || rePlayerPrefData.playerPrefData == null) return null;

            string heroName = heroData.heroName == null ? string.Empty : heroData.heroName.Trim();
            if (heroName.Length == 0) return null;

            string key = heroName + "hideSpeSkeleton";
            PlayerPrefDictionary playerPrefData = rePlayerPrefData.playerPrefData;

            SpeHeroPreferenceSuppressionState state = new SpeHeroPreferenceSuppressionState
            {
                PlayerPrefData = playerPrefData,
                Key = key,
                HadKey = playerPrefData.ContainsKey(key),
                OriginalValue = playerPrefData.GetInt(key)
            };

            playerPrefData.SetKey(key, 1);
            return state;
        }

        private static void RestoreSuppressedPreference(SpeHeroPreferenceSuppressionState state)
        {
            if (state == null || state.Restored || state.PlayerPrefData == null || string.IsNullOrEmpty(state.Key)) return;

            try
            {
                if (state.HadKey)
                {
                    state.PlayerPrefData.SetKey(state.Key, state.OriginalValue);
                }
                else
                {
                    state.PlayerPrefData.RemoveKey(state.Key);
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("Failed to restore temporary hideSpeSkeleton preference: key=" + state.Key + ", error=" + ex.Message);
            }
            finally
            {
                state.Restored = true;
            }
        }

        private static bool IsKnownSpeSkeletonRenderingException(Exception exception)
        {
            if (exception == null) return false;

            string text = ((exception.GetType().FullName ?? string.Empty) + "\n" +
                           (exception.Message ?? string.Empty) + "\n" +
                           (exception.StackTrace ?? string.Empty)).ToLowerInvariant();

            return ContainsAny(text,
                "setskeletongraphic",
                "spine.unity.skeletongraphic",
                "meshgenerator",
                "skeletonrendererinstruction",
                "skeletondataasset",
                "spehero",
                "speskeleton");
        }

        private static bool ContainsAny(string text, params string[] patterns)
        {
            if (string.IsNullOrEmpty(text)) return false;

            for (int i = 0; i < patterns.Length; i++)
            {
                if (text.Contains(patterns[i])) return true;
            }

            return false;
        }

        private static string GetExceptionSummary(Exception exception)
        {
            if (exception == null) return string.Empty;

            string message = exception.Message ?? string.Empty;
            int lineBreak = message.IndexOfAny(new[] { '\r', '\n' });
            if (lineBreak >= 0) message = message.Substring(0, lineBreak);
            return exception.GetType().Name + (string.IsNullOrEmpty(message) ? string.Empty : ": " + message);
        }
    }

    internal static class SpeHeroSkeletonLoadContext
    {
        [ThreadStatic]
        private static HeroData CurrentHeroData;

        public static void Begin(HeroData heroData)
        {
            CurrentHeroData = heroData;
        }

        public static void End(HeroData heroData)
        {
            if (CurrentHeroData == heroData) CurrentHeroData = null;
        }

        public static bool TryLoadMappedSkeletonDataAsset(string path, out UnityEngine.Object asset)
        {
            asset = null;

            HeroData heroData = CurrentHeroData;
            if (heroData == null) return false;

            string expectedPath = SpeHeroSkeletonPolicy.GetVanillaSpeHeroSkeletonPath(heroData);
            if (!string.Equals(PathUtility.NormalizeResourcePath(path), PathUtility.NormalizeResourcePath(expectedPath), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            SpeHeroSkeletonOverrideEntry entry;
            if (!SpeHeroSkeletonOverrideRegistry.TryGet(heroData, out entry) || entry == null) return false;
            if (SpeHeroSkeletonOverrideRegistry.IsSupportedImage(entry.ResourcePath) || SpeHeroSkeletonOverrideRegistry.IsPrefab(entry.ResourcePath)) return false;

            if (entry.Rule == null &&
                string.Equals(PathUtility.NormalizeResourcePath(entry.DirectResourcePath), PathUtility.NormalizeResourcePath(expectedPath), StringComparison.OrdinalIgnoreCase))
            {
                LoggerManager.Debug("Skipped self-referential runtime SpeHeroSkeleton mapping to avoid recursive Resources.Load. heroID=" +
                                    heroData.heroID + ", path=" + expectedPath);
                return false;
            }

            Il2CppSpine.Unity.SkeletonDataAsset skeletonDataAsset;
            if (!SpeHeroSkeletonOverrideRegistry.TryLoadSkeletonDataAsset(entry, out skeletonDataAsset) || skeletonDataAsset == null) return false;

            asset = skeletonDataAsset;
            return true;
        }
    }
}
