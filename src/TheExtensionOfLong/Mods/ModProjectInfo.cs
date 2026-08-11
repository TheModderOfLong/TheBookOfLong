namespace TheExtensionOfLong
{
    public sealed class ModProjectInfo
    {
        public string ModId { get; set; }
        public string DisplayName { get; set; }
        public string Version { get; set; }
        public int LoadOrder { get; set; }
        public bool IsEnabled { get; set; }
        public string DirectoryPath { get; set; }
        public string DataDirectory { get; set; }
    }
}
