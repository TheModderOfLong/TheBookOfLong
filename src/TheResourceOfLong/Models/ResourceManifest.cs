using System.Collections.Generic;

namespace TheResourceOfLong
{
    public sealed class ResourceManifest
    {
        public int FormatVersion { get; set; }

        public List<ResourceManifestEntry> Resources { get; set; }

        public List<PathTypeRule> PathTypeRules { get; set; }
    }

    public sealed class ResourceManifestEntry
    {
        public string Path { get; set; }

        public string Type { get; set; }

        public string Source { get; set; }

        public string Mode { get; set; }

        public float? PixelsPerUnit { get; set; }

        public float? PivotX { get; set; }

        public float? PivotY { get; set; }
    }

    /// <summary>
    /// 按路径前缀批量指定资源类型的规则。
    /// 当资源条目未显式声明 Type 时，按顺序匹配 PathTypeRules 中 prefix 最长命中项。
    /// prefix 使用 '/' 分隔，忽略大小写，末尾 '/' 表示目录前缀匹配。
    /// 示例: { "prefix": "Icon/", "type": "Sprite" } 会匹配 "Icon/MyIcon"、"Icon/Sub/123" 等。
    /// </summary>
    public sealed class PathTypeRule
    {
        public string Prefix { get; set; }

        public string Type { get; set; }

        public float? PixelsPerUnit { get; set; }

        public float? PivotX { get; set; }

        public float? PivotY { get; set; }
    }
}
