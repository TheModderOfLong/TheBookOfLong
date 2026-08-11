using System;
using HarmonyLib;
using Il2Cpp;

namespace TheExtensionOfLong
{
    [HarmonyPatch(typeof(GameController), "GetHeroName", new[] { typeof(HeroData), typeof(HeroData) })]
    public static class HarmonyPatchForGetHeroName
    {
        [ThreadStatic]
        private static bool _isEvaluatingDefaultAddress;

        /// <summary>
        /// Postfix：在原方法执行后，根据条件覆盖返回值为自定义称呼
        /// 原方法签名：string GetHeroName(HeroData sourceHero, HeroData targetHero)
        /// 自定义称呼通过 PlotEventLogData 中 key="call_{sourceID}_{targetID}" 存储，
        /// 可由 SetHeroRelationShipText 指令设置
        /// </summary>
        [HarmonyPostfix]
        public static void GetHeroNamePostfix(
            HeroData sourceHero,
            HeroData targetHero,
            ref string __result)
        {
            // LoggerManager.Debug("GetHeroName: 尝试载入自定义称呼中……");

            if (sourceHero == null || targetHero == null || targetHero.tempPlotHero)
            {
                // LoggerManager.Debug("GetHeroName: sourceHero或targetHero为null，跳过");
                return;
            }

            GameController gameController = GameController.Instance;
            if (gameController == null)
            {
                LoggerManager.Warning("GetHeroName: GameController实例不存在，无法重载称呼");
                return;
            }

            WorldData worldData = gameController.worldData;
            if (worldData == null)
            {
                LoggerManager.Warning("GetHeroName: WorldData实例不存在，无法重载称呼");
                return;
            }

            PlotEventLogData plotEventLogData = worldData.PlotEventLog;
            if (plotEventLogData == null)
            {
                LoggerManager.Warning("GetHeroName: PlotEventLogData实例不存在，无法重载称呼");
                return;
            }

            // LoggerManager.Debug("GetHeroName: 源角色: " + sourceHero.heroName + ", 目标角色: " + targetHero.heroName);

            if (IsTargetRalation(worldData, sourceHero, targetHero))
            {
                // string targetCallKey = "HeroAddressForm_" + sourceHero.heroName + "_" + targetHero.heroName;
                string targetCallKey = "HeroAddressForm_" + sourceHero.heroID + "_" + targetHero.heroID;
                if (plotEventLogData.HaveKey(targetCallKey))
                {
                    string targetCall = plotEventLogData.Get(targetCallKey);
                    if (!string.IsNullOrEmpty(targetCall))
                    {
                        LoggerManager.Debug($"GetHeroName: 使用自定义称呼: {__result} → {targetCall}");
                        __result = targetCall;
                        return;
                    }
                    else
                    {
                        // LoggerManager.Debug("GetHeroName: 无自定义称呼，返回默认称呼");
                    }
                }
                else
                {
                    // LoggerManager.Debug("GetHeroName: 无自定义称呼，返回默认称呼");
                }
            }

            TryApplyDefaultAddressForm(sourceHero, targetHero, ref __result);
        }

        private static void TryApplyDefaultAddressForm(HeroData sourceHero, HeroData targetHero, ref string result)
        {
            if (_isEvaluatingDefaultAddress || !HeroAddressFormRegistry.HasRules)
                return;

            PlotController plotController = PlotController.Instance;
            if (plotController == null)
                return;

            try
            {
                _isEvaluatingDefaultAddress = true;
                string defaultForm;
                if (HeroAddressFormRegistry.TryGetAddressForm(plotController, sourceHero, targetHero, out defaultForm))
                {
                    LoggerManager.Debug($"GetHeroName: 使用默认称呼规则: {result} → {defaultForm}");
                    result = defaultForm;
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("GetHeroName: 默认称呼规则求值失败: " + ex.Message);
            }
            finally
            {
                _isEvaluatingDefaultAddress = false;
            }
        }

        public static bool IsTargetRalation(WorldData worldData, HeroData sourceHero, HeroData targetHero)
        {
            if (worldData != null)
            {
                // 源角色为主角且目标角色好感度低于80
                if (sourceHero == worldData.Player() && targetHero.favor < 80)
                {
                    return false;
                }

                // 目标角色为主角且源角色好感度低于80
                if (targetHero == worldData.Player() && sourceHero.favor < 80)
                {
                    return false;
                }
            }

            // 源角色与目标角色的关系未超过朋友
            if (!sourceHero.HaveRelationBetterThanFriend(targetHero.heroID, true, true))
            {
                return false;
            }
            return true;
        }
    }
}
