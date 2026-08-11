namespace TheResourceOfLong
{
    public sealed class ResourceProbeState
    {
        public bool HandledByMod { get; set; }
        public string Outcome { get; set; }
        public string ModId { get; set; }
        public string SourceKind { get; set; }
        public string Source { get; set; }
    }
}
