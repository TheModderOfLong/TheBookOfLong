using System;
using Il2Cpp;
using Il2CppSpine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace TheResourceOfLong
{
    internal static class SpeHeroSkeletonOverrideRenderer
    {
        private const string OverrideRootName = "TheResourceOfLongMappingOverride";
        private const string ContentName = "Content";
        private const string GlobalSceneBridgeRootName = "TheResourceOfLongScenePrefabBridgeRoot";
        private const string SceneBridgeCameraName = "ScenePrefabCamera";
        private const string SceneBridgeTexturePrefix = "TheResourceOfLong_SpeHeroSceneRT_";
        private const string StraightAlphaInputProperty = "_StraightAlphaInput";
        private const string StraightAlphaInputKeyword = "_STRAIGHT_ALPHA_INPUT";
        private const string CanvasGroupCompatibleProperty = "_CanvasGroupCompatible";
        private const string CanvasGroupCompatibleKeyword = "_CANVAS_GROUP_COMPATIBLE";
        private const string HudFaceContainerPath = "Canvas/HudPanel/FaceMask/Face";
        private const string DetailFaceContainerPath = "Canvas/HeroDetailPanel/Face";
        private const float HudSpeSkeletonTemplateAnchoredY = -255f;
        private const float FemaleSpeSkeletonLocalY = -255f;
        private const float MaleSpeSkeletonLocalY = -270f;
        private const int SceneBridgeLayer = 31;
        private static int _sceneBridgeIndex;
        private static readonly System.Collections.Generic.Dictionary<int, SkeletonGraphicMaterialState> OriginalSkeletonGraphicMaterials =
            new System.Collections.Generic.Dictionary<int, SkeletonGraphicMaterialState>();
        private static readonly System.Collections.Generic.Dictionary<int, float> OriginalSkeletonGraphicScaleX =
            new System.Collections.Generic.Dictionary<int, float>();
        private static readonly System.Collections.Generic.Dictionary<string, SceneBridgeHandle> SceneBridgeHandles =
            new System.Collections.Generic.Dictionary<string, SceneBridgeHandle>(StringComparer.Ordinal);

        private sealed class SkeletonGraphicMaterialState
        {
            public Material Material;
            public Material AdditiveMaterial;
            public Material MultiplyMaterial;
            public Material ScreenMaterial;
        }

        private sealed class SceneBridgeHandle
        {
            public string RenderTextureName;
            public Transform UiRoot;
            public Transform SceneRoot;
            public RawImage Image;
            public RenderTexture RenderTexture;
        }

        private enum FaceContainerKind
        {
            Other,
            Hud,
            Detail
        }

        public static void Cleanup(Transform targetSkeletonParent)
        {
            if (targetSkeletonParent == null) return;

            DestroyExisting(targetSkeletonParent);
            RestoreManagedSkeletonGraphicMaterials(targetSkeletonParent);
            RestoreDirectSkeletonGraphics(targetSkeletonParent);
        }

        public static void DeactivateVanillaSpeSkeleton(Transform targetSkeletonParent)
        {
            if (targetSkeletonParent == null) return;

            Transform speSkeleton = targetSkeletonParent.Find("SpeSkeleton");
            if (speSkeleton != null && speSkeleton.gameObject.activeSelf)
            {
                speSkeleton.gameObject.SetActive(false);
            }
        }

        public static bool TryApply(HeroData heroData, Transform targetSkeletonParent)
        {
            if (heroData == null || targetSkeletonParent == null) return false;

            SpeHeroSkeletonOverrideEntry entry;
            if (!SpeHeroSkeletonOverrideRegistry.TryGet(heroData, out entry) || entry == null) return false;

            if (!ShouldApply(heroData, entry)) return false;

            DestroyExisting(targetSkeletonParent);

            SkeletonGraphic reference = FindDirectReferenceSkeleton(targetSkeletonParent);
            RectTransform referenceRect = reference == null ? null : reference.GetComponent<RectTransform>();
            if (referenceRect == null)
            {
                LoggerManager.Warning("SpeHeroSkeleton override skipped because reference SkeletonGraphic was not found. heroID=" + heroData.heroID);
                return false;
            }

            bool applied = TryApplyByResourceType(heroData, entry, targetSkeletonParent, referenceRect);

            if (!applied) return false;

            // HUD/详情页的图片和预制体使用覆盖层翻转；Spine 保留 SpeSkeleton 路径。
            if (NeedsOverrideRootFlip(entry, targetSkeletonParent))
            {
                Transform root = targetSkeletonParent.Find(OverrideRootName);
                if (root != null)
                {
                    Vector3 s = root.localScale;
                    s.x = -s.x;
                    root.localScale = s;
                }
            }

            HideDirectSkeletonGraphics(targetSkeletonParent, ShouldKeepSpeSkeleton(entry) ? GetActiveSpeSkeletonTransform(targetSkeletonParent) : null);
            return true;
        }

        private static bool NeedsOverrideRootFlip(SpeHeroSkeletonOverrideEntry entry, Transform targetSkeletonParent)
        {
            if (ShouldKeepSpeSkeleton(entry)) return false;
            FaceContainerKind containerKind = GetFaceContainerKind(targetSkeletonParent);
            return containerKind == FaceContainerKind.Hud || containerKind == FaceContainerKind.Detail;
        }

        public static bool TryApplyAfterOriginalFailure(HeroData heroData, Transform targetSkeletonParent, Exception originalException)
        {
            if (heroData == null || targetSkeletonParent == null) return false;

            SpeHeroSkeletonOverrideEntry entry;
            if (!SpeHeroSkeletonOverrideRegistry.TryGet(heroData, out entry) || entry == null) return false;
            if (!ShouldApply(heroData, entry)) return false;

            DestroyExisting(targetSkeletonParent);

            SkeletonGraphic reference = FindDirectReferenceSkeleton(targetSkeletonParent);
            RectTransform referenceRect = reference == null ? null : reference.GetComponent<RectTransform>();
            if (referenceRect == null) referenceRect = targetSkeletonParent.GetComponent<RectTransform>();
            if (referenceRect == null)
            {
                LoggerManager.Warning("SpeHeroSkeleton fallback skipped because no RectTransform was found after original failure. heroID=" + heroData.heroID);
                return false;
            }

            bool applied = TryApplyByResourceType(heroData, entry, targetSkeletonParent, referenceRect);

            if (!applied) return false;

            HideDirectSkeletonGraphics(targetSkeletonParent, ShouldKeepSpeSkeleton(entry) ? GetActiveSpeSkeletonTransform(targetSkeletonParent) : null);
            LoggerManager.Warning("Suppressed SetSkeletonGraphic exception after applying SpeHeroSkeleton fallback. heroID=" + heroData.heroID +
                                  ", heroName=" + Safe(heroData.heroName) +
                                  ", exception=" + GetExceptionSummary(originalException) +
                                  ", ui=" + GetTransformPath(targetSkeletonParent));
            return true;
        }

        public static bool CanApply(HeroData heroData)
        {
            if (heroData == null) return false;

            SpeHeroSkeletonOverrideEntry entry;
            if (!SpeHeroSkeletonOverrideRegistry.TryGet(heroData, out entry) || entry == null) return false;

            return ShouldApply(heroData, entry);
        }

        private static bool TryApplyByResourceType(HeroData heroData, SpeHeroSkeletonOverrideEntry entry, Transform targetSkeletonParent, RectTransform referenceRect)
        {
            if (SpeHeroSkeletonOverrideRegistry.IsPrefab(entry.ResourcePath))
            {
                return TryApplyPrefab(heroData, entry, targetSkeletonParent, referenceRect);
            }

            if (SpeHeroSkeletonOverrideRegistry.IsSupportedImage(entry.ResourcePath))
            {
                return TryApplyStaticImage(entry, targetSkeletonParent, referenceRect);
            }

            return TryApplySpineSkeleton(heroData, entry, targetSkeletonParent);
        }

        private static bool ShouldKeepSpeSkeleton(SpeHeroSkeletonOverrideEntry entry)
        {
            if (entry == null) return false;
            return !SpeHeroSkeletonOverrideRegistry.IsPrefab(entry.ResourcePath) &&
                   !SpeHeroSkeletonOverrideRegistry.IsSupportedImage(entry.ResourcePath);
        }

        public static RectTransform GetProbeReferenceRect(Transform targetSkeletonParent)
        {
            if (targetSkeletonParent == null) return null;

            RectTransform referenceRect = null;
            SkeletonGraphic reference = FindDirectReferenceSkeleton(targetSkeletonParent);
            if (reference != null) referenceRect = reference.GetComponent<RectTransform>();
            if (referenceRect != null) return referenceRect;

            return targetSkeletonParent.GetComponent<RectTransform>();
        }

        public static void CleanupOrphanScenePrefabBridges()
        {
            GameObject globalRoot = GameObject.Find(GlobalSceneBridgeRootName);
            if (globalRoot == null) return;

            System.Collections.Generic.HashSet<string> activeRenderTextureNames = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            PruneSceneBridgeHandles(activeRenderTextureNames);

            RawImage[] images = FindRawImagesIncludingInactive();
            for (int i = 0; i < images.Length; i++)
            {
                RawImage image = images[i];
                if (image == null) continue;

                RenderTexture renderTexture = image.texture as RenderTexture;
                if (renderTexture == null || renderTexture.name == null || !renderTexture.name.StartsWith(SceneBridgeTexturePrefix, StringComparison.Ordinal)) continue;

                activeRenderTextureNames.Add(renderTexture.name);
            }

            int removed = 0;
            Transform rootTransform = globalRoot.transform;
            for (int i = rootTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = rootTransform.GetChild(i);
                if (child == null) continue;

                string childName = child.name;
                if (string.IsNullOrEmpty(childName) || !childName.StartsWith(SceneBridgeTexturePrefix, StringComparison.Ordinal)) continue;
                if (activeRenderTextureNames.Contains(childName)) continue;

                ReleaseSceneBridgeCameraRenderTextures(child);
                UnityEngine.Object.Destroy(child.gameObject);
                UnregisterSceneBridgeHandle(childName);
                removed++;
            }

            if (removed > 0)
            {
                LoggerManager.Debug("Cleaned orphan scene prefab bridge root(s): " + removed);
            }
        }

        private static bool ShouldApply(HeroData heroData, SpeHeroSkeletonOverrideEntry entry)
        {
            if (entry.ApplyWhen == SpeHeroSkeletonApplyWhen.UseSpeSkeleton && !SpeHeroSkeletonPolicy.IsPlayerSpeSkeletonEnabled(heroData))
            {
                LoggerManager.Debug("SpeHeroSkeleton override skipped by applyWhen=UseSpeSkeleton. heroID=" + heroData.heroID + ", heroName=" + Safe(heroData.heroName));
                return false;
            }

            return true;
        }

        private static bool TryApplyStaticImage(SpeHeroSkeletonOverrideEntry entry, Transform targetSkeletonParent, RectTransform referenceRect)
        {
            Texture2D texture;
            if (!SpeHeroSkeletonOverrideRegistry.TryLoadTexture(entry, out texture) || texture == null) return false;

            GameObject root = CreateRoot(targetSkeletonParent, referenceRect);
            GameObject content = new GameObject(ContentName);
            content.transform.SetParent(root.transform, false);

            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);

            RawImage image = content.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;

            ApplyStaticImageLayout(entry, texture, referenceRect, contentRect);
            return true;
        }

        /// <summary>
        /// 为新创建的 SkeletonGraphic 应用默认材质和动画设置：
        /// 材质用 spineDefaultGraphicMaterial，动画从同容器已有骨架复制。
        /// </summary>
        private static void ApplySkeletonDefaults(SkeletonGraphic sg, Transform parentRef)
        {
            if (sg == null || parentRef == null) return;

            if (!string.IsNullOrEmpty(sg.startingAnimation)) return;

            for (int ci = 0; ci < parentRef.childCount; ci++)
            {
                SkeletonGraphic refSg = parentRef.GetChild(ci).GetComponent<SkeletonGraphic>();
                if (refSg != null && !string.IsNullOrEmpty(refSg.startingAnimation))
                {
                    sg.startingAnimation = refSg.startingAnimation;
                    sg.startingLoop = refSg.startingLoop;
                    break;
                }
            }
        }

        /// <summary>
        /// 对 SpeSkeleton 执行最终初始化：激活、设数据、初始化骨骼、匹配边界。
        /// 朝向基准来自 SpeSkeleton transform；Mapping flipX 只额外作用在 Skeleton.ScaleX 上。
        /// </summary>
        private static bool FinalizeSpeSkeleton(SpeHeroSkeletonOverrideEntry entry, SkeletonGraphic skeletonGraphic, SkeletonDataAsset skeletonDataAsset, Transform targetSkeletonParent)
        {
            if (skeletonGraphic == null) return false;

            skeletonGraphic.gameObject.SetActive(true);
            skeletonGraphic.skeletonDataAsset = skeletonDataAsset;

            try
            {
                skeletonGraphic.Initialize(true);
                ApplySkeletonFlipX(entry, skeletonGraphic);
                EnsureSkeletonAnimationPlaying(skeletonGraphic);
                skeletonGraphic.MatchRectTransformWithBounds();
                skeletonGraphic.SetMaterialDirty();
                skeletonGraphic.SetVerticesDirty();
                return true;
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("Failed to initialize SpeHeroSkeleton spine override: " + entry.SourceDescription + " - " + ex.Message);
                return false;
            }
        }

        private static bool TryApplySpineSkeleton(HeroData heroData, SpeHeroSkeletonOverrideEntry entry, Transform targetSkeletonParent)
        {
            SkeletonDataAsset skeletonDataAsset;
            if (!SpeHeroSkeletonOverrideRegistry.TryLoadSkeletonDataAsset(entry, out skeletonDataAsset) || skeletonDataAsset == null) return false;

            Transform speSkeletonTransform = targetSkeletonParent == null ? null : targetSkeletonParent.Find("SpeSkeleton");
            if (speSkeletonTransform == null)
            {
                if (TryApplyHudSpineSkeletonCompatibilityLayer(heroData, entry, targetSkeletonParent, skeletonDataAsset))
                    return true;

                GameObjectController goc = GameObjectController.Instance;
                Material sgMat = goc != null ? goc.spineDefaultGraphicMaterial : null;
                SkeletonGraphic sg = SkeletonGraphic.NewSkeletonGraphicGameObject(skeletonDataAsset, targetSkeletonParent, sgMat);
                sg.gameObject.name = "SpeSkeleton";
                ApplySkeletonDefaults(sg, targetSkeletonParent);
                RectTransform rt = sg.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                ApplySpeSkeletonVanillaCreationLayout(heroData, targetSkeletonParent, rt);
                sg.raycastTarget = false;
                speSkeletonTransform = sg.transform;
                LoggerManager.Debug("Created SpeSkeleton container for spine override: " + entry.SourceDescription +
                    " ui=" + GetTransformPath(targetSkeletonParent));
            }

            SkeletonGraphic skeletonGraphic = speSkeletonTransform.GetComponent<SkeletonGraphic>();
            if (skeletonGraphic == null)
            {
                LoggerManager.Warning("SpeHeroSkeleton spine override skipped because SpeSkeleton has no SkeletonGraphic: " + entry.SourceDescription);
                return false;
            }

            ApplySkeletonDefaults(skeletonGraphic, targetSkeletonParent);

            if (!FinalizeSpeSkeleton(entry, skeletonGraphic, skeletonDataAsset, targetSkeletonParent))
                return false;

            LoggerManager.Debug("Applied SpeHeroSkeleton spine override: " + entry.SourceDescription +
                                " ui=" + GetTransformPath(targetSkeletonParent));
            return true;
        }

        private static bool TryApplyHudSpineSkeletonCompatibilityLayer(HeroData heroData, SpeHeroSkeletonOverrideEntry entry, Transform targetSkeletonParent, SkeletonDataAsset skeletonDataAsset)
        {
            if (entry == null || targetSkeletonParent == null || skeletonDataAsset == null) return false;
            if (GetFaceContainerKind(targetSkeletonParent) != FaceContainerKind.Hud)
            {
                LogHudLikeContainerIfNeeded(targetSkeletonParent);
                return false;
            }

            RectTransform referenceRect = targetSkeletonParent.GetComponent<RectTransform>();
            if (referenceRect == null)
            {
                LoggerManager.Warning("HUD SpeHeroSkeleton compatibility layer skipped because target RectTransform was not found: " + entry.SourceDescription);
                return false;
            }

            GameObjectController goc = GameObjectController.Instance;
            Material mat = goc != null ? goc.spineDefaultGraphicMaterial : null;
            SkeletonGraphic skeletonGraphic = SkeletonGraphic.NewSkeletonGraphicGameObject(skeletonDataAsset, targetSkeletonParent, mat);
            if (skeletonGraphic == null)
            {
                LoggerManager.Warning("HUD SpeHeroSkeleton compatibility layer skipped because SkeletonGraphic creation failed: " + entry.SourceDescription);
                return false;
            }

            GameObject content = skeletonGraphic.gameObject;
            content.name = "SpeSkeleton";
            RectTransform contentRect = content.GetComponent<RectTransform>();
            if (contentRect == null)
            {
                UnityEngine.Object.Destroy(content);
                LoggerManager.Warning("HUD SpeHeroSkeleton compatibility layer skipped because created SkeletonGraphic has no RectTransform: " + entry.SourceDescription);
                return false;
            }

            ApplyHudStandardSpeSkeletonLayout(heroData, targetSkeletonParent, contentRect);
            skeletonGraphic.raycastTarget = false;
            ApplySkeletonDefaults(skeletonGraphic, targetSkeletonParent);
            if (!FinalizeSpeSkeleton(entry, skeletonGraphic, skeletonDataAsset, targetSkeletonParent))
            {
                UnityEngine.Object.Destroy(content);
                return false;
            }

            ApplyHudSpeSkeletonTemplateOffset(heroData, contentRect);
            ContainerProbe.LogSpeSkeletonLayout(heroData, targetSkeletonParent, content.transform, "HudCompatibilityLayer", entry.SourceDescription);

            LoggerManager.Debug("Applied HUD SpeHeroSkeleton compatibility layer: " + entry.SourceDescription +
                                " ui=" + GetTransformPath(targetSkeletonParent));
            return true;
        }

        private static Material GetDefaultSkeletonGraphicMaterial()
        {
            try
            {
                GameObjectController controller = GameObjectController.Instance;
                if (controller != null && controller.skeletonGraphicDefault != null) return controller.skeletonGraphicDefault;
            }
            catch
            {
            }

            return null;
        }

        private static void ConfigureSkeletonGraphicMaterials(SpeHeroSkeletonOverrideEntry entry, SkeletonGraphic skeletonGraphic, SkeletonDataAsset skeletonDataAsset)
        {
            if (entry == null || skeletonGraphic == null || skeletonDataAsset == null) return;

            Material atlasMaterial = GetPrimaryAtlasMaterial(skeletonDataAsset);
            if (!UsesStraightAlphaTexture(atlasMaterial)) return;

            StoreOriginalSkeletonGraphicMaterials(skeletonGraphic);

            Material template = skeletonGraphic.material;
            if (template == null) template = skeletonGraphic.materialForRendering;
            if (template == null) template = atlasMaterial;

            skeletonGraphic.material = GetOrCreateSkeletonGraphicMaterial(ref entry.CachedSkeletonGraphicMaterial, template, true, "Normal");

            if (skeletonGraphic.additiveMaterial != null)
            {
                skeletonGraphic.additiveMaterial = GetOrCreateSkeletonGraphicMaterial(ref entry.CachedSkeletonGraphicAdditiveMaterial, skeletonGraphic.additiveMaterial, true, "Additive");
            }

            if (skeletonGraphic.multiplyMaterial != null)
            {
                skeletonGraphic.multiplyMaterial = GetOrCreateSkeletonGraphicMaterial(ref entry.CachedSkeletonGraphicMultiplyMaterial, skeletonGraphic.multiplyMaterial, true, "Multiply");
            }

            if (skeletonGraphic.screenMaterial != null)
            {
                skeletonGraphic.screenMaterial = GetOrCreateSkeletonGraphicMaterial(ref entry.CachedSkeletonGraphicScreenMaterial, skeletonGraphic.screenMaterial, true, "Screen");
            }
        }

        private static Material GetPrimaryAtlasMaterial(SkeletonDataAsset skeletonDataAsset)
        {
            try
            {
                if (skeletonDataAsset.atlasAssets == null || skeletonDataAsset.atlasAssets.Length <= 0) return null;
                AtlasAssetBase atlasAsset = skeletonDataAsset.atlasAssets[0];
                if (atlasAsset == null || atlasAsset.MaterialCount <= 0) return null;
                return atlasAsset.PrimaryMaterial;
            }
            catch
            {
                return null;
            }
        }

        private static bool UsesStraightAlphaTexture(Material material)
        {
            if (material == null || !material.HasProperty(StraightAlphaInputProperty)) return false;
            return material.GetFloat(StraightAlphaInputProperty) > 0.5f;
        }

        private static Material GetOrCreateSkeletonGraphicMaterial(ref Material cachedMaterial, Material template, bool straightAlphaTexture, string suffix)
        {
            if (cachedMaterial == null)
            {
                cachedMaterial = new Material(template);
                cachedMaterial.name = "TheResourceOfLong_SkeletonGraphic_" + suffix + "_" + template.name;
                UnityEngine.Object.DontDestroyOnLoad(cachedMaterial);
            }

            ApplyStraightAlphaSetting(cachedMaterial, straightAlphaTexture);
            return cachedMaterial;
        }

        private static void ApplyStraightAlphaSetting(Material material, bool enabled)
        {
            if (material == null) return;

            if (material.HasProperty(StraightAlphaInputProperty))
            {
                material.SetFloat(StraightAlphaInputProperty, enabled ? 1f : 0f);
            }

            if (enabled) material.EnableKeyword(StraightAlphaInputKeyword);
            else material.DisableKeyword(StraightAlphaInputKeyword);

            if (material.HasProperty(CanvasGroupCompatibleProperty))
            {
                material.SetFloat(CanvasGroupCompatibleProperty, 1f);
                material.EnableKeyword(CanvasGroupCompatibleKeyword);
            }
        }

        private static void StoreOriginalSkeletonGraphicMaterials(SkeletonGraphic skeletonGraphic)
        {
            if (skeletonGraphic == null) return;

            int key = skeletonGraphic.GetInstanceID();
            if (OriginalSkeletonGraphicMaterials.ContainsKey(key)) return;

            SkeletonGraphicMaterialState state = new SkeletonGraphicMaterialState();
            state.Material = skeletonGraphic.material;
            state.AdditiveMaterial = skeletonGraphic.additiveMaterial;
            state.MultiplyMaterial = skeletonGraphic.multiplyMaterial;
            state.ScreenMaterial = skeletonGraphic.screenMaterial;
            OriginalSkeletonGraphicMaterials[key] = state;
        }

        private static void RestoreManagedSkeletonGraphicMaterials(Transform targetSkeletonParent)
        {
            if (targetSkeletonParent == null) return;

            SkeletonGraphic[] skeletonGraphics = targetSkeletonParent.GetComponentsInChildren<SkeletonGraphic>(true);
            for (int i = 0; i < skeletonGraphics.Length; i++)
            {
                RestoreSkeletonGraphicMaterials(skeletonGraphics[i]);
                RestoreSkeletonGraphicScaleX(skeletonGraphics[i]);
            }
        }

        private static void RestoreSkeletonGraphicMaterials(SkeletonGraphic skeletonGraphic)
        {
            if (skeletonGraphic == null) return;

            int key = skeletonGraphic.GetInstanceID();
            SkeletonGraphicMaterialState state;
            if (!OriginalSkeletonGraphicMaterials.TryGetValue(key, out state)) return;

            skeletonGraphic.material = state.Material;
            skeletonGraphic.additiveMaterial = state.AdditiveMaterial;
            skeletonGraphic.multiplyMaterial = state.MultiplyMaterial;
            skeletonGraphic.screenMaterial = state.ScreenMaterial;
            OriginalSkeletonGraphicMaterials.Remove(key);
        }

        private static void StoreOriginalSkeletonGraphicScaleX(SkeletonGraphic skeletonGraphic)
        {
            if (skeletonGraphic == null || skeletonGraphic.Skeleton == null) return;

            int key = skeletonGraphic.GetInstanceID();
            if (OriginalSkeletonGraphicScaleX.ContainsKey(key)) return;
            OriginalSkeletonGraphicScaleX[key] = skeletonGraphic.Skeleton.ScaleX;
        }

        private static void RestoreSkeletonGraphicScaleX(SkeletonGraphic skeletonGraphic)
        {
            if (skeletonGraphic == null || skeletonGraphic.Skeleton == null) return;

            int key = skeletonGraphic.GetInstanceID();
            float scaleX;
            if (!OriginalSkeletonGraphicScaleX.TryGetValue(key, out scaleX)) return;

            skeletonGraphic.Skeleton.ScaleX = scaleX;
            OriginalSkeletonGraphicScaleX.Remove(key);
        }

        private static bool TryApplyPrefab(HeroData heroData, SpeHeroSkeletonOverrideEntry entry, Transform targetSkeletonParent, RectTransform referenceRect)
        {
            GameObject prefab;
            if (!SpeHeroSkeletonOverrideRegistry.TryLoadPrefab(entry, out prefab) || prefab == null) return false;

            if (HasSceneRenderer(prefab) && !HasUiGraphic(prefab))
            {
                RectTransform sceneReferenceRect = GetScenePrefabReferenceRect(targetSkeletonParent, referenceRect);
                GameObject sceneRoot = CreateRoot(targetSkeletonParent, sceneReferenceRect);
                if (TryApplyScenePrefabBridge(heroData, entry, prefab, targetSkeletonParent, sceneRoot.transform, sceneReferenceRect)) return true;

                UnityEngine.Object.Destroy(sceneRoot);
                return false;
            }

            GameObject root = CreateRoot(targetSkeletonParent, referenceRect);

            if (!HasUiGraphic(prefab) && prefab.GetComponent<RectTransform>() == null)
            {
                LoggerManager.Warning("SpeHeroSkeleton prefab has no UGUI Graphic/RectTransform and no scene Renderer: " + entry.SourceDescription);
                UnityEngine.Object.Destroy(root);
                return false;
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab, root.transform, false);
            instance.name = ContentName;
            DisableRaycastTargets(instance);

            RectTransform contentRect = instance.GetComponent<RectTransform>();
            if (contentRect != null)
            {
                float scale = entry.Scale;
                contentRect.localScale = new Vector3(scale, scale, 1f);
                ApplyTransformFlipX(entry, contentRect, Mathf.Abs(scale));
                contentRect.anchoredPosition = GetAnchoredPosition(entry, referenceRect, GetRectSize(contentRect), scale);
                NormalizeMaskedMultiGraphicPrefab(instance, contentRect, targetSkeletonParent);
            }
            else
            {
                instance.transform.localPosition = new Vector3(GetEffectiveOffsetX(entry), entry.OffsetY, 0f);
                float scale = entry.Scale;
                instance.transform.localScale = new Vector3(scale, scale, scale);
                ApplyTransformFlipX(entry, instance.transform, Mathf.Abs(scale));
            }

            ScenePrefabUiProbe.LogUiPrefab(heroData, targetSkeletonParent, root.transform, instance.transform, referenceRect, entry.SourceDescription);
            return true;
        }

        private static void NormalizeMaskedMultiGraphicPrefab(GameObject instance, RectTransform contentRect, Transform targetSkeletonParent)
        {
            if (instance == null || contentRect == null || targetSkeletonParent == null) return;
            if (!HasAncestorUiMask(targetSkeletonParent)) return;

            Graphic[] graphics = instance.GetComponentsInChildren<Graphic>(true);
            if (graphics == null || graphics.Length <= 1) return;

            Vector2 size = GetRectSize(contentRect);
            if (size.x <= 0f || size.y <= 0f) return;

            int normalized = 0;
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null || graphic.transform == instance.transform) continue;

                RectTransform rect = graphic.GetComponent<RectTransform>();
                if (rect == null) continue;

                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = size;
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one;
                graphic.SetMaterialDirty();
                graphic.SetVerticesDirty();
                normalized++;
            }

            if (normalized > 0)
            {
                LoggerManager.Debug("Normalized masked multi-Graphic prefab rects: count=" + normalized +
                                    " ui=" + GetTransformPath(targetSkeletonParent));
            }
        }

        private static bool HasAncestorUiMask(Transform transform)
        {
            Transform current = transform;
            while (current != null)
            {
                if (current.GetComponent<RectMask2D>() != null) return true;
                if (current.GetComponent<Mask>() != null) return true;
                current = current.parent;
            }

            return false;
        }

        private static bool TryApplyScenePrefabBridge(HeroData heroData, SpeHeroSkeletonOverrideEntry entry, GameObject prefab, Transform targetSkeletonParent, Transform root, RectTransform referenceRect)
        {
            Vector2 containerSize = GetReferenceSize(referenceRect);
            float renderScale = entry.SceneRenderScale <= 0f ? 1f : entry.SceneRenderScale;
            int textureWidth = Mathf.Clamp(Mathf.RoundToInt(containerSize.x * renderScale), 64, 4096);
            int textureHeight = Mathf.Clamp(Mathf.RoundToInt(containerSize.y * renderScale), 64, 4096);

            RenderTexture renderTexture = new RenderTexture(textureWidth, textureHeight, 16, RenderTextureFormat.ARGB32);
            renderTexture.name = SceneBridgeTexturePrefix + (++_sceneBridgeIndex);
            renderTexture.filterMode = FilterMode.Bilinear;
            renderTexture.Create();

            GameObject content = new GameObject(ContentName);
            content.transform.SetParent(root, false);

            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            float scale = entry.Scale;
            if (UseStableScenePrefabLayout(entry))
            {
                ApplyScenePrefabImageLayout(entry, referenceRect, contentRect, textureWidth, textureHeight, renderScale);
            }
            else
            {
                contentRect.sizeDelta = new Vector2(containerSize.x * scale, containerSize.y * scale);
                contentRect.anchoredPosition = GetAnchoredPosition(entry, referenceRect, contentRect.sizeDelta, 1f);
            }

            contentRect.localScale = Vector3.one;

            ApplyTransformFlipX(entry, contentRect, 1f);

            RawImage image = content.AddComponent<RawImage>();
            image.texture = renderTexture;
            image.raycastTarget = false;

            GameObject sceneRoot = new GameObject(renderTexture.name);
            sceneRoot.transform.SetParent(GetGlobalSceneBridgeRoot().transform, false);
            sceneRoot.transform.position = GetNextSceneBridgePosition();
            SetLayerRecursively(sceneRoot, SceneBridgeLayer);

            GameObject instance = UnityEngine.Object.Instantiate(prefab, sceneRoot.transform, false);
            instance.name = ContentName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            SetLayerRecursively(instance, SceneBridgeLayer);
            ConfigureScenePrefabRuntime(instance, entry);
            string prefabRuntimeProbe = ProbeScenePrefabRuntime(instance);

            Bounds bounds;
            if (!TryGetRendererBounds(instance, out bounds))
            {
                LoggerManager.Warning("SpeHeroSkeleton scene prefab has no active Renderer bounds: " + entry.SourceDescription +
                                      (string.IsNullOrEmpty(prefabRuntimeProbe) ? string.Empty : " " + prefabRuntimeProbe));
                DestroySceneBridgesForRenderTextures(root);
                ReleaseRenderTextures(root);
                UnityEngine.Object.Destroy(root.gameObject);
                return false;
            }

            GameObject cameraObject = new GameObject(SceneBridgeCameraName);
            cameraObject.transform.SetParent(sceneRoot.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.targetTexture = renderTexture;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.allowHDR = false;
            camera.cullingMask = 1 << SceneBridgeLayer;
            camera.aspect = textureHeight <= 0 ? 1f : (float)textureWidth / textureHeight;

            float aspect = textureHeight <= 0 ? 1f : (float)textureWidth / textureHeight;
            float halfHeight = Mathf.Max(bounds.extents.y, bounds.extents.x / Mathf.Max(aspect, 0.01f));
            if (halfHeight <= 0.01f) halfHeight = 1f;
            if (UseStableScenePrefabLayout(entry)) halfHeight *= 1f + entry.ScenePadding;
            float cameraZoom = entry.SceneCameraZoom <= 0f ? 1f : entry.SceneCameraZoom;
            camera.orthographicSize = halfHeight * 1.05f / cameraZoom;
            float cameraX = bounds.center.x;
            float cameraY = bounds.center.y;
            if (UseStableScenePrefabLayout(entry))
            {
                cameraX += bounds.size.x * entry.SceneCameraOffsetX;
                cameraY += bounds.size.y * entry.SceneCameraOffsetY;
            }

            camera.transform.position = new Vector3(cameraX, cameraY, bounds.center.z - 10f);
            camera.transform.rotation = Quaternion.identity;
            SceneBridgeRenderTextureDriver renderDriver = cameraObject.AddComponent<SceneBridgeRenderTextureDriver>();
            renderDriver.TargetCamera = camera;
            renderDriver.FramesPerSecond = 30f;
            RegisterSceneBridgeHandle(renderTexture.name, root, sceneRoot.transform, image, renderTexture);

            string renderProbe = string.Empty;
            try
            {
                renderDriver.RenderNow();
                renderProbe = ProbeRenderTextureAlpha(renderTexture);
            }
            catch (Exception ex)
            {
                renderProbe = "alphaProbeError=" + ex.GetType().Name + ":" + ex.Message;
            }

            LoggerManager.Debug("Applied scene prefab bridge: " + entry.SourceDescription +
                                " rt=" + textureWidth + "x" + textureHeight +
                                " sceneRenderScale=" + renderScale +
                                " sceneCameraZoom=" + cameraZoom +
                                " scenePadding=" + entry.ScenePadding +
                                " sceneCameraOffset=(" + entry.SceneCameraOffsetX + "," + entry.SceneCameraOffsetY + ")" +
                                " bounds=" + bounds.size +
                                (string.IsNullOrEmpty(prefabRuntimeProbe) ? string.Empty : " " + prefabRuntimeProbe) +
                                (string.IsNullOrEmpty(renderProbe) ? string.Empty : " " + renderProbe) +
                                " ui=" + GetTransformPath(root));
            ContainerProbe.LogSpeSkeletonLayout(heroData, targetSkeletonParent, root, "ScenePrefabBridgeRoot", entry.SourceDescription);
            ContainerProbe.LogSpeSkeletonLayout(heroData, targetSkeletonParent, content.transform, "ScenePrefabBridgeContent",
                entry.SourceDescription + ";rt=" + textureWidth + "x" + textureHeight + ";renderScale=" + renderScale +
                (string.IsNullOrEmpty(prefabRuntimeProbe) ? string.Empty : ";" + prefabRuntimeProbe) +
                (string.IsNullOrEmpty(renderProbe) ? string.Empty : ";" + renderProbe));
            ScenePrefabUiProbe.TrackAreaHeroList(heroData, targetSkeletonParent, root, content.transform, image, renderTexture,
                entry.SourceDescription + ";rt=" + textureWidth + "x" + textureHeight + ";renderScale=" + renderScale +
                (string.IsNullOrEmpty(renderProbe) ? string.Empty : ";" + renderProbe));
            return true;
        }

        private static string ProbeRenderTextureAlpha(RenderTexture renderTexture)
        {
            if (renderTexture == null) return "alphaProbe=missingRT";

            RenderTexture previous = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                RenderTexture.active = renderTexture;
                texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0, false);
                texture.Apply(false, false);

                int samplesX = Mathf.Min(16, Mathf.Max(1, renderTexture.width));
                int samplesY = Mathf.Min(16, Mathf.Max(1, renderTexture.height));
                int samples = 0;
                int nonTransparent = 0;
                float maxAlpha = 0f;
                float sumAlpha = 0f;

                for (int y = 0; y < samplesY; y++)
                {
                    int py = Mathf.Clamp(Mathf.RoundToInt((y + 0.5f) * renderTexture.height / samplesY), 0, renderTexture.height - 1);
                    for (int x = 0; x < samplesX; x++)
                    {
                        int px = Mathf.Clamp(Mathf.RoundToInt((x + 0.5f) * renderTexture.width / samplesX), 0, renderTexture.width - 1);
                        float alpha = texture.GetPixel(px, py).a;
                        samples++;
                        sumAlpha += alpha;
                        if (alpha > maxAlpha) maxAlpha = alpha;
                        if (alpha > 0.01f) nonTransparent++;
                    }
                }

                float avgAlpha = samples <= 0 ? 0f : sumAlpha / samples;
                return "alphaProbe=samples:" + samples +
                       ",nonTransparent:" + nonTransparent +
                       ",max:" + maxAlpha.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                       ",avg:" + avgAlpha.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            }
            finally
            {
                RenderTexture.active = previous;
                if (texture != null) UnityEngine.Object.Destroy(texture);
            }
        }

        private static bool UseStableScenePrefabLayout(SpeHeroSkeletonOverrideEntry entry)
        {
            return entry != null &&
                   (entry.HasScenePadding || entry.HasSceneCameraOffsetX || entry.HasSceneCameraOffsetY);
        }

        private static void ApplyScenePrefabImageLayout(SpeHeroSkeletonOverrideEntry entry, RectTransform referenceRect, RectTransform contentRect, int textureWidth, int textureHeight, float renderScale)
        {
            if (renderScale <= 0f) renderScale = 1f;
            float imageW = textureWidth <= 0 ? 1f : textureWidth / renderScale;
            float imageH = textureHeight <= 0 ? 1f : textureHeight / renderScale;
            Vector2 containerSize = GetReferenceSize(referenceRect);
            float containerW = containerSize.x <= 0f ? imageW : containerSize.x;
            float containerH = containerSize.y <= 0f ? imageH : containerSize.y;

            float autoScale = 1f;
            if (entry.FitMode == SpeHeroSkeletonFitMode.FitHeight)
            {
                autoScale = containerH / imageH;
            }
            else if (entry.FitMode == SpeHeroSkeletonFitMode.Cover)
            {
                autoScale = Mathf.Max(containerW / imageW, containerH / imageH);
            }
            else if (entry.FitMode == SpeHeroSkeletonFitMode.Contain)
            {
                autoScale = Mathf.Min(containerW / imageW, containerH / imageH);
            }

            float finalScale = autoScale * entry.Scale;
            Vector2 contentSize = new Vector2(imageW * finalScale, imageH * finalScale);
            contentRect.sizeDelta = contentSize;
            contentRect.anchoredPosition = GetAnchoredPosition(entry, referenceRect, contentSize, 1f);
        }

        private static void DestroyExisting(Transform targetSkeletonParent)
        {
            Transform existing = targetSkeletonParent.Find(OverrideRootName);
            if (existing != null)
            {
                DestroySceneBridgesForRenderTextures(existing);
                ReleaseRenderTextures(existing);
                UnityEngine.Object.Destroy(existing.gameObject);
            }
        }

        private static GameObject CreateRoot(Transform targetSkeletonParent, RectTransform referenceRect)
        {
            GameObject root = new GameObject(OverrideRootName);
            root.transform.SetParent(targetSkeletonParent, false);

            RectTransform rootRect = root.AddComponent<RectTransform>();
            if (referenceRect.transform == targetSkeletonParent)
            {
                CopyParentRectTransformToChild(rootRect);
                root.transform.SetAsLastSibling();
            }
            else
            {
                CopyRectTransform(referenceRect, rootRect);
                root.transform.SetSiblingIndex(referenceRect.transform.GetSiblingIndex() + 1);
            }

            return root;
        }

        private static void ApplyStaticImageLayout(SpeHeroSkeletonOverrideEntry entry, Texture2D texture, RectTransform referenceRect, RectTransform contentRect)
        {
            float imageW = texture.width <= 0 ? 1f : texture.width;
            float imageH = texture.height <= 0 ? 1f : texture.height;

            Rect rect = referenceRect.rect;
            float containerW = Mathf.Abs(rect.width);
            float containerH = Mathf.Abs(rect.height);
            if (containerW <= 0f) containerW = Mathf.Abs(referenceRect.sizeDelta.x);
            if (containerH <= 0f) containerH = Mathf.Abs(referenceRect.sizeDelta.y);
            if (containerW <= 0f) containerW = imageW;
            if (containerH <= 0f) containerH = imageH;

            float autoScale = 1f;
            if (entry.FitMode == SpeHeroSkeletonFitMode.FitHeight)
            {
                autoScale = containerH / imageH;
            }
            else if (entry.FitMode == SpeHeroSkeletonFitMode.Cover)
            {
                autoScale = Mathf.Max(containerW / imageW, containerH / imageH);
            }
            else if (entry.FitMode == SpeHeroSkeletonFitMode.Contain)
            {
                autoScale = Mathf.Min(containerW / imageW, containerH / imageH);
            }

            float finalScale = autoScale * entry.Scale;
            Vector2 contentSize = new Vector2(imageW * finalScale, imageH * finalScale);
            contentRect.sizeDelta = contentSize;
            contentRect.anchoredPosition = GetAnchoredPosition(entry, referenceRect, contentSize, 1f);
            contentRect.localScale = Vector3.one;
            ApplyTransformFlipX(entry, contentRect, 1f);
        }

        private static void ApplyTransformFlipX(SpeHeroSkeletonOverrideEntry entry, Transform transform, float baseScaleX)
        {
            if (entry == null || transform == null) return;

            Vector3 scale = transform.localScale;
            float magnitude = Mathf.Abs(baseScaleX);
            if (magnitude <= 0f) magnitude = Mathf.Abs(scale.x);
            if (magnitude <= 0f) magnitude = 1f;

            float oldFlipX = scale.x;
            scale.x = entry.FlipX ? -magnitude : magnitude;
            LoggerManager.Debug($"[FlipX] entry.FlipX={entry.FlipX} path={GetTransformPath(transform)} baseScaleX={baseScaleX} before=({oldFlipX}), after=({scale.x})");
            transform.localScale = scale;
        }

        private static void ApplySkeletonFlipX(SpeHeroSkeletonOverrideEntry entry, SkeletonGraphic skeletonGraphic)
        {
            if (entry == null || skeletonGraphic == null || skeletonGraphic.Skeleton == null) return;

            int key = skeletonGraphic.GetInstanceID();
            float baseScaleX;
            if (!OriginalSkeletonGraphicScaleX.TryGetValue(key, out baseScaleX))
            {
                baseScaleX = skeletonGraphic.Skeleton.ScaleX;
                if (Mathf.Approximately(baseScaleX, 0f)) baseScaleX = 1f;
                OriginalSkeletonGraphicScaleX[key] = baseScaleX;
            }

            if (Mathf.Approximately(baseScaleX, 0f)) baseScaleX = 1f;
            skeletonGraphic.Skeleton.ScaleX = entry.FlipX ? -baseScaleX : baseScaleX;
        }

        private static void EnsureSkeletonAnimationPlaying(SkeletonGraphic skeletonGraphic)
        {
            if (skeletonGraphic == null || string.IsNullOrEmpty(skeletonGraphic.startingAnimation)) return;

            try
            {
                if (skeletonGraphic.AnimationState == null) return;
                skeletonGraphic.AnimationState.SetAnimation(0, skeletonGraphic.startingAnimation, skeletonGraphic.startingLoop);
            }
            catch (Exception ex)
            {
                LoggerManager.Debug("Failed to ensure SpeHeroSkeleton animation playing: " + ex.Message);
            }
        }

        private static Vector2 GetAnchoredPosition(SpeHeroSkeletonOverrideEntry entry, RectTransform referenceRect, Vector2 contentSize, float localScale)
        {
            Vector2 referenceSize = GetReferenceSize(referenceRect);
            Vector2 referencePivot = referenceRect == null ? new Vector2(0.5f, 0.5f) : referenceRect.pivot;
            float scaledW = contentSize.x * localScale;
            float scaledH = contentSize.y * localScale;

            float x = GetEffectiveOffsetX(entry);
            float y = entry.OffsetY;
            if (entry.HasAnchorX)
            {
                float targetX = (referencePivot.x - 0.5f) * referenceSize.x;
                float anchorX = (entry.AnchorX - 0.5f) * scaledW;
                x += targetX - anchorX;
            }

            if (entry.HasAnchorY)
            {
                float targetY = (referencePivot.y - 0.5f) * referenceSize.y;
                float anchorY = (entry.AnchorY - 0.5f) * scaledH;
                y += targetY - anchorY;
            }

            return new Vector2(x, y);
        }

        private static float GetEffectiveOffsetX(SpeHeroSkeletonOverrideEntry entry)
        {
            if (entry == null) return 0f;
            return entry.FlipX ? -entry.OffsetX : entry.OffsetX;
        }

        private static Vector2 GetRectSize(RectTransform rectTransform)
        {
            if (rectTransform == null) return Vector2.zero;

            Rect rect = rectTransform.rect;
            float width = Mathf.Abs(rect.width);
            float height = Mathf.Abs(rect.height);
            if (width <= 0f) width = Mathf.Abs(rectTransform.sizeDelta.x);
            if (height <= 0f) height = Mathf.Abs(rectTransform.sizeDelta.y);
            return new Vector2(width, height);
        }

        private static SkeletonGraphic FindDirectReferenceSkeleton(Transform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == null || child.name == OverrideRootName) continue;

                SkeletonGraphic skeleton = child.GetComponent<SkeletonGraphic>();
                if (skeleton != null) return skeleton;
            }

            return null;
        }

        private static RectTransform GetScenePrefabReferenceRect(Transform targetSkeletonParent, RectTransform fallbackReferenceRect)
        {
            if (targetSkeletonParent == null) return fallbackReferenceRect;

            Transform heroSkeleton = targetSkeletonParent.Find("HeroSkeleton0");
            if (heroSkeleton != null)
            {
                RectTransform rect = heroSkeleton.GetComponent<RectTransform>();
                if (rect != null) return rect;
            }

            SkeletonGraphic reference = FindDirectReferenceSkeleton(targetSkeletonParent);
            RectTransform referenceRect = reference == null ? null : reference.GetComponent<RectTransform>();
            return referenceRect == null ? fallbackReferenceRect : referenceRect;
        }

        private static void ApplyHudStandardSpeSkeletonLayout(HeroData heroData, Transform targetSkeletonParent, RectTransform contentRect)
        {
            if (targetSkeletonParent == null || contentRect == null) return;

            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;

            ApplyHudSpeSkeletonContentScale(targetSkeletonParent, contentRect);

            float zOffset = heroData != null && heroData.isFemale ? -0.062f : -0.072f;
            contentRect.localPosition = new Vector3(contentRect.localPosition.x, contentRect.localPosition.y, zOffset);
        }

        private static void ApplyHudSpeSkeletonTemplateOffset(HeroData heroData, RectTransform contentRect)
        {
            if (contentRect == null) return;

            contentRect.anchoredPosition = new Vector2(0f, HudSpeSkeletonTemplateAnchoredY);
            float zOffset = heroData != null && heroData.isFemale ? -0.062f : -0.072f;
            contentRect.localPosition = new Vector3(contentRect.localPosition.x, contentRect.localPosition.y, zOffset);
        }

        private static void ApplyHudSpeSkeletonContentScale(Transform targetSkeletonParent, Transform contentTransform)
        {
            if (contentTransform == null) return;

            float magnitude = Mathf.Abs(GetFirstChildScaleX(targetSkeletonParent));
            if (Mathf.Approximately(magnitude, 0f)) magnitude = Mathf.Abs(contentTransform.localScale.x);
            if (Mathf.Approximately(magnitude, 0f)) magnitude = 1f;

            contentTransform.localScale = new Vector3(-magnitude, magnitude, magnitude);
        }

        private static float GetFirstChildScaleX(Transform targetSkeletonParent)
        {
            return GetFirstChildScaleX(targetSkeletonParent, null);
        }

        private static float GetFirstChildScaleX(Transform targetSkeletonParent, Transform excluded)
        {
            if (targetSkeletonParent == null || targetSkeletonParent.childCount <= 0) return 1f;

            for (int i = 0; i < targetSkeletonParent.childCount; i++)
            {
                Transform child = targetSkeletonParent.GetChild(i);
                if (child == null || child == excluded || child.name == OverrideRootName) continue;
                return child.localScale.x;
            }

            return 1f;
        }

        private static void ApplySpeSkeletonVanillaCreationLayout(HeroData heroData, Transform targetSkeletonParent, Transform speSkeletonTransform)
        {
            if (speSkeletonTransform == null) return;

            float y = heroData != null && heroData.isFemale ? FemaleSpeSkeletonLocalY : MaleSpeSkeletonLocalY;
            speSkeletonTransform.localPosition = new Vector3(0f, y, 0f);
            ApplySpeSkeletonVanillaScale(targetSkeletonParent, speSkeletonTransform);
        }

        private static void ApplySpeSkeletonVanillaScale(Transform targetSkeletonParent, Transform speSkeletonTransform)
        {
            if (speSkeletonTransform == null) return;

            float scale = GetFirstChildScaleX(targetSkeletonParent, speSkeletonTransform);
            if (Mathf.Approximately(scale, 0f)) return;

            speSkeletonTransform.localScale = Vector3.one * scale;
        }

        private static void HideDirectSkeletonGraphics(Transform parent, Transform except)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == null || child.name == OverrideRootName) continue;
                if (except != null && child == except) continue;

                SkeletonGraphic skeleton = child.GetComponent<SkeletonGraphic>();
                if (skeleton != null && child.gameObject.activeSelf) child.gameObject.SetActive(false);
            }
        }

        private static Transform GetActiveSpeSkeletonTransform(Transform parent)
        {
            if (parent == null) return null;

            Transform speSkeleton = parent.Find("SpeSkeleton");
            if (speSkeleton == null || !speSkeleton.gameObject.activeSelf) return null;
            SkeletonGraphic skeleton = speSkeleton.GetComponent<SkeletonGraphic>();
            return skeleton == null ? null : speSkeleton;
        }

        private static void RestoreDirectSkeletonGraphics(Transform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == null || child.name == OverrideRootName) continue;

                SkeletonGraphic skeleton = child.GetComponent<SkeletonGraphic>();
                if (skeleton != null && !child.gameObject.activeSelf) child.gameObject.SetActive(true);
            }
        }

        private static void CopyRectTransform(RectTransform source, RectTransform target)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.anchoredPosition3D = source.anchoredPosition3D;
            target.sizeDelta = source.sizeDelta;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
        }

        private static void CopyParentRectTransformToChild(RectTransform target)
        {
            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.one;
            target.pivot = new Vector2(0.5f, 0.5f);
            target.anchoredPosition = Vector2.zero;
            target.sizeDelta = Vector2.zero;
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;
        }

        private static bool HasUiGraphic(GameObject prefab)
        {
            if (prefab == null) return false;
            return prefab.GetComponentInChildren<Graphic>(true) != null;
        }

        private static bool HasSceneRenderer(GameObject prefab)
        {
            if (prefab == null) return false;
            return prefab.GetComponentInChildren<Renderer>(true) != null;
        }

        private static void DisableRaycastTargets(GameObject root)
        {
            if (root == null) return;

            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null) continue;
                graphic.raycastTarget = false;
            }

            CanvasGroup[] canvasGroups = root.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < canvasGroups.Length; i++)
            {
                CanvasGroup canvasGroup = canvasGroups[i];
                if (canvasGroup == null) continue;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private static Vector2 GetReferenceSize(RectTransform referenceRect)
        {
            Rect rect = referenceRect.rect;
            float width = Mathf.Abs(rect.width);
            float height = Mathf.Abs(rect.height);
            if (width <= 0f) width = Mathf.Abs(referenceRect.sizeDelta.x);
            if (height <= 0f) height = Mathf.Abs(referenceRect.sizeDelta.y);
            if (width <= 0f) width = 512f;
            if (height <= 0f) height = 768f;
            return new Vector2(width, height);
        }

        private static Vector3 GetNextSceneBridgePosition()
        {
            int index = _sceneBridgeIndex;
            return new Vector3(10000f + index * 100f, 10000f, 0f);
        }

        private static GameObject GetGlobalSceneBridgeRoot()
        {
            GameObject root = GameObject.Find(GlobalSceneBridgeRootName);
            if (root != null) return root;

            root = new GameObject(GlobalSceneBridgeRootName);
            UnityEngine.Object.DontDestroyOnLoad(root);
            return root;
        }

        private static bool TryGetRendererBounds(GameObject instance, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.one);
            if (instance == null) return false;

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds) return false;
            if (bounds.size.x <= 0.001f && bounds.size.y <= 0.001f && bounds.size.z <= 0.001f)
            {
                bounds.size = Vector3.one;
            }

            return true;
        }

        private static void SetLayerRecursively(GameObject gameObject, int layer)
        {
            if (gameObject == null) return;

            gameObject.layer = layer;
            Transform transform = gameObject.transform;
            if (transform == null) return;

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null) continue;
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static void ConfigureScenePrefabRuntime(GameObject instance, SpeHeroSkeletonOverrideEntry entry)
        {
            if (instance == null) return;

            SpineLitePrefabPlayer[] spineLitePlayers = instance.GetComponentsInChildren<SpineLitePrefabPlayer>(true);
            if ((spineLitePlayers == null || spineLitePlayers.Length == 0 || !SpineLitePrefabRuntimeBinder.HasUsablePlayer(instance)) &&
                TryConfigureSpineLiteFallback(instance, entry))
            {
                spineLitePlayers = instance.GetComponentsInChildren<SpineLitePrefabPlayer>(true);
            }

            for (int i = 0; i < spineLitePlayers.Length; i++)
            {
                SpineLitePrefabPlayer player = spineLitePlayers[i];
                if (player == null) continue;

                player.enabled = true;
                player.RefreshNow();
            }

            Animator[] animators = instance.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null) continue;

                animator.enabled = true;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                animator.Update(0f);
            }
        }

        private static bool TryConfigureSpineLiteFallback(GameObject instance, SpeHeroSkeletonOverrideEntry entry)
        {
            string jsonText;
            string jsonPath;
            if (!TryLoadSpineLiteSidecarJson(entry, out jsonText, out jsonPath)) return false;

            SpineLitePrefabPlayer player;
            string report;
            bool bound = SpineLitePrefabRuntimeBinder.TryBind(instance, jsonText, out player, out report);
            if (bound)
            {
                LoggerManager.Debug("Bound SpineLite prefab fallback: " + jsonPath + " " + report);
                return true;
            }

            LoggerManager.Warning("Failed to bind SpineLite prefab fallback: " + jsonPath + " " + report);
            return false;
        }

        private static bool TryLoadSpineLiteSidecarJson(SpeHeroSkeletonOverrideEntry entry, out string jsonText, out string jsonPath)
        {
            jsonText = null;
            jsonPath = null;
            if (entry == null || string.IsNullOrEmpty(entry.VirtualResourcePath)) return false;

            jsonPath = GetSpineLiteSidecarPath(entry.VirtualResourcePath);
            if (string.IsNullOrEmpty(jsonPath)) return false;

            UnityEngine.Object asset;
            if (!ModResourceRegistry.TryLoad(jsonPath, typeof(UnityEngine.Object), out asset) || asset == null)
            {
                LoggerManager.Debug("SpineLite sidecar not found: " + jsonPath);
                return false;
            }

            TextAsset textAsset = asset as TextAsset;
            if (textAsset == null)
            {
                try
                {
                    textAsset = asset.TryCast<TextAsset>();
                }
                catch
                {
                    textAsset = null;
                }
            }

            if (textAsset == null)
            {
                LoggerManager.Warning("SpineLite sidecar is not TextAsset: " + jsonPath +
                                      " actual=" + asset.GetType().FullName +
                                      " name=" + Safe(asset.name));
                return false;
            }

            jsonText = textAsset.text;
            return !string.IsNullOrEmpty(jsonText);
        }

        private static string GetSpineLiteSidecarPath(string virtualPrefabPath)
        {
            string path = PathUtility.NormalizeResourcePath(virtualPrefabPath);
            if (string.IsNullOrEmpty(path)) return string.Empty;

            int dot = path.LastIndexOf('.');
            if (dot < 0) return path + "_spinelite.json";
            return path.Substring(0, dot) + "_spinelite.json";
        }

        private static string ProbeScenePrefabRuntime(GameObject instance)
        {
            if (instance == null) return string.Empty;

            try
            {
                SpineLitePrefabPlayer[] players = instance.GetComponentsInChildren<SpineLitePrefabPlayer>(true);
                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                int enabledRenderers = 0;
                int rendererWithMaterial = 0;
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null) continue;
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy) enabledRenderers++;
                    if (renderer.sharedMaterial != null) rendererWithMaterial++;
                }

                if (players == null || players.Length == 0)
                    return "prefabProbe=players:0,renderers:" + GetArrayLength(renderers) + ",enabledRenderers:" + enabledRenderers + ",materialRenderers:" + rendererWithMaterial;

                SpineLitePrefabPlayer player = players[0];
                SpineLiteBakedAnimationData data = player == null ? null : player.AnimationData;
                int bones = GetArrayLength(data != null ? data.Bones : null);
                int slots = GetArrayLength(data != null ? data.Slots : null);
                int attachments = GetArrayLength(data != null ? data.Attachments : null);
                int meshFilters = GetArrayLength(player != null ? player.MeshFilters : null);
                int meshRenderers = GetArrayLength(player != null ? player.MeshRenderers : null);
                string playerDebug = player == null ? string.Empty : player.GetDebugReport();
                return "prefabProbe=players:" + players.Length +
                       ",bones:" + bones +
                       ",slots:" + slots +
                       ",attachments:" + attachments +
                       ",meshFilters:" + meshFilters +
                       ",meshRenderers:" + meshRenderers +
                       ",renderers:" + GetArrayLength(renderers) +
                       ",enabledRenderers:" + enabledRenderers +
                       ",materialRenderers:" + rendererWithMaterial +
                       (string.IsNullOrEmpty(playerDebug) ? string.Empty : "," + playerDebug);
            }
            catch (Exception ex)
            {
                return "prefabProbeError=" + ex.GetType().Name + ":" + ex.Message;
            }
        }

        private static int GetArrayLength(Array array)
        {
            return array == null ? 0 : array.Length;
        }

        private static void ReleaseRenderTextures(Transform root)
        {
            RawImage[] images = root.GetComponentsInChildren<RawImage>(true);
            for (int i = 0; i < images.Length; i++)
            {
                RawImage image = images[i];
                if (image == null) continue;

                RenderTexture renderTexture = image.texture as RenderTexture;
                if (renderTexture == null || renderTexture.name == null || !renderTexture.name.StartsWith(SceneBridgeTexturePrefix, StringComparison.Ordinal)) continue;

                string renderTextureName = renderTexture.name;
                image.texture = null;
                renderTexture.Release();
                UnityEngine.Object.Destroy(renderTexture);
                UnregisterSceneBridgeHandle(renderTextureName);
            }
        }

        private static void DestroySceneBridgesForRenderTextures(Transform root)
        {
            RawImage[] images = root.GetComponentsInChildren<RawImage>(true);
            GameObject globalRoot = GameObject.Find(GlobalSceneBridgeRootName);
            if (globalRoot == null) return;

            for (int i = 0; i < images.Length; i++)
            {
                RawImage image = images[i];
                if (image == null) continue;

                RenderTexture renderTexture = image.texture as RenderTexture;
                if (renderTexture == null || renderTexture.name == null || !renderTexture.name.StartsWith(SceneBridgeTexturePrefix, StringComparison.Ordinal)) continue;

                Transform sceneRoot = globalRoot.transform.Find(renderTexture.name);
                if (sceneRoot != null)
                {
                    string renderTextureName = renderTexture.name;
                    ClearSceneBridgeCameraTargets(sceneRoot);
                    UnityEngine.Object.Destroy(sceneRoot.gameObject);
                    UnregisterSceneBridgeHandle(renderTextureName);
                }
            }
        }

        private static void RegisterSceneBridgeHandle(string renderTextureName, Transform uiRoot, Transform sceneRoot, RawImage image, RenderTexture renderTexture)
        {
            if (string.IsNullOrEmpty(renderTextureName)) return;

            SceneBridgeHandles[renderTextureName] = new SceneBridgeHandle
            {
                RenderTextureName = renderTextureName,
                UiRoot = uiRoot,
                SceneRoot = sceneRoot,
                Image = image,
                RenderTexture = renderTexture
            };
        }

        private static void UnregisterSceneBridgeHandle(string renderTextureName)
        {
            if (string.IsNullOrEmpty(renderTextureName)) return;
            SceneBridgeHandles.Remove(renderTextureName);
        }

        private static void PruneSceneBridgeHandles(System.Collections.Generic.HashSet<string> activeRenderTextureNames)
        {
            if (activeRenderTextureNames == null || SceneBridgeHandles.Count <= 0) return;

            System.Collections.Generic.List<string> staleKeys = null;
            foreach (System.Collections.Generic.KeyValuePair<string, SceneBridgeHandle> pair in SceneBridgeHandles)
            {
                SceneBridgeHandle handle = pair.Value;
                if (IsSceneBridgeHandleActive(handle))
                {
                    activeRenderTextureNames.Add(pair.Key);
                    continue;
                }

                if (staleKeys == null) staleKeys = new System.Collections.Generic.List<string>();
                staleKeys.Add(pair.Key);
            }

            if (staleKeys == null) return;
            for (int i = 0; i < staleKeys.Count; i++)
            {
                SceneBridgeHandles.Remove(staleKeys[i]);
            }
        }

        private static bool IsSceneBridgeHandleActive(SceneBridgeHandle handle)
        {
            if (handle == null || string.IsNullOrEmpty(handle.RenderTextureName)) return false;
            if (handle.UiRoot == null || handle.SceneRoot == null || handle.Image == null || handle.RenderTexture == null) return false;
            if (handle.Image.texture != handle.RenderTexture) return false;
            if (!string.Equals(handle.RenderTexture.name, handle.RenderTextureName, StringComparison.Ordinal)) return false;
            return true;
        }

        private static RawImage[] FindRawImagesIncludingInactive()
        {
            try
            {
                return UnityEngine.Object.FindObjectsOfType<RawImage>(true);
            }
            catch (MissingMethodException)
            {
                return UnityEngine.Object.FindObjectsOfType<RawImage>();
            }
        }

        private static void ClearSceneBridgeCameraTargets(Transform sceneRoot)
        {
            if (sceneRoot == null) return;

            Camera[] cameras = sceneRoot.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null || camera.targetTexture == null) continue;

                camera.targetTexture = null;
            }
        }

        private static void ReleaseSceneBridgeCameraRenderTextures(Transform sceneRoot)
        {
            if (sceneRoot == null) return;

            Camera[] cameras = sceneRoot.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null) continue;

                RenderTexture renderTexture = camera.targetTexture;
                camera.targetTexture = null;
                if (renderTexture == null || renderTexture.name == null || !renderTexture.name.StartsWith(SceneBridgeTexturePrefix, StringComparison.Ordinal)) continue;

                renderTexture.Release();
                UnityEngine.Object.Destroy(renderTexture);
            }
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null) return string.Empty;

            System.Text.StringBuilder builder = new System.Text.StringBuilder(transform.name);
            Transform current = transform.parent;
            while (current != null)
            {
                builder.Insert(0, current.name + "/");
                current = current.parent;
            }

            return builder.ToString();
        }

        private static FaceContainerKind GetFaceContainerKind(Transform transform)
        {
            string path = GetTransformPath(transform);
            if (string.Equals(path, HudFaceContainerPath, StringComparison.Ordinal)) return FaceContainerKind.Hud;
            if (string.Equals(path, DetailFaceContainerPath, StringComparison.Ordinal)) return FaceContainerKind.Detail;
            return FaceContainerKind.Other;
        }

        private static void LogHudLikeContainerIfNeeded(Transform transform)
        {
            try
            {
                if (transform == null) return;
                if (!string.Equals(transform.name, "Face", StringComparison.Ordinal)) return;
                Transform parent = transform.parent;
                if (parent == null || !string.Equals(parent.name, "FaceMask", StringComparison.Ordinal)) return;

                ShowHeroDetail showHeroDetail = transform.GetComponent<ShowHeroDetail>();
                if (showHeroDetail == null || showHeroDetail.heroData == null) return;

                LoggerManager.Debug("SpeHeroSkeleton HUD compatibility layer path mismatch. path=" +
                                    GetTransformPath(transform) +
                                    ", expected=" + HudFaceContainerPath +
                                    ", heroID=" + showHeroDetail.heroData.heroID +
                                    ", heroName=" + Safe(showHeroDetail.heroData.heroName));
            }
            catch
            {
            }
        }

        private static string Safe(string value)
        {
            return value ?? string.Empty;
        }

        private static string GetExceptionSummary(Exception exception)
        {
            if (exception == null) return string.Empty;

            string message = exception.Message ?? string.Empty;
            int lineBreak = message.IndexOfAny(new[] { '\r', '\n' });
            if (lineBreak >= 0) message = message.Substring(0, lineBreak);
            return exception.GetType().Name + (string.IsNullOrEmpty(message) ? string.Empty : ": " + message);
        }
    }
}
