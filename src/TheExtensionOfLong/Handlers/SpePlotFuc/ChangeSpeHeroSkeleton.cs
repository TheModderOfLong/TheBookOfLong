using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppSystem.Collections.Generic;
using System.Reflection;
using System;

namespace TheExtensionOfLong
{
    [SpePlotFuc("ChangeSpeHeroSkeleton")]
    public static class SpePlotFucChangeSpeHeroSkeleton
    {
        /// <summary>
        /// 运行时更换角色特殊立绘。
        /// 格式: ChangeSpeHeroSkeleton*角色ID/角色名称#类型#参数#是否强制开启(可选)#是否刷新对话立绘(可选)
        /// 类型: 0/Revert, 1/SpeHero, 2/Mapping
        /// 是否强制开启: 省略/0/false=false, 1/true=true(忽略大小写)
        /// 是否刷新对话立绘: 省略/0/false=false, 1/true=true(忽略大小写)
        /// </summary>
        public static void TryCall(PlotController __instance, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 2)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*角色ID/角色名称#类型#参数#是否强制开启(可选)#是否刷新对话立绘(可选)]");
                return;
            }

            HeroData hero = CommonHandlers.ResolveHeroId(__instance, fucParams[0], __instance?.targetInteractHero);
            if (hero == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到目标角色: {fucParams[0]}");
                return;
            }

            if (!TryParseSpeHeroSkeletonSourceType(fucParams[1], out int sourceType))
            {
                LoggerManager.Warning($"{fucName}: 类型参数无效: {fucParams[1]}，支持0/Revert, 1/SpeHero, 2/Mapping");
                return;
            }

            string sourceParam = fucParams.Length > 2 ? (fucParams[2] ?? string.Empty).Trim() : string.Empty;
            if (sourceType > 0 && string.IsNullOrWhiteSpace(sourceParam))
            {
                LoggerManager.Warning($"{fucName}: 类型为{sourceType}时参数不能为空");
                return;
            }

            bool forceEnable;
            if (!TryParseChangeSpeHeroSkeletonForceEnable(fucParams.Length > 3 ? fucParams[3] : null, out forceEnable))
            {
                LoggerManager.Warning($"{fucName}: 是否强制开启参数无效: {fucParams[3]}，仅支持省略、0、1、true、false");
                return;
            }

            bool refreshPlotPanel;
            if (!TryParseChangeSpeHeroSkeletonOptionalBool(fucParams.Length > 4 ? fucParams[4] : null, out refreshPlotPanel))
            {
                LoggerManager.Warning($"{fucName}: 是否刷新对话立绘参数无效: {fucParams[4]}，仅支持省略、0、1、true、false");
                return;
            }

            PlotEventLogData plotEventLogData = CommonHandlers.GetPlotEventLogData();
            if (plotEventLogData == null)
            {
                LoggerManager.Error($"{fucName}: plotEventLogData实例不存在，无法调用此指令");
                return;
            }

            string key = "SpeHeroSkeleton_" + hero.heroID;
            if (sourceType == 0)
            {
                plotEventLogData.Set(key, null);
                LoggerManager.Debug($"{fucName}: 已清除 {hero.heroName}(ID={hero.heroID}) 的运行时特殊立绘设置");
            }
            else
            {
                string value = sourceType + "#" + sourceParam;
                plotEventLogData.Set(key, value);
                LoggerManager.Debug($"{fucName}: 已设置 {hero.heroName}(ID={hero.heroID}) 的运行时特殊立绘: {value}");
                TryValidateRuntimeSpeHeroSkeletonSource(sourceType, sourceParam, fucName);
            }

            if (forceEnable)
            {
                ForceEnableSpeHeroSkeleton(hero, fucName);
            }

