using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 修复普通武学使用高位ID时被原版误判为召唤武学的问题。
    /// 原版以 GlobalData.SummonSkillIDStart 为硬分界；当普通武学ID >= 10000 时，
    /// GetSkillDataBase 会错误地去 summonSkillDataBase 取值。
    /// </summary>
    [HarmonyPatch(typeof(GameDataController), "GetSkillDataBase", new Type[] { typeof(int) })]
    public static class GetSkillDataBasePatch
    {
        private static readonly HashSet<int> RedirectLoggedSkillIDs = new HashSet<int>();
        private static readonly HashSet<int> ConflictLoggedSkillIDs = new HashSet<int>();

        [HarmonyPrefix]
        public static bool Prefix(GameDataController __instance, int skillID, ref KungfuSkillData __result)
        {
            try
            {
                if (__instance == null || skillID < GlobalData.SummonSkillIDStart)
                    return true;

                bool inKungfu = __instance.kungfuSkillDataBase != null
                    && __instance.kungfuSkillDataBase.ContainsKey(skillID);
                bool inSummon = __instance.summonSkillDataBase != null
                    && __instance.summonSkillDataBase.ContainsKey(skillID);

                if (inKungfu && !inSummon)
                {
                    __result = __instance.kungfuSkillDataBase[skillID];
                    LogRedirectOnce(skillID, __result);
                    return false;
                }

                if (inKungfu && inSummon)
                    LogConflictOnce(skillID);

                return true;
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"GetSkillDataBasePatch: 处理高位武学ID失败，回退原版逻辑。skillID={skillID}, 异常={ex}");
                return true;
            }
        }

        private static void LogRedirectOnce(int skillID, KungfuSkillData skillData)
        {
            if (!RedirectLoggedSkillIDs.Add(skillID))
                return;

            string skillName = skillData != null ? skillData.name : string.Empty;
            LoggerManager.Debug($"GetSkillDataBasePatch: 高位普通武学ID已从kungfuSkillDataBase读取。skillID={skillID}, name={skillName}");
        }

        private static void LogConflictOnce(int skillID)
        {
            if (!ConflictLoggedSkillIDs.Add(skillID))
                return;

            LoggerManager.Warning($"GetSkillDataBasePatch: 武学ID同时存在于普通武学表和召唤武学表，保留原版召唤武学分支。skillID={skillID}");
        }
    }
}
