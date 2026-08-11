namespace TheResourceOfLong
{
    public sealed class ModProjectInfo
    {
        public string ModId { get; set; }
        public string DisplayName { get; set; }
        public string Version { get; set; }
        public string DirectoryPath { get; set; }
        public string ResDirectoryPath { get; set; }
        public int Priority { get; set; }
        public bool HasPriority { get; set; }
        public int DiscoveryOrder { get; set; }
        public int LoadOrder { get; set; }
        public bool IsEnabled { get; set; }
        public bool UsesBookLoadOrder { get; set; }
    }
}
