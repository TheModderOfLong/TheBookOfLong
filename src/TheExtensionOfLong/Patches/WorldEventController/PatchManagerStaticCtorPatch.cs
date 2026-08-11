using HarmonyLib;

namespace TheExtensionOfLong.Patches.WorldEventController
{
    /// <summary>
    /// 补丁1：在 GameComplexDataPatchManager.LoadPatchFiles 执行前注册 WorldEventController 的 TargetDefinition
    /// 
    /// 作用：让龙之书的文件加载逻辑识别 WorldEventController_worldEventDataBase.json，
    /// 并将其加载到 LoadedPatchFiles 中（ControllerKind = 2）。
    /// 
    /// 重要：必须使用 Prefix 而非 Postfix，因为 LoadPatchFiles() 在静态构造函数 cctor 内部被调用。
    /// 如果在 cctor 的 Postfix 中注册，LoadPatchFiles 已经执行完毕，WorldEvent 文件不会被加载。
    /// 改为在 LoadPatchFiles 的 Prefix 中注册，确保文件加载之前 TargetDefinition 已就绪。
    /// 
    /// 使用字符串指定类型名，因为 GameComplexDataPatchManager 是 internal 类
    /// </summary>
    [HarmonyPatch("TheBookOfLong.GameComplexDataPatchManager", "LoadPatchFiles")]
    public static class PatchManagerStaticCtorPatch
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            WorldEventPatchManager.RegisterTargetDefinition();
        }
    }
}
