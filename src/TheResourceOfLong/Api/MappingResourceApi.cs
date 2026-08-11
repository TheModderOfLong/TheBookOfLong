using System;
using UnityEngine;

namespace TheResourceOfLong
{
    public static class MappingResourceApi
    {
        public static bool TryGet(string id, out MappingResourceInfo info)
        {
            info = null;

            MappingRuleEntry entry;
            if (!MappingRuleRegistry.TryGetById(id, out entry)) return false;

            info = MappingResourceInfo.FromEntry(entry);
            return info != null;
        }

        public static bool TryLoad(string id, Type requestedType, out UnityEngine.Object asset, out MappingResourceInfo info)
        {
            asset = null;
            info = null;

            MappingRuleEntry entry;
            if (!MappingRuleRegistry.TryGetById(id, out entry)) return false;

            info = MappingResourceInfo.FromEntry(entry);
            return ModResourceRegistry.TryLoad(entry.VirtualResourcePath, requestedType, out asset);
        }

        public static bool TryLoad<T>(string id, out T asset, out MappingResourceInfo info) where T : UnityEngine.Object
        {
            asset = null;

            UnityEngine.Object loaded;
            if (!TryLoad(id, typeof(T), out loaded, out info) || loaded == null) return false;

            asset = loaded as T;
            return asset != null;
        }
    }
}
