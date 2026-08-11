using System.Collections.Generic;

namespace TheExtensionOfLong
{
    public sealed class TriggerStorageData
    {
        public int Version = 1;
        public List<TriggerState> States = new List<TriggerState>();
    }

    public sealed class TriggerState
    {
        public string Id;
        public bool Enabled;
    }
}
