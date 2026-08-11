namespace TheResourceOfLong
{
    public sealed class ResourceProbeConfig
    {
        public bool EnableResourceProbe { get; set; }
        public bool LogMisses { get; set; }
        public bool LogStackTrace { get; set; }
        public bool LogLoadAll { get; set; }
        public bool EnableContainerProbe { get; set; }
        public bool EnableContainerProbeOverlay { get; set; }
        public bool EnableScenePrefabUiProbe { get; set; }
        public bool EnableResourceManifestGenerator { get; set; }
        public bool EnableMappingRulesGenerator { get; set; }
        public int MaxStackTraceLength { get; set; }

        public static ResourceProbeConfig CreateDefault()
        {
            return new ResourceProbeConfig
            {
                EnableResourceProbe = false,
                LogMisses = true,
                LogStackTrace = false,
                LogLoadAll = true,
                EnableContainerProbe = false,
                EnableContainerProbeOverlay = false,
                EnableScenePrefabUiProbe = false,
                EnableResourceManifestGenerator = false,
                EnableMappingRulesGenerator = false,
                MaxStackTraceLength = 4000
            };
        }
    }
}
