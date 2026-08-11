using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 防护世界剧情 InteractHero 目标角色不存在的情况，避免旧存档缺少新增角色实例时推进天数报错。
    /// </summary>
    [HarmonyPatch(typeof(WorldPlotEventController), "StartNewWorldPlotEventFromDataBase", new Type[] { typeof(int) })]
    public static class WorldPlotEventStartGuardPatch
    {
        private static readonly HashSet<string> WarnedMissingInteractHeroes = new HashSet<string>();

        /// <summary>
        /// 在原版创建世界剧情前检查 InteractHero 目标角色，缺失时跳过本次创建并保留后续每日重试机会。
        /// </summary>
        [HarmonyPrefix]
        public static bool Prefix(WorldPlotEventController __instance, int i)
        {
            if (!ShouldSkipMissingInteractHero(__instance, i, out int plotID, out string eventName, out string heroName))
            {
                return true;
            }

            LogMissingInteractHeroOncePerRun(plotID, eventName, heroName);
            return false;
        }

        /// <summary>
        /// 判断指定世界剧情是否因 InteractHero 目标角色缺失而需要跳过。
        /// </summary>
        private static bool ShouldSkipMissingInteractHero(
            WorldPlotEventController controller,
            int index,
            out int plotID,
            out string eventName,
            out string heroName)
        {
            plotID = 0;
            eventName = null;
            heroName = null;

            try
            {
                var list = controller?.WorldPlotEventDataBase;
                if (list == null || index < 0 || index >= list.Count) return false;

                WorldPlotEventData data = list[index];
                if (data == null) return false;

                plotID = data.plotID;
                eventName = data.name;

                if (data.triggerType != PlotTriggerType.InteractHero) return false;
                if (string.IsNullOrEmpty(data.triggerTargetID)) return false;

                heroName = FirstPart(data.triggerTargetID);
                WorldData worldData = GameController._instance?.worldData;
                if (worldData == null) return false;

                HeroData hero = worldData.GetHero(heroName);
                return hero == null;
            }
            catch (Exception ex)
            {
                LoggerManager.Warning($"WorldPlotEvent: 检查 InteractHero 目标角色时失败，交给原版逻辑处理: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 对同一缺失角色世界剧情首次输出 Warning，后续降级为 Debug，避免每日推进刷屏。
        /// </summary>
        private static void LogMissingInteractHeroOncePerRun(int plotID, string eventName, string heroName)
        {
            string key = $"{plotID}|{eventName}|{heroName}";
            string message = $"WorldPlotEvent: 世界剧情 [{Safe(eventName)}] 的 InteractHero 目标角色 [{Safe(heroName)}] 不存在，已跳过";

            if (WarnedMissingInteractHeroes.Add(key))
            {
                LoggerManager.Warning(message);
            }
            else
            {
                LoggerManager.Debug(message);
            }
        }

        /// <summary>
        /// 提取冒号分隔字符串的首段，用于兼容 triggerTargetID 中附带额外参数的情况。
        /// </summary>
        private static string FirstPart(string value)
        {
            int index = value.IndexOf(':');
            return index >= 0 ? value.Substring(0, index) : value;
        }

        /// <summary>
        /// 将空文本转换为日志占位符。
        /// </summary>
        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<empty>" : value;
        }
    }
}
