using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace TheExtensionOfLong.Patches.WorldEventController
{
    /// <summary>
    /// 补丁3：在 GameController.Start 后独立应用 WorldEvent 补丁
    /// 
    /// 混合路径核心：不修改龙之书的 ApplyLoadedPatchFiles，
    /// 而是在 GameController.Start 的独立 Postfix 中自行应用 WorldEvent 补丁。
    /// 
    /// 补丁文件已在 LoadPatchFilesPostfixPatch 中从龙之书的 LoadedPatchFiles 提取并移除，
    /// 因此龙之书的 ApplyLoadedPatchFiles 不会错误地处理 WorldEvent 补丁。
    /// 
    /// Priority 设为低于龙之书的 Postfix，确保龙之书管线先启动。
    /// 
    /// 使用协程等待龙之书的 dump 完成后，先导出原始数据再应用补丁，
    /// 确保导出的是游戏本体原生数据。
    /// </summary>
    [HarmonyPatch(typeof(GameController), "Start")]
    public static class GameControllerStartWorldEventPatchPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        public static void Postfix()
        {
            // 确保管理器已初始化
            WorldEventPatchManager.Initialize();

            // 启动协程：等待龙之书 dump 完成后，先导出原始数据再应用补丁
            MelonCoroutines.Start(WorldEventPatchManager.ExportThenPatchCoroutine());
        }
    }
}
