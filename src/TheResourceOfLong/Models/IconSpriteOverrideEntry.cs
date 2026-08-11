using UnityEngine;

namespace TheResourceOfLong
{
    internal sealed class IconSpriteOverrideEntry
    {
        public string ModId;
        public string AtlasPath;
        public string SpriteName;
        public string SymbolicSpriteName;
        public string Source;
        public string FullSourcePath;
        public int Priority;
        public bool HasPriority;
        public int ProjectOrder;
        public int ResourceOrder;
        public float PixelsPerUnit = 100f;
        public float PivotX = 0.5f;
        public float PivotY = 0.5f;
        public Sprite CachedSprite;
        public bool LoadFailed;
    }
}
