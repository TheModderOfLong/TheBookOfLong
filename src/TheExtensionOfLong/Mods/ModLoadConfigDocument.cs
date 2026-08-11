using System.Collections.Generic;

namespace TheExtensionOfLong
{
    internal sealed class ModLoadConfigDocument
    {
        public string GameRoot { get; set; }
        public string ModsRoot { get; set; }
        public string ModsOfLongRoot { get; set; }
        public string ConfigPath { get; set; }
        public List<ModLoadConfigEntry> Entries { get; private set; }
        public bool IsDirty { get; set; }
        public string LastMessage { get; set; }

        public ModLoadConfigDocument()
        {
            GameRoot = string.Empty;
            ModsRoot = string.Empty;
            ModsOfLongRoot = string.Empty;
            ConfigPath = string.Empty;
            Entries = new List<ModLoadConfigEntry>();
            LastMessage = string.Empty;
        }
    }
}
