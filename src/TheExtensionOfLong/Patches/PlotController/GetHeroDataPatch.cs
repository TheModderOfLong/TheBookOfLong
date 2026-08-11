using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 对 PlotController.GetHeroData 的 HarmonyPatch
    /// 当 heroName 为特定关键字时，忽略 targetHeroType 参数，按关键字语义返回角色数据
    /// 关键字解析委托给 CommonHandlers.ResolveHeroSource，本Patch仅额外处理：
    ///   - ResolveAllCommands 预处理（查询指令替换）
    ///   - "保持/Keep" → origin（仅GetHeroData上下文有此语义）
    /// </summary>
    [HarmonyPatch(typeof(PlotController), "GetHeroData",
        new[] { typeof(PlotTargetHeroType), typeof(string), typeof(HeroData) })]
    public static class PlotControllerGetHeroDataPatch
    {
        [HarmonyPrefix]
        public static bool GetHeroDataPrefix(
            PlotController __instance,
            PlotTargetHeroType targetHeroType,
            string heroName,
            HeroData origin,
            ref HeroData __result)
        {
            if (targetHeroType != PlotTargetHeroType.HeroName)
                return true; // 不拦截，走原逻辑

            if (heroName == null || heroName == "")
                return true; // 不拦截，走原逻辑

            // 如果存在查询指令则对其进行解析
            string resolved = ConditionQueryHandlers.ResolveAllCommands(__instance, heroName);
            if (resolved != heroName)
            {
                LoggerManager.Debug($"GetHeroData: heroName解析: \"{heroName}\" → \"{resolved}\"");
                heroName = resolved;
            }

            string lowerName = heroName.ToLower();

            // "保持" 或 "Keep" → 返回 origin（仅GetHeroData上下文有此语义，不纳入ResolveHeroSource）
            if (lowerName == "保持" || lowerName == "keep")
            {
                __result = origin;
                LoggerManager.Debug("GetHeroData: heroName为保持/Keep，返回origin=" + __result?.heroName);
                return false;
            }

            // 委托 ResolveHeroSource 解析所有关键字
            HeroData heroData = CommonHandlers.ResolveHeroSource(__instance, heroName);
            if (heroData != null)
            {
                __result = heroData;
                LoggerManager.Debug("GetHeroData: heroName为关键字，返回=" + __result?.heroName);
                return false;
            }

            // 非关键字，走原逻辑
            return true;
        }
    }
}
