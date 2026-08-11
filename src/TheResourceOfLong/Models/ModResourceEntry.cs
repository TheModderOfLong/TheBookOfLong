using System;

namespace TheResourceOfLong
{
    public sealed class ModResourceEntry
    {
        public string ModId { get; set; }
        public string ModDirectoryPath { get; set; }
        public string ResDirectoryPath { get; set; }
        public string VirtualPath { get; set; }
        public string ResourceTypeName { get; set; }
        public string Source { get; set; }
        public string FullSourcePath { get; set; }
        public string BundlePath { get; set; }
        public string BundleAssetName { get; set; }
        public string Mode { get; set; }
        public int Priority { get; set; }
        public bool HasPriority { get; set; }
        public int ProjectOrder { get; set; }
        public int ResourceOrder { get; set; }
        public bool FromManifest { get; set; }
        public ResourceSourceKind SourceKind { get; set; }
        public float PixelsPerUnit { get; set; }
        public float PivotX { get; set; }
        public float PivotY { get; set; }

        public string CacheKey(Type requestedType)
        {
            string typeName = requestedType == null ? string.Empty : requestedType.FullName;
            return NormalizePath(VirtualPath) + "|" + typeName;
        }

        public static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            string normalized = path.Replace('\\', '/').Trim();
            while (normalized.StartsWith("/", StringComparison.Ordinal)) normalized = normalized.Substring(1);
            while (normalized.Contains("//")) normalized = normalized.Replace("//", "/");
            return normalized;
        }
    }
}
