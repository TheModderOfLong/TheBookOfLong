using UnityEngine;
using Il2CppSpine.Unity;

namespace TheResourceOfLong
{
    public sealed class SpeHeroSkeletonOverrideEntry
    {
        public MappingRuleEntry Rule;
        public string DirectResourcePath;
        public string DirectFullSourcePath;
        public string DisplayPath;
        public SpeHeroSkeletonFitMode FitMode = SpeHeroSkeletonFitMode.FitHeight;
        public SpeHeroSkeletonApplyWhen ApplyWhen = SpeHeroSkeletonApplyWhen.UseSpeSkeleton;
        public float Scale = 1f;
        public bool HasAnchorX;
        public bool HasAnchorY;
        public float AnchorX = 0.5f;
        public float AnchorY = 0.5f;
        public float OffsetX = 0f;
        public float OffsetY = 0f;
        public bool FlipX;
        public float SceneCameraZoom = 1f;
        public float SceneRenderScale = 1f;
        public bool HasScenePadding;
        public bool HasSceneCameraOffsetX;
        public bool HasSceneCameraOffsetY;
        public float ScenePadding = 0f;
        public float SceneCameraOffsetX = 0f;
        public float SceneCameraOffsetY = 0f;
        public Texture2D CachedTexture;
        public GameObject CachedPrefab;
        public SkeletonDataAsset CachedSkeletonDataAsset;
        public Material CachedSkeletonGraphicMaterial;
        public Material CachedSkeletonGraphicAdditiveMaterial;
        public Material CachedSkeletonGraphicMultiplyMaterial;
        public Material CachedSkeletonGraphicScreenMaterial;
        public bool LoadFailed;

        public string ResourcePath { get { return Rule == null ? (DirectResourcePath ?? string.Empty) : Rule.ResourcePath; } }
        public string FullSourcePath { get { return Rule == null ? (DirectFullSourcePath ?? string.Empty) : Rule.FullSourcePath; } }
        public string VirtualResourcePath { get { return Rule == null ? (DirectResourcePath ?? string.Empty) : Rule.VirtualResourcePath; } }
        public string SourceDescription { get { return string.IsNullOrEmpty(DisplayPath) ? VirtualResourcePath : DisplayPath; } }
    }
}
