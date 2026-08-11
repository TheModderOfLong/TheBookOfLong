using Newtonsoft.Json;

namespace TheExtensionOfLong
{
    internal sealed class ModLoadConfigEntry
    {
        public string FolderName { get; set; }
        public string DisplayName { get; set; }
        public string Version { get; set; }
        public bool Enabled { get; set; }

        [JsonIgnore]
        public string Author { get; set; }

        [JsonIgnore]
        public string Desc { get; set; }

        public ModLoadConfigEntry()
        {
            FolderName = string.Empty;
            DisplayName = string.Empty;
            Version = "unspecified";
            Enabled = true;
            Author = string.Empty;
            Desc = string.Empty;
        }
    }
}
