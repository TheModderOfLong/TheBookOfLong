using System;
namespace TheResourceOfLong
{
    public static class ResourceHotReloadManager
    {
        public static void ReloadAll()
        {
            try
            {
                LoggerManager.Info("Resource hot reload started: MappingRules.");

                MappingRuleRegistry.Reload();
                IconSpriteOverrideRegistry.Reload();
                SpeHeroSkeletonOverrideRegistry.Reload();
                ModResourceRegistry.ClearRuntimeCache();

                bool refreshed = SpeHeroSkeletonRuntimeApi.RefreshVisiblePanels(true);

                LoggerManager.Info("Resource hot reload completed: mapping rules=" + MappingRuleRegistry.EntryCount +
                                   ", atlas sprite overrides=" + IconSpriteOverrideRegistry.EntryCount +
                                   ", SpeHeroSkeleton overrides=" + SpeHeroSkeletonOverrideRegistry.EntryCount +
                                   ", refreshed visible panels=" + refreshed + ".");
            }
            catch (Exception ex)
            {
                LoggerManager.Error("Resource hot reload failed: " + ex);
            }
        }
    }
}
