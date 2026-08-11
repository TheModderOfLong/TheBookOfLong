using System;
using System.IO;
using Il2Cpp;
using Il2CppSpine.Unity;
using UnityEngine;

namespace TheResourceOfLong
{
    public static class SpeHeroSkeletonRuntimeApi
    {
        public static string ValidateRuntimeSpeHeroSkeletonSource(int sourceType, string sourceParam)
        {
            try
            {
                sourceParam = (sourceParam ?? string.Empty).Trim();
                if (sourceType == 0) return string.Empty;

                if (sourceType == 1)
                {
                    int sourceHeroId;
                    if (!int.TryParse(sourceParam, out sourceHeroId))
                    {
                        return "运行时特殊立绘 SpeHero 参数无效: " + sourceParam;
                    }

                    string path = "Skeleton/SpeHero/" + sourceHeroId + "/skeleton_SkeletonData";
                    UnityEngine.Object asset = UnityEngine.Resources.Load(path);
                    if (asset == null)
                    {
                        return "运行时特殊立绘资源不存在: Resources/" + path;
                    }

                    return IsSkeletonDataAsset(asset)
                        ? string.Empty
                        : "运行时特殊立绘资源类型不是 SkeletonDataAsset: Resources/" + path + ", actual=" + asset.GetType().FullName;
                }

                if (sourceType == 2)
                {
                    MappingRuleEntry rule;
                    if (!MappingRuleRegistry.TryGetById(sourceParam, out rule) || rule == null)
                    {
                        return "运行时特殊立绘 Mapping 编号不存在: " + sourceParam;
                    }

                    if (rule.OverrideType != MappingOverrideType.SpeHeroSkeleton)
                    {
                        return "运行时特殊立绘 Mapping 编号类型不匹配: " + sourceParam + ", type=" + rule.OverrideType;
                    }

                    return ValidateMappingRuleResource(sourceParam, rule);
                }

                return "运行时特殊立绘类型不支持: " + sourceType;
            }
            catch (Exception ex)
            {
                return "运行时特殊立绘参数校验失败: " + ex.Message;
            }
        }

        public static bool RefreshRuntimeSpeHeroSkeleton(int heroId)
        {
            return RefreshRuntimeSpeHeroSkeleton(heroId, false);
        }

        public static bool RefreshRuntimeSpeHeroSkeleton(int heroId, bool refreshPlotPanel = false)
        {
            return RefreshVisiblePanels(heroId, refreshPlotPanel);
        }

        public static bool RefreshRuntimeSpeHeroSkeleton(bool refreshPlotPanel = false)
        {
            return RefreshVisiblePanels(refreshPlotPanel);
        }

        public static bool RefreshVisiblePanels(int heroId)
        {
            return RefreshVisiblePanels(heroId, false);
        }

        public static bool RefreshVisiblePanels()
        {
            return RefreshVisiblePanels(false);
        }

        public static bool RefreshVisiblePanels(bool refreshPlotPanel = false)
        {
            bool refreshed = false;

            try
            {
                refreshed |= RefreshHeroDetail();
                refreshed |= RefreshAreaHeroList();
                refreshed |= RefreshHudPlayerFace();
                if (refreshPlotPanel) refreshed |= RefreshPlotPanel();
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("Failed to refresh all runtime SpeHeroSkeleton panels. error=" + ex.Message);
            }

            LoggerManager.Debug("RefreshRuntimeSpeHeroSkeleton completed. heroID=all" +
                                ", refreshPlotPanel=" + refreshPlotPanel +
                                ", refreshed=" + refreshed);
            return refreshed;
        }

        public static bool RefreshVisiblePanels(int heroId, bool refreshPlotPanel = false)
        {
            bool refreshed = false;

            try
            {
                refreshed |= RefreshHeroDetail(heroId);
                refreshed |= RefreshAreaHeroList(heroId);
                if (heroId == 0) refreshed |= RefreshHudPlayerFace();
                if (refreshPlotPanel) refreshed |= RefreshPlotPanel(heroId);
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("Failed to refresh runtime SpeHeroSkeleton panels. heroID=" + heroId + ", error=" + ex.Message);
            }

            LoggerManager.Debug("RefreshRuntimeSpeHeroSkeleton completed. heroID=" + heroId +
                                ", refreshPlotPanel=" + refreshPlotPanel +
                                ", refreshed=" + refreshed);
            return refreshed;
        }

