using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace TheResourceOfLong
{
    [HarmonyPatch]
    internal static class TextAssetTextPatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            PropertyInfo property = typeof(TextAsset).GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.GetGetMethod() != null) yield return property.GetGetMethod();
        }

        public static void Postfix(TextAsset __instance, ref string __result)
        {
            string text;
            if (ModResourceRegistry.TryGetTextAssetText(__instance, out text))
            {
                __result = text;
            }
        }
    }

    [HarmonyPatch]
    internal static class TextAssetBytesPatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            PropertyInfo property = typeof(TextAsset).GetProperty("bytes", BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.GetGetMethod() != null) yield return property.GetGetMethod();
        }

        public static void Postfix(TextAsset __instance, ref Il2CppStructArray<byte> __result)
        {
            byte[] bytes;
            if (!ModResourceRegistry.TryGetTextAssetBytes(__instance, out bytes)) return;

            Il2CppStructArray<byte> array = new Il2CppStructArray<byte>(bytes.Length);
            for (int i = 0; i < bytes.Length; i++)
            {
                array[i] = bytes[i];
            }

            __result = array;
        }
    }
}
