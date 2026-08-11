using System;
using System.Collections.Generic;
using System.IO;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace TheResourceOfLong
{
    public static class BundleResourceLoader
    {
        private static readonly Dictionary<string, Il2CppAssetBundle> Bundles = new Dictionary<string, Il2CppAssetBundle>(StringComparer.OrdinalIgnoreCase);

        public static UnityEngine.Object Load(ModResourceEntry entry, Type requestedType)
        {
            if (entry == null || string.IsNullOrEmpty(entry.BundlePath) || string.IsNullOrEmpty(entry.BundleAssetName))
            {
                return null;
            }

            Il2CppAssetBundle bundle = GetBundle(entry.BundlePath);
            if (bundle == null) return null;

            UnityEngine.Object asset = LoadAsset(bundle, entry.BundleAssetName, requestedType);

            if (asset == null)
            {
                LoggerManager.Warning("AssetBundle asset not found: " + entry.BundlePath + ":" + entry.BundleAssetName);
            }

            return asset;
        }

        private static UnityEngine.Object LoadAsset(Il2CppAssetBundle bundle, string assetName, Type requestedType)
        {
            if (bundle == null || string.IsNullOrWhiteSpace(assetName)) return null;

            if (requestedType != null && requestedType != typeof(UnityEngine.Object))
            {
                try
                {
                    return bundle.LoadAsset(assetName, Il2CppType.From(requestedType));
                }
                catch (Exception ex)
                {
                    LoggerManager.Debug("Typed AssetBundle.LoadAsset failed: " + assetName + " type=" + requestedType.FullName + " - " + ex.Message);
                }
            }

            return bundle.LoadAsset(assetName);
        }

        public static void UnloadAll(bool unloadLoadedObjects)
        {
            foreach (KeyValuePair<string, Il2CppAssetBundle> pair in Bundles)
            {
                if (pair.Value != null) pair.Value.Unload(unloadLoadedObjects);
            }

            Bundles.Clear();
        }

        private static Il2CppAssetBundle GetBundle(string path)
        {
            string fullPath = Path.GetFullPath(path);
            Il2CppAssetBundle bundle;
            if (Bundles.TryGetValue(fullPath, out bundle) && bundle != null) return bundle;

            if (!File.Exists(fullPath))
            {
                LoggerManager.Warning("AssetBundle file not found: " + fullPath);
                return null;
            }

            bundle = TryLoadBundle(fullPath);
            if (bundle == null)
            {
                LoggerManager.Warning("Failed to load AssetBundle: " + fullPath);
                return null;
            }

            Bundles[fullPath] = bundle;
            LoggerManager.Info("Loaded AssetBundle: " + fullPath);
            return bundle;
        }

        private static Il2CppAssetBundle TryLoadBundle(string fullPath)
        {
            try
            {
                Il2CppAssetBundle bundle = Il2CppAssetBundleManager.LoadFromFile(fullPath);
                if (bundle != null) return bundle;

                LoggerManager.Warning("Il2CppAssetBundleManager.LoadFromFile returned null: " + fullPath);
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("Il2CppAssetBundleManager.LoadFromFile threw " + ex.GetType().Name + " for " + fullPath + ": " + ex.Message);
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(fullPath);
                Il2CppStructArray<byte> il2CppBytes = new Il2CppStructArray<byte>(bytes.Length);
                for (int i = 0; i < bytes.Length; i++) il2CppBytes[i] = bytes[i];

                Il2CppAssetBundle bundle = Il2CppAssetBundleManager.LoadFromMemory(il2CppBytes);
                if (bundle != null)
                {
                    LoggerManager.Info("Loaded AssetBundle from memory fallback: " + fullPath);
                    return bundle;
                }

                LoggerManager.Warning("Il2CppAssetBundleManager.LoadFromMemory returned null: " + fullPath);
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("Il2CppAssetBundleManager.LoadFromMemory threw " + ex.GetType().Name + " for " + fullPath + ": " + ex.Message);
            }

            return null;
        }
    }
}
