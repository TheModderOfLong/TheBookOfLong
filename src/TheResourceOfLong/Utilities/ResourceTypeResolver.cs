using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppSpine.Unity;
using UnityEngine;

namespace TheResourceOfLong
{
    public static class ResourceTypeResolver
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, Type> ResolvedTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> WarnedUnknownTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static Type Resolve(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return typeof(UnityEngine.Object);

            string normalized = typeName.Trim();
            Type cached;
            lock (SyncRoot)
            {
                if (ResolvedTypes.TryGetValue(normalized, out cached)) return cached;
            }

            Type resolved = ResolveCore(normalized);
            lock (SyncRoot)
            {
                ResolvedTypes[normalized] = resolved;
            }

            return resolved;
        }

        public static bool IsRequestedTypeCompatible(UnityEngine.Object asset, Type requestedType)
        {
            if (asset == null) return false;
            if (requestedType == null || requestedType == typeof(UnityEngine.Object)) return true;

            Type actualType = asset.GetType();
            if (requestedType.IsAssignableFrom(actualType)) return true;

            if (requestedType == typeof(GameObject))
            {
                try
                {
                    return asset.TryCast<GameObject>() != null;
                }
                catch
                {
                    return false;
                }
            }

            if (requestedType == typeof(SkeletonDataAsset))
            {
                try
                {
                    return asset.TryCast<SkeletonDataAsset>() != null;
                }
                catch
                {
                    return false;
                }
            }

            if (requestedType == typeof(TextAsset))
            {
                try
                {
                    return asset.TryCast<TextAsset>() != null;
                }
                catch
                {
                    return false;
                }
            }

            if (requestedType == typeof(Texture) && asset is Texture2D) return true;
            if (requestedType == typeof(UnityEngine.Object)) return true;

            return false;
        }

        private static Type ResolveCore(string normalized)
        {
            Type knownType = ResolveKnownUnityType(normalized);
            if (knownType != null) return knownType;

            Type type = Type.GetType(normalized, false);
            if (type != null) return type;

            type = Type.GetType("UnityEngine." + normalized + ", UnityEngine.CoreModule", false);
            if (type != null) return type;

            type = Type.GetType("UnityEngine." + normalized + ", UnityEngine.AssetBundleModule", false);
            if (type != null) return type;

            type = FindLoadedType(normalized);
            if (type != null) return type;

            WarnUnknownTypeOnce(normalized);
            return typeof(UnityEngine.Object);
        }

        private static Type ResolveKnownUnityType(string normalized)
        {
            if (EqualsName(normalized, "Object")) return typeof(UnityEngine.Object);
            if (EqualsName(normalized, "TextAsset")) return typeof(TextAsset);
            if (EqualsName(normalized, "Texture2D")) return typeof(Texture2D);
            if (EqualsName(normalized, "Texture")) return typeof(Texture);
            if (EqualsName(normalized, "Sprite")) return typeof(Sprite);
            if (EqualsName(normalized, "AudioClip")) return typeof(AudioClip);
            if (EqualsName(normalized, "GameObject")) return typeof(GameObject);
            if (EqualsName(normalized, "Material")) return typeof(Material);
            if (EqualsName(normalized, "Shader")) return typeof(Shader);
            if (EqualsName(normalized, "AnimationClip")) return typeof(AnimationClip);
            if (EqualsName(normalized, "RuntimeAnimatorController")) return typeof(RuntimeAnimatorController);
            if (EqualsName(normalized, "SkeletonDataAsset") ||
                string.Equals(normalized, "Spine.Unity.SkeletonDataAsset", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "Il2CppSpine.Unity.SkeletonDataAsset", StringComparison.OrdinalIgnoreCase))
            {
                return typeof(SkeletonDataAsset);
            }
            return null;
        }

        private static Type FindLoadedType(string normalized)
        {
            string il2CppName = normalized.StartsWith("Il2Cpp", StringComparison.Ordinal)
                ? normalized
                : "Il2Cpp" + normalized;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                Type type = assembly.GetType(normalized, false, true);
                if (type != null) return type;

                type = assembly.GetType(il2CppName, false, true);
                if (type != null) return type;
            }

            return null;
        }

        private static void WarnUnknownTypeOnce(string normalized)
        {
            lock (SyncRoot)
            {
                if (!WarnedUnknownTypes.Add(normalized)) return;
            }

            LoggerManager.Warning("Unknown resource type '" + normalized + "', falling back to UnityEngine.Object.");
        }

        private static bool EqualsName(string value, string expected)
        {
            return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "UnityEngine." + expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