        private static bool RefreshHeroDetail()
        {
            HeroDetailController[] controllers = FindObjects<HeroDetailController>();
            bool refreshed = false;
            for (int i = 0; i < controllers.Length; i++)
            {
                HeroDetailController controller = controllers[i];
                if (controller == null || controller.nowShowHero == null) continue;
                if (!IsActiveInHierarchy(controller.gameObject)) continue;

                controller.RefreshHeroSkeleton();
                refreshed = true;
            }

            return refreshed;
        }

        private static bool RefreshHeroDetail(int heroId)
        {
            HeroDetailController[] controllers = FindObjects<HeroDetailController>();
            bool refreshed = false;
            for (int i = 0; i < controllers.Length; i++)
            {
                HeroDetailController controller = controllers[i];
                if (controller == null || controller.nowShowHero == null) continue;
                if (controller.nowShowHero.heroID != heroId) continue;
                if (!IsActiveInHierarchy(controller.gameObject)) continue;

                controller.RefreshHeroSkeleton();
                refreshed = true;
            }

            return refreshed;
        }

        private static bool RefreshAreaHeroList()
        {
            AreaController[] controllers = FindObjects<AreaController>();
            bool refreshed = false;
            for (int i = 0; i < controllers.Length; i++)
            {
                AreaController controller = controllers[i];
                if (controller == null || !IsActiveInHierarchy(controller.gameObject)) continue;

                refreshed |= RefreshAreaHeroIconBack(controller);
            }

            return refreshed;
        }

        private static bool RefreshAreaHeroList(int heroId)
        {
            AreaController[] controllers = FindObjects<AreaController>();
            bool refreshed = false;
            for (int i = 0; i < controllers.Length; i++)
            {
                AreaController controller = controllers[i];
                if (controller == null || !IsActiveInHierarchy(controller.gameObject)) continue;

                refreshed |= RefreshAreaHeroIconBack(controller, heroId);
            }

            return refreshed;
        }

        private static bool RefreshAreaHeroIconBack(AreaController controller)
        {
            if (controller == null || controller.heroIconGrid == null) return false;

            bool refreshed = false;
            Transform gridTransform = controller.heroIconGrid.transform;
            int childCount = gridTransform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = gridTransform.GetChild(i);
                if (child == null || !IsActiveInHierarchy(child.gameObject)) continue;

                HeroIconController iconController = child.GetComponent<HeroIconController>();
                if (iconController == null || iconController.heroData == null) continue;

                Transform back = child.Find("Back");
                if (back == null) continue;

                iconController.heroData.SetSkeletonGraphic(back, -99, -1);
                refreshed = true;
            }

            return refreshed;
        }

        private static bool RefreshAreaHeroIconBack(AreaController controller, int heroId)
        {
            if (controller == null || controller.heroIconGrid == null) return false;

            bool refreshed = false;
            Transform gridTransform = controller.heroIconGrid.transform;
            int childCount = gridTransform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = gridTransform.GetChild(i);
                if (child == null || !IsActiveInHierarchy(child.gameObject)) continue;

                HeroIconController iconController = child.GetComponent<HeroIconController>();
                if (iconController == null || iconController.heroData == null) continue;
                if (iconController.heroData.heroID != heroId) continue;

                Transform back = child.Find("Back");
                if (back == null) continue;

                iconController.heroData.SetSkeletonGraphic(back, -99, -1);
                refreshed = true;
            }

