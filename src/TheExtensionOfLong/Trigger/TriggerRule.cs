namespace TheExtensionOfLong
{
    public sealed class TriggerRule
    {
        public string Id;
        public TriggerType Type;
        public int Priority;
        public bool DefaultEnabled;
        public bool Enabled;
        public string Condition;
        public string Functions;
        public string Note;
        public string ModId;
        public string SourceFile;
        public int LoadOrder;
        public int RowOrder;
    }
}
