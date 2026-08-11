using System;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace TheResourceOfLong
{
    [HarmonyPatch]
    internal static class ResourcesLoadStringPatch
    {
        public static MethodBase TargetMethod()
        {
            return ResourcesPatchTarget.FindNonGeneric("Load", "System.String");
        }

        public static bool Prefix(string path, ref UnityEngine.Object __result, ref ResourceProbeState __state)
        {
            __state = ResourceProbe.CreateState();

            UnityEngine.Object mappedSpeSkeletonAsset;
            if (SpeHeroSkeletonLoadContext.TryLoadMappedSkeletonDataAsset(path, out mappedSpeSkeletonAsset))
            {
                __result = mappedSpeSkeletonAsset;
                ResourceProbe.MarkHandled(__state, path, "MappedSpeHeroSkeleton");
                return false;
            }

            UnityEngine.Object asset;
            if (!ModResourceRegistry.TryLoad(path, typeof(UnityEngine.Object), out asset)) return true;

            __result = asset;
            ResourceProbe.MarkHandled(__state, path, "ModHit");
            return false;
        }

        public static void Postfix(string path, ref UnityEngine.Object __result, ResourceProbeState __state)
        {
            ModResourceRegistry.BindLoadedTextAsset(path, __result);
            ResourceProbe.LogLoad("Resources.Load(string)", path, typeof(UnityEngine.Object), __result, __state);
        }
    }

    [HarmonyPatch]
    internal static class ResourcesLoadStringTypePatch
    {
        public static MethodBase TargetMethod()
        {
            return ResourcesPatchTarget.FindNonGeneric("Load", "System.String", "Il2CppSystem.Type");
        }

        public static bool Prefix(string path, Il2CppSystem.Type systemTypeInstance, ref UnityEngine.Object __result, ref ResourceProbeState __state)
        {
            __state = ResourceProbe.CreateState();
            Type requestedType = ResourcesPatchTarget.ToManagedType(systemTypeInstance);

            UnityEngine.Object mappedSpeSkeletonAsset;
            if (SpeHeroSkeletonLoadContext.TryLoadMappedSkeletonDataAsset(path, out mappedSpeSkeletonAsset) &&
                ResourceTypeResolver.IsRequestedTypeCompatible(mappedSpeSkeletonAsset, requestedType))
            {
                __result = mappedSpeSkeletonAsset;
                ResourceProbe.MarkHandled(__state, path, "MappedSpeHeroSkeleton");
                return false;
            }

            UnityEngine.Object asset;
            if (!ModResourceRegistry.TryLoad(path, requestedType, out asset)) return true;

            __result = asset;
            ResourceProbe.MarkHandled(__state, path, "ModHit");
            return false;
        }

        public static void Postfix(string path, Il2CppSystem.Type systemTypeInstance, ref UnityEngine.Object __result, ResourceProbeState __state)
        {
            ModResourceRegistry.BindLoadedTextAsset(path, __result);
            Type requestedType = ResourcesPatchTarget.ToManagedType(systemTypeInstance);
            ResourceProbe.LogLoad("Resources.Load(string, Type)", path, requestedType, __result, __state);
        }
    }

    [HarmonyPatch]
    internal static class ResourcesLoadAllStringPatch
    {
        public static MethodBase TargetMethod()
        {
            return ResourcesPatchTarget.FindNonGeneric("LoadAll", "System.String");
        }

        public static bool Prefix(string path, ref Il2CppReferenceArray<UnityEngine.Object> __result, ref ResourceProbeState __state)
        {
            __state = ResourceProbe.CreateState();
            UnityEngine.Object[] assets;
            if (!ModResourceRegistry.TryLoadAll(path, typeof(UnityEngine.Object), out assets)) return true;

            __result = ToIl2CppArray(assets);
            ResourceProbe.MarkHandled(__state, path, "ModHit");
            return false;
        }

        public static void Postfix(string path, ref Il2CppReferenceArray<UnityEngine.Object> __result, ResourceProbeState __state)
        {
            int count = __result == null ? 0 : __result.Length;
            ResourceProbe.LogLoadAll("Resources.LoadAll(string)", path, typeof(UnityEngine.Object), count, __state);
        }

        private static Il2CppReferenceArray<UnityEngine.Object> ToIl2CppArray(UnityEngine.Object[] assets)
        {
            Il2CppReferenceArray<UnityEngine.Object> array = new Il2CppReferenceArray<UnityEngine.Object>(assets.Length);
            for (int i = 0; i < assets.Length; i++) array[i] = assets[i];
            return array;
        }
    }

    [HarmonyPatch]
    internal static class ResourcesLoadAllStringTypePatch
    {
        public static MethodBase TargetMethod()
        {
            return ResourcesPatchTarget.FindNonGeneric("LoadAll", "System.String", "Il2CppSystem.Type");
        }

        public static bool Prefix(string path, Il2CppSystem.Type systemTypeInstance, ref Il2CppReferenceArray<UnityEngine.Object> __result, ref ResourceProbeState __state)
        {
            __state = ResourceProbe.CreateState();
            Type requestedType = ResourcesPatchTarget.ToManagedType(systemTypeInstance);
            UnityEngine.Object[] assets;
            if (!ModResourceRegistry.TryLoadAll(path, requestedType, out assets)) return true;

            __result = ToIl2CppArray(assets);
            ResourceProbe.MarkHandled(__state, path, "ModHit");
            return false;
        }

        public static void Postfix(string path, Il2CppSystem.Type systemTypeInstance, ref Il2CppReferenceArray<UnityEngine.Object> __result, ResourceProbeState __state)
        {
            Type requestedType = ResourcesPatchTarget.ToManagedType(systemTypeInstance);
            int count = __result == null ? 0 : __result.Length;
            ResourceProbe.LogLoadAll("Resources.LoadAll(string, Type)", path, requestedType, count, __state);
        }

        private static Il2CppReferenceArray<UnityEngine.Object> ToIl2CppArray(UnityEngine.Object[] assets)
        {
            Il2CppReferenceArray<UnityEngine.Object> array = new Il2CppReferenceArray<UnityEngine.Object>(assets.Length);
            for (int i = 0; i < assets.Length; i++) array[i] = assets[i];
            return array;
        }
    }

    internal static class ResourcesPatchTarget
    {
        public static MethodBase FindNonGeneric(string methodName, params string[] parameterTypeNames)
        {
            MethodInfo[] methods = typeof(Resources).GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (MethodInfo method in methods)
            {
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal)) continue;
                if (method.IsGenericMethodDefinition) continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != parameterTypeNames.Length) continue;

                bool match = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (!string.Equals(parameters[i].ParameterType.FullName, parameterTypeNames[i], StringComparison.Ordinal))
                    {
                        match = false;
                        break;
                    }
                }

                if (match) return method;
            }

            throw new MissingMethodException("UnityEngine.Resources", methodName + "(" + string.Join(", ", parameterTypeNames) + ")");
        }

        public static Type ToManagedType(Il2CppSystem.Type systemTypeInstance)
        {
            if (systemTypeInstance == null) return typeof(UnityEngine.Object);

            string fullName = systemTypeInstance.FullName;
            if (string.IsNullOrWhiteSpace(fullName)) return typeof(UnityEngine.Object);

            return ResourceTypeResolver.Resolve(fullName);
        }
    }
}