            return refreshed;
        }

        private static bool RefreshHudPlayerFace()
        {
            HudController[] controllers = FindObjects<HudController>();
            bool refreshed = false;
            for (int i = 0; i < controllers.Length; i++)
            {
                HudController controller = controllers[i];
                if (controller == null || !IsActiveInHierarchy(controller.gameObject)) continue;

                controller.RefreshHeroSkeleton();
                controller.needRefreshPlayerSkeleton = false;
                refreshed = true;
            }

            return refreshed;
        }

        private static bool RefreshPlotPanel()
        {
            PlotController controller = PlotController._instance;
            if (controller == null || controller.plotPanel == null || !IsActiveInHierarchy(controller.plotPanel)) return false;

            bool refreshed = false;
            refreshed |= RefreshPlotFace(controller, "LeftFace", 0);
            refreshed |= RefreshPlotFace(controller, "RightFace", 0);
            return refreshed;
        }

        private static bool RefreshPlotPanel(int heroId)
        {
            PlotController controller = PlotController._instance;
            if (controller == null || controller.plotPanel == null || !IsActiveInHierarchy(controller.plotPanel)) return false;

            bool refreshed = false;
            refreshed |= RefreshPlotFace(controller, "LeftFace", heroId);
            refreshed |= RefreshPlotFace(controller, "RightFace", heroId);
            return refreshed;
        }

        private static bool RefreshPlotFace(PlotController controller, string faceName, int heroId)
        {
            try
            {
                if (controller == null || controller.plotPanel == null) return false;

                Transform face = controller.plotPanel.transform.Find(faceName);
                if (face == null || !IsActiveInHierarchy(face.gameObject)) return false;

                Transform back = face.Find("Back");
                if (back == null) return false;

                ShowHeroDetail showHeroDetail = back.GetComponent<ShowHeroDetail>();
                HeroData hero = showHeroDetail == null ? null : showHeroDetail.heroData;
                if (hero == null) return false;
                if (heroId > 0 && hero.heroID != heroId) return false;

                hero.SetSkeletonGraphic(face, -99, -1);
                LoggerManager.Debug("RefreshRuntimeSpeHeroSkeleton refreshed PlotPanel face=" + faceName +
                                    ", heroID=" + hero.heroID +
                                    ", heroName=" + (hero.heroName ?? string.Empty));
                return true;
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("Failed to refresh PlotPanel face=" + faceName + ": " + ex.Message);
                return false;
            }
        }

        private static bool IsSkeletonDataAsset(UnityEngine.Object asset)
        {
            if (asset == null) return false;
            if (asset is SkeletonDataAsset) return true;

            try
            {
                return asset.TryCast<SkeletonDataAsset>() != null;
            }
            catch
            {
                return false;
            }
        }

        private static string ValidateMappingRuleResource(string id, MappingRuleEntry rule)
        {
            if (rule == null) return "运行时特殊立绘 Mapping 编号不存在: " + id;

            string resourcePath = rule.ResourcePath ?? string.Empty;
            if (SpeHeroSkeletonOverrideRegistry.IsSupportedImage(resourcePath))
            {
                if (string.IsNullOrEmpty(rule.FullSourcePath) || !File.Exists(rule.FullSourcePath))
                {
                    return "运行时特殊立绘 Mapping 图片不存在: " + id +
                           ", resource=" + resourcePath +
                           ", expected=" + (rule.FullSourcePath ?? string.Empty) +
                           ", hint=MappingRules 资源路径应相对 Res/Mapping";
                }

                return string.Empty;
            }

            if (SpeHeroSkeletonOverrideRegistry.IsPrefab(resourcePath))
            {
                ModResourceEntry resourceEntry;
                if (!ModResourceRegistry.TryGetEntry(rule.VirtualResourcePath, out resourceEntry) || resourceEntry == null)
                {
                    return "运行时特殊立绘 Mapping Prefab 资源未注册: " + id +
                           ", virtualPath=" + (rule.VirtualResourcePath ?? string.Empty);
                }

                return string.Empty;
            }

            ModResourceEntry entry;
            if (!ModResourceRegistry.TryGetEntry(rule.VirtualResourcePath, out entry) || entry == null)
            {
                return "运行时特殊立绘 Mapping Spine 资源未注册: " + id +
                       ", virtualPath=" + (rule.VirtualResourcePath ?? string.Empty);
            }

            SpeHeroSkeletonOverrideEntry skeletonEntry = SpeHeroSkeletonOverrideRegistry.BuildEntry(rule);
            SkeletonDataAsset skeletonDataAsset;
            if (!SpeHeroSkeletonOverrideRegistry.TryLoadSkeletonDataAsset(skeletonEntry, out skeletonDataAsset) || skeletonDataAsset == null)
            {
                return "运行时特殊立绘 Mapping Spine 资源不可加载或类型不是 SkeletonDataAsset: " + id +
                       ", virtualPath=" + (rule.VirtualResourcePath ?? string.Empty);
            }

            return string.Empty;
        }

        private static T[] FindObjects<T>() where T : UnityEngine.Object
        {
            try
            {
                return UnityEngine.Object.FindObjectsOfType<T>(true);
            }
            catch
            {
                return UnityEngine.Object.FindObjectsOfType<T>();
            }
        }

        private static bool IsActiveInHierarchy(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
        }
    }
}

