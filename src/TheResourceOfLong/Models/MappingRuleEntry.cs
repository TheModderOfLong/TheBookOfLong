using System;
using System.Collections.Generic;

namespace TheResourceOfLong
{
    public sealed class MappingRuleEntry
    {
        public string Id;
        public string ModId;
        public string ResourcePath;
        public string VirtualResourcePath;
        public string FullSourcePath;
        public MappingOverrideType OverrideType;
        public string Target;
        public string RawParameters;
        public Dictionary<string, string> Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public string Remark;
        public int Priority;
        public bool HasPriority;
        public int ProjectOrder;
        public int ResourceOrder;
    }
}
