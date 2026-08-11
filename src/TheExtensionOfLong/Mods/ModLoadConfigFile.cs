using System.Collections.Generic;

namespace TheExtensionOfLong
{
    internal sealed class ModLoadConfigFile
    {
        public int FormatVersion { get; set; }
        public string Description { get; set; }
        public List<ModLoadConfigEntry> Mods { get; set; }

        public ModLoadConfigFile()
        {
            FormatVersion = 1;
            Description = ModLoadConfigService.DefaultDescription;
            Mods = new List<ModLoadConfigEntry>();
        }
    }
}