            TryRefreshRuntimeSpeHeroSkeleton(hero.heroID, refreshPlotPanel, fucName);
        }


        private static void TryValidateRuntimeSpeHeroSkeletonSource(int sourceType, string sourceParam, string fucName)
        {
            try
            {
                Type apiType = Type.GetType("TheResourceOfLong.SpeHeroSkeletonRuntimeApi, TheResourceOfLong", false);
                if (apiType == null) return;

                MethodInfo method = apiType.GetMethod("ValidateRuntimeSpeHeroSkeletonSource", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(int), typeof(string) }, null);
                if (method == null)
                {
                    LoggerManager.Debug($"{fucName}: TheResourceOfLong未提供ValidateRuntimeSpeHeroSkeletonSource API，跳过运行时特殊立绘资源校验");
                    return;
                }

                object result = method.Invoke(null, new object[] { sourceType, sourceParam });
                string message = result as string;
                if (!string.IsNullOrWhiteSpace(message))
                {
                    LoggerManager.Warning($"{fucName}: {message}");
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Warning($"{fucName}: 调用TheResourceOfLong资源校验API失败: {ex.Message}");
            }
        }


        private static bool TryParseSpeHeroSkeletonSourceType(string raw, out int sourceType)
        {
            sourceType = -1;
            string value = (raw ?? string.Empty).Trim();
            if (int.TryParse(value, out sourceType))
            {
                return sourceType >= 0 && sourceType <= 2;
            }

            if (string.Equals(value, "Revert", StringComparison.OrdinalIgnoreCase))
            {
                sourceType = 0;
                return true;
            }

            if (string.Equals(value, "SpeHero", StringComparison.OrdinalIgnoreCase))
            {
                sourceType = 1;
                return true;
            }

            if (string.Equals(value, "Mapping", StringComparison.OrdinalIgnoreCase))
            {
                sourceType = 2;
                return true;
            }

            return false;
        }


        private static bool TryParseChangeSpeHeroSkeletonForceEnable(string raw, out bool forceEnable)
        {
            return TryParseChangeSpeHeroSkeletonOptionalBool(raw, out forceEnable);
        }


        private static bool TryParseChangeSpeHeroSkeletonOptionalBool(string raw, out bool value)
        {
            value = false;
            if (string.IsNullOrWhiteSpace(raw)) return true;

            string normalized = raw.Trim();
            if (normalized == "0" || string.Equals(normalized, "false", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }

            if (normalized == "1" || string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }

            return false;
        }


        private static void ForceEnableSpeHeroSkeleton(HeroData hero, string fucName)
        {
            try
            {
                if (hero == null || string.IsNullOrWhiteSpace(hero.heroName)) return;
                if (GameDataController.playerPrefData == null || GameDataController.playerPrefData.playerPrefData == null)
                {
                    LoggerManager.Warning($"{fucName}: playerPrefData不存在，无法强制开启特殊立绘开关");
                    return;
                }

                string preferenceKey = hero.heroName.Trim() + "hideSpeSkeleton";
                GameDataController.playerPrefData.playerPrefData.SetKey(preferenceKey, 0);
                LoggerManager.Debug($"{fucName}: 已强制开启 {hero.heroName}(ID={hero.heroID}) 的特殊立绘开关");
            }
            catch (Exception ex)
            {
                LoggerManager.Warning($"{fucName}: 强制开启特殊立绘开关失败: {ex.Message}");
            }
        }


        private static void TryRefreshRuntimeSpeHeroSkeleton(int heroId, bool refreshPlotPanel, string fucName)
        {
            try
            {
                Type apiType = Type.GetType("TheResourceOfLong.SpeHeroSkeletonRuntimeApi, TheResourceOfLong", false);
                if (apiType == null)
                {
                    LoggerManager.Debug($"{fucName}: TheResourceOfLong未启用，已仅写入运行时特殊立绘变量");
                    return;
                }

                MethodInfo method = apiType.GetMethod("RefreshRuntimeSpeHeroSkeleton", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(int), typeof(bool) }, null);
                if (method != null)
                {
                    method.Invoke(null, new object[] { heroId, refreshPlotPanel });
                    return;
                }

                method = apiType.GetMethod("RefreshRuntimeSpeHeroSkeleton", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(int) }, null);
                if (method != null)
                {
                    method.Invoke(null, new object[] { heroId });
                    return;
                }

                LoggerManager.Warning($"{fucName}: TheResourceOfLong未提供RefreshRuntimeSpeHeroSkeleton API，无法立即刷新可见面板");
            }
            catch (Exception ex)
            {
                LoggerManager.Warning($"{fucName}: 调用TheResourceOfLong刷新API失败: {ex.Message}");
            }
        }
    }
}
