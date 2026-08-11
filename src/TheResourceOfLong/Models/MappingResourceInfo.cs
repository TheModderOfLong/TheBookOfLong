using System;
using System.Collections.Generic;

namespace TheResourceOfLong
{
    public sealed class MappingResourceInfo
    {
        public string Id { get; private set; }
        public string ModId { get; private set; }
        public string ResourcePath { get; private set; }
        public string VirtualResourcePath { get; private set; }
        public string FullSourcePath { get; private set; }
        public string OverrideType { get; private set; }
        public string Target { get; private set; }
        public string RawParameters { get; private set; }
        public IReadOnlyDictionary<string, string> Parameters { get; private set; }
        public string Remark { get; private set; }

        public static MappingResourceInfo FromEntry(MappingRuleEntry entry)
        {
            if (entry == null) return null;

            return new MappingResourceInfo
            {
                Id = entry.Id,
                ModId = entry.ModId,
                ResourcePath = entry.ResourcePath,
                VirtualResourcePath = entry.VirtualResourcePath,
                FullSourcePath = entry.FullSourcePath,
                OverrideType = entry.OverrideType.ToString(),
                Target = entry.Target,
                RawParameters = entry.RawParameters,
                Parameters = new Dictionary<string, string>(entry.Parameters ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase),
                Remark = entry.Remark
            };
        }
    }
}
