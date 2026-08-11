using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Il2CppInterop.Runtime.Injection;
using MelonLoader;
using TheResourceOfLong;
using UnityEngine;

[assembly: MelonInfo(typeof(ModMain), ModConfig.ModName, ModConfig.ModVersion, ModConfig.ModAuthor)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.IL2CPP)]
namespace TheResourceOfLong
{
    public class ModMain : MelonMod
    {
        // 重写OnInitialize方法，在Mod加载时调用
        public override void OnInitializeMelon()
        {
            // 默认的日志级别设为 Info
            LoggerManager.CurrentLogLevel = LogLevel.Info;

            try
            {
                // 不再应用sprineLite进行时
                // ClassInjector.RegisterTypeInIl2Cpp<SpineLitePrefabPlayer>();
                ClassInjector.RegisterTypeInIl2Cpp<SceneBridgeRenderTextureDriver>();

                string gameRoot = ModDiscovery.ResolveGameRoot();
                string modsOfLongRoot = ModDiscovery.ResolveModsOfLongRoot(gameRoot);
                ResourceManifestGenerator.Initialize(gameRoot, modsOfLongRoot);
                MappingRulesGenerator.Initialize(gameRoot, modsOfLongRoot);
                ModResourceRegistry.Initialize();
                MappingRuleRegistry.Initialize();
                IconSpriteOverrideRegistry.Initialize();
                SpeHeroSkeletonOverrideRegistry.Initialize();
                ContainerProbe.Initialize(ModResourceRegistry.GameRoot);
                ScenePrefabUiProbe.Initialize(ModResourceRegistry.GameRoot);
                ResourceProbe.Initialize(ModResourceRegistry.GameRoot);
                LoggerManager.Info("TheResourceOfLong initialized. Registered resources: " + ModResourceRegistry.EntryCount + ", mapping rules: " + MappingRuleRegistry.EntryCount + ", atlas sprite overrides: " + IconSpriteOverrideRegistry.EntryCount + ", SpeHeroSkeleton overrides: " + SpeHeroSkeletonOverrideRegistry.EntryCount);
            }
            catch (Exception ex)
            {
                LoggerManager.Error("TheResourceOfLong initialization failed: " + ex);
            }
        }


        // 场景加载完成
        public override void OnSceneWasInitialized(int level, string name)
        {
            try
            {
                SpeHeroSkeletonOverrideRenderer.CleanupOrphanScenePrefabBridges();
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("Failed to cleanup orphan scene prefab bridges after scene initialized: " + ex.Message);
            }
        }

        // 每帧更新
        public override void OnUpdate()
        {
            if ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetKeyDown(KeyCode.Alpha2))
            {
                ResourceHotReloadManager.ReloadAll();
            }

            ScenePrefabUiProbe.Update();
        }
    }
}
