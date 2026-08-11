using Il2Cpp;
using MelonLoader;
using TheExtensionOfLong;
using UnityEngine;

[assembly: MelonInfo(typeof(XGMod), ModConfig.ModName, ModConfig.ModVersion, ModConfig.ModAuthor)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.IL2CPP)]
namespace TheExtensionOfLong
{
    public class XGMod : MelonMod
    {
        private static bool _resourceLoggerLookupFailedLogged;

        // 重写OnInitialize方法，在Mod加载时调用
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg($"日志级别: {LoggerManager.CurrentLogLevel} (Alt+1切换)");

            ModLoadConfigWindow.InitializePreferences();

            // 初始化选项补丁管理器
            PlotChoicePatchManager.Initialize();
            HeroAddressFormRegistry.Initialize();
            TriggerRegistry.Initialize();

            // 手动 Patch 龙之书的 CsvPatchApplier.CreateBlankRow
            PatchTheBookOfLongCreateBlankRow();

            // 默认的日志级别设为 Info
            LoggerManager.CurrentLogLevel = LogLevel.Info;
        }

        /// <summary>
        /// 使用反射手动 Patch 龙之书的 CsvPatchApplier.CreateBlankRow 方法，
        /// 修复空白占位行导致 GameDataController.LoadAllGameData 崩溃的问题。
        /// </summary>
        private void PatchTheBookOfLongCreateBlankRow()
        {
            try
            {
                var harmony = new HarmonyLib.Harmony("TheExtensionOfLong.TheBookOfLongPatches");
                var type = HarmonyLib.AccessTools.TypeByName("TheBookOfLong.CsvPatchApplier");
                if (type == null)
                {
                    LoggerInstance.Warning("[PatchTheBookOfLongCreateBlankRow] 未找到 TheBookOfLong.CsvPatchApplier，跳过 Patch（龙之书可能未加载）");
                    return;
                }

                var method = HarmonyLib.AccessTools.Method(type, "CreateBlankRow");
                if (method == null)
                {
                    LoggerInstance.Warning("[PatchTheBookOfLongCreateBlankRow] 未找到 CreateBlankRow 方法，跳过 Patch");
                    return;
                }

                var postfix = new HarmonyLib.HarmonyMethod(
                    typeof(CsvPatchApplierCreateBlankRowPatch),
                    nameof(CsvPatchApplierCreateBlankRowPatch.CreateBlankRowPostfix)
                );

                harmony.Patch(method, postfix: postfix);
                LoggerInstance.Msg("[PatchTheBookOfLongCreateBlankRow] CsvPatchApplier.CreateBlankRow 已 Patch");
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Error($"[PatchTheBookOfLongCreateBlankRow] Patch 失败: {ex}");
            }
        }

        // 场景加载完成
        public override void OnSceneWasInitialized(int level, string name)
        {
            // LoggerManager.Info($"场景加载: {name}");
        }

        // 每帧更新
        public override void OnUpdate()
        {
            TextInputDialog.OnUpdate();
            ModLoadConfigWindow.OnUpdate();

            // if (Input.GetKeyDown(KeyCode.F1))
            // {
            //     LoggerManager.Info("F1 被按下！");
            // }

            // Alt + 1: 循环切换 TheExtensionOfLong，并同步切换 TheResourceOfLong 日志级别。
            if ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetKeyDown(KeyCode.Alpha1))
            {
                LoggerManager.CycleLogLevel();
                string resourceLogLevel = TryCycleResourceLogLevel();
                string message = $"TheExtensionOfLong 日志级别切换为: {LoggerManager.CurrentLogLevel}";
                if (!string.IsNullOrEmpty(resourceLogLevel))
                {
                    message += $"；TheResourceOfLong 日志级别切换为: {resourceLogLevel}";
                }

                LoggerInstance.Msg(message);
            }

            // Alt + F3: 重新加载选项补丁
            if ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetKeyDown(KeyCode.Alpha2))
            {
                PlotChoicePatchManager.Reload();
                HeroAddressFormRegistry.Reload();
                TriggerRegistry.Reload();
                LoggerInstance.Msg("选项补丁、默认称呼规则与触发器规则已重新加载");
            }

            // Alt + `(~): 打开剧情指令调试台
            if ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetKeyDown(KeyCode.BackQuote))
            {
                PlotCommandDebugConsole.Show();
            }
        }

        // IMGUI 渲染（TextInputDialog IMGUI 回退模式需要）
        public override void OnGUI()
        {
            TextInputDialog.OnGUI();
            ModLoadConfigWindow.OnGUI();
        }

        private static string TryCycleResourceLogLevel()
        {
            try
            {
                System.Type loggerType = FindType("TheResourceOfLong.LoggerManager");
                if (loggerType == null)
                {
                    LogResourceLoggerLookupFailureOnce("未找到 TheResourceOfLong.LoggerManager，可能未加载 TheResourceOfLong.dll");
                    return null;
                }

                System.Reflection.MethodInfo cycleMethod = loggerType.GetMethod(
                    "CycleLogLevel",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (cycleMethod == null)
                {
                    LogResourceLoggerLookupFailureOnce("未找到 TheResourceOfLong.LoggerManager.CycleLogLevel()");
                    return null;
                }

                cycleMethod.Invoke(null, null);

                System.Reflection.FieldInfo levelField = loggerType.GetField(
                    "CurrentLogLevel",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                object level = levelField == null ? null : levelField.GetValue(null);
                return level == null ? "已切换" : level.ToString();
            }
            catch (System.Exception ex)
            {
                LogResourceLoggerLookupFailureOnce("同步切换 TheResourceOfLong 日志级别失败: " + ex.Message);
                return null;
            }
        }

        private static System.Type FindType(string fullName)
        {
            System.Type direct = System.Type.GetType(fullName + ", TheResourceOfLong", false);
            if (direct != null) return direct;

            System.Reflection.Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                System.Type type = assemblies[i].GetType(fullName, false);
                if (type != null) return type;
            }

            return null;
        }

        private static void LogResourceLoggerLookupFailureOnce(string message)
        {
            if (_resourceLoggerLookupFailedLogged) return;
            _resourceLoggerLookupFailedLogged = true;
            Melon<XGMod>.Logger.Warning(message);
        }
    }
}
