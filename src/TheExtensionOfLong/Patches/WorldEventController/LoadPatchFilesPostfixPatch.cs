using HarmonyLib;

namespace TheExtensionOfLong.Patches.WorldEventController
{
    /// <summary>
    /// 补丁：在龙之书的 LoadPatchFiles 执行后，提取 WorldEvent 补丁并从原列表中移除
    /// 
    /// 为什么在这里做：LoadPatchFiles 在 GameComplexDataPatchManager.Initialize() 中调用，
    /// 而 ApplyLoadedPatchFiles 在后续的协程中调用。在 LoadPatchFiles 的 Postfix 中提取，
    /// 可以确保 WorldEvent 补丁在 ApplyLoadedPatchFiles 执行前就已从龙之书的列表中移除，
    /// 避免被错误路由到 MissionDataController。
    /// 
    /// 使用字符串指定类型名，因为 GameComplexDataPatchManager 是 internal 类
    /// </summary>
    [HarmonyPatch("TheBookOfLong.GameComplexDataPatchManager", "LoadPatchFiles")]
    public static class LoadPatchFilesPostfixPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            WorldEventPatchManager.ExtractAndRemoveWorldEventPatches();
        }
    }
}
