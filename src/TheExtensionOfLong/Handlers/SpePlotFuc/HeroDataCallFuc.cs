using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppSystem.Collections.Generic;
using System.Reflection;
using System;

namespace TheExtensionOfLong
{
    [SpePlotFuc("HeroDataCallFuc")]
    public static class SpePlotFucHeroDataCallFuc
    {
        /// <summary>
        /// HeroDataCallFuc 指令：对 HeroData 实例调用指定函数
        /// 格式: HeroDataCallFuc*角色ID#函数名#函数参数(可选)
        ///   角色ID: 通过 CommonHandlers.ResolveHeroId 解析（int ID / 名称 / player 等关键字）
        ///   函数名: HeroData 的无参方法名（反射调用）、复合动作名，或可写属性/字段名
        ///   函数参数: 复合动作的参数，用"-"分隔；当函数名为属性/字段名时，表示要写入的新值
        ///
        /// 支持的无参方法(示例): GoInPrison, GoOutPrison, SetNeedRemove, DeadToAlive,
        ///   RecoverState, ResetLoyal, ResetAI, FullRecover(提示=false), RemoveLover(提示=false) 等
        ///
        /// 支持的复合动作:
        ///   ChangeForceContribution=数量-是否提示-门派ID
        ///   ChangeHeroForceLv=等级-是否提示
        ///   ChangeMoney=数量-是否提示
        ///   ChangeFame=数量-是否提示
        ///   ChangeBadFame=数量-是否提示
        ///   ChangeLoyal=数量-是否提示
        ///   ChangeFavor=数量-是否提示-最大好感-强制倍率-成功音效
        ///   ChangeHp=数量-使用恢复率-不死-刷新-提示
        ///   ChangeMana=数量-使用恢复率-刷新-提示
        ///   ChangePower=数量-提示
        ///   ChangeGovernContribution=数量-是否提示
        ///   ChangeGovernLv=等级
        ///   ChangeHornorLv=等级
        ///   SetLover=目标角色ID-是否提示
        ///   RemoveLover=是否提示
        ///   RemoveTeacher=是否提示
        ///   RemoveAllPrelover=是否提示
        ///   RemoveAllStudent=是否提示
        ///   FullRecover=是否提示
        ///   AddTag=标签ID-时间-来源-提示
        ///   RemoveTag=标签ID-是否提示
        ///   AddLog=日志内容
        ///   SetCustomValue#属性名=属性值（推荐）或 SetCustomValue=属性名-属性值（兼容）
        ///   字段名=字段值（写入公开实例属性或字段，支持数字、布尔、字符串、枚举）
        ///
        /// 示例:
        ///   HeroDataCallFuc*player#GoInPrison
        ///   HeroDataCallFuc*小白#ChangeMoney=1000-true
        ///   HeroDataCallFuc*player#ChangeForceContribution=50-true--1
        ///   HeroDataCallFuc*player#ChangeFame=100-true
        ///   HeroDataCallFuc*player#ChangeHp=50-true-false-true-false
        ///   HeroDataCallFuc*小白#SetLover=player-true
        ///   HeroDataCallFuc*小白#SetCustomValue#bioFather=张三
        ///   HeroDataCallFuc*小白#hide=true
        /// </summary>
        public static void TryCall(PlotController __instance, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 2 || string.IsNullOrWhiteSpace(fucParams[0]) || string.IsNullOrWhiteSpace(fucParams[1]))
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[HeroDataCallFuc*角色ID#函数名#函数参数(可选)]");
                return;
            }

            string heroIdRaw = fucParams[0];
            string funcInfo = fucParams[1];

            // 解析角色
            HeroData hero = CommonHandlers.ResolveHeroId(__instance, heroIdRaw);
            if (hero == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到角色 \"{heroIdRaw}\"");
                return;
            }

            // 解析函数名和参数
            // 支持两种格式:
            //   FuncName=Arg1-Arg2    (fucParams[1] 含 =)
            //   FuncName#Arg1-Arg2    (fucParams[2] 作为参数)
            int eqIdx = funcInfo.IndexOf('=');
            string methodName;
            string methodArg;

            if (eqIdx >= 0)
            {
                methodName = funcInfo.Substring(0, eqIdx);
                methodArg = funcInfo.Substring(eqIdx + 1);
            }
            else
            {
                methodName = funcInfo;
                methodArg = fucParams.Length > 2 ? fucParams[2] : "";
            }

            // 优先匹配复合动作
            if (HeroCallFucCompositeActions.TryGetValue(methodName, out var handler))
            {
                if (methodName.Equals("SetCustomValue", StringComparison.OrdinalIgnoreCase))
                {
                    string[] customValueArgs = eqIdx >= 0
                        ? methodArg.Split('-')
                        : new string[] { methodArg };
                    handler(__instance, hero, customValueArgs);
                    return;
                }

                string[] args = methodArg.Split('-');
                // 将参数中的"负"还原为"-"，以支持负数等含"-"的参数值
                for (int i = 0; i < args.Length; i++)
                {
                    args[i] = args[i].Replace("负", "-");
                }
                handler(__instance, hero, args);
                return;
            }

            // 字段/属性写入：含参数且未命中复合动作时，尝试按公开实例属性或字段赋值。
            if (eqIdx >= 0 || fucParams.Length > 2)
            {
                if (CommonHandlers.TryWriteObjectMemberValue(hero, "HeroData", methodName, methodArg, out string oldValue, out string newValue))
                {
                    LoggerManager.Debug($"{fucName}: 已设置 {hero.heroName}.{methodName}: {oldValue} → {newValue}");
                    return;
                }

                LoggerManager.Warning($"{fucName}: HeroData 未找到复合动作或可写公开字段/属性 \"{methodName}\"");
                return;
            }

            // 无参复合动作（不带=号调用）
            if (HeroCallFucCompositeActions.ContainsKey(methodName))
            {
                handler(__instance, hero, new string[0]);
                return;
            }

            // 无参方法：通过反射调用
            try
            {
                var bindingFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase;
                System.Reflection.MethodInfo method = hero.GetType().GetMethod(methodName, bindingFlags, null, Type.EmptyTypes, null);
                if (method != null)
                {
                    method.Invoke(hero, null);
                    LoggerManager.Debug($"{fucName}: 已调用 {hero.heroName}.{methodName}()");
                    return;
                }

                LoggerManager.Warning($"{fucName}: HeroData 未找到无参方法 \"{methodName}\"，且非已知复合动作");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"{fucName}: 调用 {hero.heroName}.{methodName}() 失败: {e.Message}");
            }
        }

        private static readonly System.Collections.Generic.Dictionary<string, System.Action<PlotController, HeroData, string[]>> HeroCallFucCompositeActions
            = new System.Collections.Generic.Dictionary<string, System.Action<PlotController, HeroData, string[]>>(StringComparer.OrdinalIgnoreCase)
        {
            { "ChangeForceContribution",     HeroCallFucChangeForceContribution },
            { "ChangeHeroForceLv",           HeroCallFucChangeHeroForceLv },
            { "ChangeMoney",                 HeroCallFucChangeMoney },
            { "ChangeFame",                  HeroCallFucChangeFame },
            { "ChangeBadFame",               HeroCallFucChangeBadFame },
            { "ChangeLoyal",                 HeroCallFucChangeLoyal },
            { "ChangeFavor",                 HeroCallFucChangeFavor },
            { "ChangeHp",                    HeroCallFucChangeHp },
            { "ChangeMana",                  HeroCallFucChangeMana },
            { "ChangePower",                 HeroCallFucChangePower },
            { "LeaveForce",                  HeroCallFucLeaveForce },
            { "JoinForce",                   HeroCallFucJoinForce },
            { "SetForce",                    HeroCallFucSetForce },
            { "JoinForceServant",            HeroCallFucJoinForceServant },
            { "LeaveServantForce",           HeroCallFucLeaveServantForce },
            { "ClearForceJob",               HeroCallFucClearForceJob },
            { "AutoChangeLoyal",             HeroCallFucAutoChangeLoyal },
            { "ChangeGovernContribution",    HeroCallFucChangeGovernContribution },
            { "ChangeGovernLv",              HeroCallFucChangeGovernLv },
            { "ChangeHornorLv",              HeroCallFucChangeHornorLv },
            { "AddFriend",                   HeroCallFucAddFriend },
            { "RemoveFriend",                HeroCallFucRemoveFriend },
            { "AddHater",                    HeroCallFucAddHater },
            { "RemoveHater",                 HeroCallFucRemoveHater },
            { "AddBrother",                  HeroCallFucAddBrother },
            { "RemoveBrother",               HeroCallFucRemoveBrother },
            { "RemoveRelative",              HeroCallFucRemoveRelative },
            { "AddStudent",                  HeroCallFucAddStudent },
            { "RemoveStudent",               HeroCallFucRemoveStudent },
            { "AddPrelover",                 HeroCallFucAddPrelover },
            { "RemovePrelover",              HeroCallFucRemovePrelover },
            { "SetLover",                    HeroCallFucSetLover },
            { "RemoveLover",                 HeroCallFucRemoveLover },
            { "RemoveTeacher",               HeroCallFucRemoveTeacher },
            { "RemoveAllPrelover",           HeroCallFucRemoveAllPrelover },
            { "RemoveAllStudent",            HeroCallFucRemoveAllStudent },
            { "SetFavor",                    HeroCallFucSetFavor },
            { "SetHeroMeet",                 HeroCallFucSetHeroMeet },
            { "ChangeTagPoint",              HeroCallFucChangeTagPoint },
            { "ChangeMaxHp",                 HeroCallFucChangeMaxHp },
            { "ChangeMaxMana",               HeroCallFucChangeMaxMana },
            { "ChangeMaxPower",              HeroCallFucChangeMaxPower },
            { "ChangeAttri",                 HeroCallFucChangeAttri },
            { "ChangeFightSkill",            HeroCallFucChangeFightSkill },
            { "ChangeLivingSkill",           HeroCallFucChangeLivingSkill },
            { "ChangeLivingSkillExp",        HeroCallFucChangeLivingSkillExp },
            { "ChangeMaxAttri",              HeroCallFucChangeMaxAttri },
            { "ChangeMaxFightSkill",         HeroCallFucChangeMaxFightSkill },
            { "ChangeMaxLivingSkill",        HeroCallFucChangeMaxLivingSkill },
            { "ChangeSelfHouseTotalAdd",     HeroCallFucChangeSelfHouseTotalAdd },
            { "ChangeExternalInjury",        HeroCallFucChangeExternalInjury },
            { "ChangeInternalInjury",        HeroCallFucChangeInternalInjury },
            { "ChangePoisonInjury",          HeroCallFucChangePoisonInjury },
            { "ChangeRandomInjury",          HeroCallFucChangeRandomInjury },
            { "AutoFightQuickChangeState",   HeroCallFucAutoFightQuickChangeState },
            { "AddBuff",                     HeroCallFucAddBuff },
            { "DisUnderstandTag",            HeroCallFucDisUnderstandTag },
            { "ClearAllTempTag",             HeroCallFucClearAllTempTag },
            { "SetSkin",                     HeroCallFucSetSkin },
            { "ResetDefaultSkin",            HeroCallFucResetDefaultSkin },
            { "RandomFaceData",              HeroCallFucRandomFaceData },
            { "SetHeroForceLv",              HeroCallFucSetHeroForceLv },
            { "ClearContributionRecord",     HeroCallFucClearContributionRecord },
            { "CheckOutForceContribution",   HeroCallFucCheckOutForceContribution },
            { "CheckHeroFameForceLv",        HeroCallFucCheckHeroFameForceLv },
            { "RefreshHeroSalaryAndPopulation", HeroCallFucRefreshHeroSalaryAndPopulation },
            { "RefreshMaxAttriAndSkill",     HeroCallFucRefreshMaxAttriAndSkill },
            { "ResetAutoSetting",            HeroCallFucResetAutoSetting },
            { "AutoGetFightExp",             HeroCallFucAutoGetFightExp },
            { "FightReset",                  HeroCallFucFightReset },
            { "ManageAIInPrison",            HeroCallFucManageAIInPrison },
            { "LoseAllItem",                 HeroCallFucLoseAllItem },
            { "LoseAllSkill",                HeroCallFucLoseAllSkill },
            { "RandomBigMapMovePos",         HeroCallFucRandomBigMapMovePos },
            { "FullRecover",                 HeroCallFucFullRecover },
            { "AddTag",                      HeroCallFucAddTag },
            { "RemoveTag",                   HeroCallFucRemoveTag },
            { "AddLog",                      HeroCallFucAddLog },
            { "SetCustomValue",              HeroCallFucSetCustomValue },
        };

        // ===== HeroDataCallFuc 复合动作实现 =====

        private static void RunHeroAction(HeroData hero, string actionName, string logArgs, System.Action action)
        {
            try
            {
                action();
                LoggerManager.Debug($"  HeroDataCallFuc: {hero.heroName}.{actionName}({logArgs})");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroDataCallFuc {actionName} 失败: {e.Message}");
            }
        }

        /// <summary>
        /// ChangeForceContribution=数量-是否提示-门派ID
        /// 默认: 数量=0, 提示=true, 门派ID=-1
        /// </summary>
        private static void HeroCallFucChangeForceContribution(PlotController pc, HeroData hero, string[] args)
        {
            float num = args.Length > 0 && float.TryParse(args[0], out float n) ? n : 0f;
            bool showInfo = CommonHandlers.GetBoolArg(args, 1, true);
            int targetForce = args.Length > 2 && int.TryParse(args[2], out int f) ? f : -1;
            try
            {
                hero.ChangeForceContribution(num, showInfo, targetForce);
                LoggerManager.Debug($"  HeroDataCallFuc: {hero.heroName}.ChangeForceContribution({num}, {showInfo}, {targetForce})");
            }
            catch (Exception e) { LoggerManager.Error($"  HeroDataCallFuc ChangeForceContribution 失败: {e.Message}"); }
        }

        /// <summary>
        /// ChangeHeroForceLv=等级-是否提示
        /// 默认: 等级=0, 提示=true
        /// </summary>
        private static void HeroCallFucChangeHeroForceLv(PlotController pc, HeroData hero, string[] args)
        {
            int num = args.Length > 0 && int.TryParse(args[0], out int n) ? n : 0;
            bool showInfo = CommonHandlers.GetBoolArg(args, 1, true);
            try
            {
                hero.ChangeHeroForceLv(num, showInfo);
                LoggerManager.Debug($"  HeroDataCallFuc: {hero.heroName}.ChangeHeroForceLv({num}, {showInfo})");
            }
            catch (Exception e) { LoggerManager.Error($"  HeroDataCallFuc ChangeHeroForceLv 失败: {e.Message}"); }
        }

        /// <summary>
        /// ChangeMoney=数量-是否提示
        /// 默认: 数量=0, 提示=true
        /// </summary>
        private static void HeroCallFucChangeMoney(PlotController pc, HeroData hero, string[] args)
        {
            int num = args.Length > 0 && int.TryParse(args[0], out int n) ? n : 0;
            bool showInfo = CommonHandlers.GetBoolArg(args, 1, true);
            try
            {
                hero.ChangeMoney(num, showInfo);
                LoggerManager.Debug($"  HeroDataCallFuc: {hero.heroName}.ChangeMoney({num}, {showInfo})");
            }
            catch (Exception e) { LoggerManager.Error($"  HeroDataCallFuc ChangeMoney 失败: {e.Message}"); }
        }

        /// <summary>
        /// ChangeFame=数量-是否提示
        /// 默认: 数量=0, 提示=true
        /// </summary>
        private static void HeroCallFucChangeFame(PlotController pc, HeroData hero, string[] args)
        {
            float num = args.Length > 0 && float.TryParse(args[0], out float n) ? n : 0f;
            bool showInfo = CommonHandlers.GetBoolArg(args, 1, true);
            try
            {
                hero.ChangeFame(num, showInfo);
                LoggerManager.Debug($"  HeroDataCallFuc: {hero.heroName}.ChangeFame({num}, {showInfo})");
            }
            catch (Exception e) { LoggerManager.Error($"  HeroDataCallFuc ChangeFame 失败: {e.Message}"); }
        }

        /// <summary>
        /// ChangeBadFame=数量-是否提示
        /// 默认: 数量=0, 提示=true
        /// </summary>
        private static void HeroCallFucChangeBadFame(PlotController pc, HeroData hero, string[] args)
        {
            float num = args.Length > 0 && float.TryParse(args[0], out float n) ? n : 0f;
            bool showInfo = CommonHandlers.GetBoolArg(args, 1, true);
            try
            {
                hero.ChangeBadFame(num, showInfo);
                LoggerManager.Debug($"  HeroDataCallFuc: {hero.heroName}.ChangeBadFame({num}, {showInfo})");
            }
            catch (Exception e) { LoggerManager.Error($"  HeroDataCallFuc ChangeBadFame 失败: {e.Message}"); }
        }

        /// <summary>
        /// ChangeLoyal=数量-是否提示
        /// 默认: 数量=0, 提示=false
        /// </summary>
        private static void HeroCallFucChangeLoyal(PlotController pc, HeroData hero, string[] args)
        {
            float num = args.Length > 0 && float.TryParse(args[0], out float n) ? n : 0f;
            bool showInfo = CommonHandlers.GetBoolArg(args, 1, false);
            try
            {
                hero.ChangeLoyal(num, showInfo);
                LoggerManager.Debug($"  HeroDataCallFuc: {hero.heroName}.ChangeLoyal({num}, {showInfo})");
            }
            catch (Exception e) { LoggerManager.Error($"  HeroDataCallFuc ChangeLoyal 失败: {e.Message}"); }
        }

        /// <summary>
        /// ChangeFavor=数量-是否提示-最大好感-强制倍率-成功音效
        /// 默认: 数量=0, 提示=true, 最大好感=100, 强制倍率=0, 成功音效=false
        /// </summary>
        private static void HeroCallFucChangeFavor(PlotController pc, HeroData hero, string[] args)
        {
            float num = args.Length > 0 && float.TryParse(args[0], out float n) ? n : 0f;
            bool showPopInfo = CommonHandlers.GetBoolArg(args, 1, true);
            float maxFavor = args.Length > 2 && float.TryParse(args[2], out float mf) ? mf : 100f;
            float forceRate = args.Length > 3 && float.TryParse(args[3], out float fr) ? fr : 0f;
            bool successSound = CommonHandlers.GetBoolArg(args, 4, false);
            try
            {
                hero.ChangeFavor(num, showPopInfo, maxFavor, forceRate, successSound);
                LoggerManager.Debug($"  HeroDataCallFuc: {hero.heroName}.ChangeFavor({num}, {showPopInfo}, {maxFavor}, {forceRate}, {successSound})");
            }
            catch (Exception e) { LoggerManager.Error($"  HeroDataCallFuc ChangeFavor 失败: {e.Message}"); }
        }

        /// <summary>
        /// ChangeHp=数量-使用恢复率-不死-刷新-提示
        /// 默认: 数量=0, 使用恢复率=true, 不死=false, 刷新=true, 提示=false
        /// </summary>
        private static void HeroCallFucChangeHp(PlotController pc, HeroData hero, string[] args)
        {
            float num = args.Length > 0 && float.TryParse(args[0], out float n) ? n : 0f;
            bool useRecoverRate = CommonHandlers.GetBoolArg(args, 1, true);
            bool noDead = CommonHandlers.GetBoolArg(args, 2, false);
            bool needRefresh = CommonHandlers.GetBoolArg(args, 3, true);
            bool showInfo = CommonHandlers.GetBoolArg(args, 4, false);
            try
            {
                hero.ChangeHp(num, useRecoverRate, noDead, needRefresh, showInfo);
                LoggerManager.Debug($"  HeroDataCallFuc: {hero.heroName}.ChangeHp({num}, {useRecoverRate}, {noDead}, {needRefresh}, {showInfo})");
            }
            catch (Exception e) { LoggerManager.Error($"  HeroDataCallFuc ChangeHp 失败: {e.Message}"); }
        }

        /// <summary>
        /// ChangeMana=数量-使用恢复率-刷新-提示
        /// 默认: 数量=0, 使用恢复率=true, 刷新=true, 提示=false
        /// </summary>
        private static void HeroCallFucChangeMana(PlotController pc, HeroData hero, string[] args)
        {
            float num = args.Length > 0 && float.TryParse(args[0], out float n) ? n : 0f;
            bool useRecoverRate = CommonHandlers.GetBoolArg(args, 1, true);
            bool needRefresh = CommonHandlers.GetBoolArg(args, 2, true);
            bool showInfo = CommonHandlers.GetBoolArg(args, 3, false);
            try
            {
                hero.ChangeMana(num, useRecoverRate, needRefresh, showInfo);
                LoggerManager.Debug($"  HeroDataCallFuc: {hero.heroName}.ChangeMana({num}, {useRecoverRate}, {needRefresh}, {showInfo})");
            }
            catch (Exception e) { LoggerManager.Error($"  HeroDataCallFuc ChangeMana 失败: {e.Message}"); }
        }

        /// <summary>
        /// ChangePower=数量-提示
        /// 默认: 数量=0, 提示=false
        /// </summary>
        private static void HeroCallFucChangePower(PlotController pc, HeroData hero, string[] args)
        {
            float num = args.Length > 0 && float.TryParse(args[0], out float n) ? n : 0f;
            bool showInfo = CommonHandlers.GetBoolArg(args, 1, false);
            try
            {
                hero.ChangePower(num, showInfo);
                LoggerManager.Debug($"  HeroDataCallFuc: {hero.heroName}.ChangePower({num}, {showInfo})");
            }
            catch (Exception e) { LoggerManager.Error($"  HeroDataCallFuc ChangePower 失败: {e.Message}"); }
        }

        /// <summary>
        /// LeaveForce=是否提示-是否移除师父
        /// 默认: 提示=true, 移除师父=true
        /// </summary>
        private static void HeroCallFucLeaveForce(PlotController pc, HeroData hero, string[] args)
        {
            bool showInfo = CommonHandlers.GetBoolArg(args, 0, true);
            bool removeTeacher = CommonHandlers.GetBoolArg(args, 1, true);
            RunHeroAction(hero, "LeaveForce", $"{showInfo}, {removeTeacher}", () => hero.LeaveForce(showInfo, removeTeacher));
        }

        /// <summary>
        /// JoinForce=势力ID-势力等级-辈分-是否提示-是否拜领导者为师
        /// 默认: 势力等级=-1, 辈分=-1, 提示=true, 拜师=true
        /// </summary>
        private static void HeroCallFucJoinForce(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "JoinForce", "势力ID", out int forceID)) return;
            int forceLv = CommonHandlers.GetIntArg(args, 1, -1);
            int generation = CommonHandlers.GetIntArg(args, 2, -1);
            bool showInfo = CommonHandlers.GetBoolArg(args, 3, true);
            bool setTeacherToLeader = CommonHandlers.GetBoolArg(args, 4, true);
            RunHeroAction(hero, "JoinForce", $"{forceID}, {forceLv}, {generation}, {showInfo}, {setTeacherToLeader}", () => hero.JoinForce(forceID, forceLv, generation, showInfo, setTeacherToLeader));
        }

        /// <summary>
        /// SetForce=势力ID-势力等级
        /// </summary>
        private static void HeroCallFucSetForce(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "SetForce", "势力ID", out int forceID)) return;
            if (!CommonHandlers.TryGetIntArg(args, 1, "SetForce", "势力等级", out int forceLv)) return;
            RunHeroAction(hero, "SetForce", $"{forceID}, {forceLv}", () => hero.SetForce(forceID, forceLv));
        }

        /// <summary>
        /// JoinForceServant=势力ID
        /// </summary>
        private static void HeroCallFucJoinForceServant(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "JoinForceServant", "势力ID", out int forceID)) return;
            RunHeroAction(hero, "JoinForceServant", $"{forceID}", () => hero.JoinForceServant(forceID));
        }

        private static void HeroCallFucLeaveServantForce(PlotController pc, HeroData hero, string[] args)
        {
            RunHeroAction(hero, "LeaveServantForce", "", () => hero.LeaveServantForce());
        }

        private static void HeroCallFucClearForceJob(PlotController pc, HeroData hero, string[] args)
        {
            RunHeroAction(hero, "ClearForceJob", "", () => hero.ClearForceJob());
        }

        private static void HeroCallFucAutoChangeLoyal(PlotController pc, HeroData hero, string[] args)
        {
            RunHeroAction(hero, "AutoChangeLoyal", "", () => hero.AutoChangeLoyal());
        }

        private static void HeroCallFucRelationByHeroId(PlotController pc, HeroData hero, string[] args, string actionName, bool defaultShowInfo, System.Action<int, bool> action)
        {
            if (!CommonHandlers.TryGetHeroIdArg(pc, args, 0, $"HeroDataCallFuc {actionName}", out int heroID)) return;
            bool showInfo = CommonHandlers.GetBoolArg(args, 1, defaultShowInfo);
            RunHeroAction(hero, actionName, $"{heroID}, {showInfo}", () => action(heroID, showInfo));
        }

        private static void HeroCallFucAddFriend(PlotController pc, HeroData hero, string[] args)
        {
            HeroCallFucRelationByHeroId(pc, hero, args, "AddFriend", false, hero.AddFriend);
        }

        private static void HeroCallFucRemoveFriend(PlotController pc, HeroData hero, string[] args)
        {
            HeroCallFucRelationByHeroId(pc, hero, args, "RemoveFriend", false, hero.RemoveFriend);
        }

        private static void HeroCallFucAddHater(PlotController pc, HeroData hero, string[] args)
        {
            HeroCallFucRelationByHeroId(pc, hero, args, "AddHater", false, hero.AddHater);
        }

        private static void HeroCallFucRemoveHater(PlotController pc, HeroData hero, string[] args)
        {
            HeroCallFucRelationByHeroId(pc, hero, args, "RemoveHater", false, hero.RemoveHater);
        }

        private static void HeroCallFucAddBrother(PlotController pc, HeroData hero, string[] args)
        {
            HeroCallFucRelationByHeroId(pc, hero, args, "AddBrother", false, hero.AddBrother);
        }

        private static void HeroCallFucRemoveBrother(PlotController pc, HeroData hero, string[] args)
        {
            HeroCallFucRelationByHeroId(pc, hero, args, "RemoveBrother", false, hero.RemoveBrother);
        }

        private static void HeroCallFucRemoveRelative(PlotController pc, HeroData hero, string[] args)
        {
            HeroCallFucRelationByHeroId(pc, hero, args, "RemoveRelative", false, hero.RemoveRelative);
        }

        private static void HeroCallFucAddStudent(PlotController pc, HeroData hero, string[] args)
        {
            HeroCallFucRelationByHeroId(pc, hero, args, "AddStudent", false, hero.AddStudent);
        }

        private static void HeroCallFucRemoveStudent(PlotController pc, HeroData hero, string[] args)
        {
            HeroCallFucRelationByHeroId(pc, hero, args, "RemoveStudent", false, hero.RemoveStudent);
        }

        private static void HeroCallFucAddPrelover(PlotController pc, HeroData hero, string[] args)
        {
            HeroCallFucRelationByHeroId(pc, hero, args, "AddPrelover", false, hero.AddPrelover);
        }

        private static void HeroCallFucRemovePrelover(PlotController pc, HeroData hero, string[] args)
        {
            HeroCallFucRelationByHeroId(pc, hero, args, "RemovePrelover", false, hero.RemovePrelover);
        }

        /// <summary>
        /// SetFavor=值-是否提示
        /// </summary>
        private static void HeroCallFucSetFavor(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetFloatArg(args, 0, "SetFavor", "好感值", out float num)) return;
            bool showPopInfo = CommonHandlers.GetBoolArg(args, 1, false);
            RunHeroAction(hero, "SetFavor", $"{num}, {showPopInfo}", () => hero.SetFavor(num, showPopInfo));
        }

        /// <summary>
        /// SetHeroMeet=是否提示-初始好感
        /// 默认: 提示=true, 初始好感=-999
        /// </summary>
        private static void HeroCallFucSetHeroMeet(PlotController pc, HeroData hero, string[] args)
        {
            bool showInfo = CommonHandlers.GetBoolArg(args, 0, true);
            float startFavor = CommonHandlers.GetFloatArg(args, 1, -999f);
            RunHeroAction(hero, "SetHeroMeet", $"{showInfo}, {startFavor}", () => hero.SetHeroMeet(showInfo, startFavor));
        }

        private static void HeroCallFucChangeTagPoint(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetFloatArg(args, 0, "ChangeTagPoint", "变化量", out float delta)) return;
            bool showInfo = CommonHandlers.GetBoolArg(args, 1, false);
            RunHeroAction(hero, "ChangeTagPoint", $"{delta}, {showInfo}", () => hero.ChangeTagPoint(delta, showInfo));
        }

        private static void HeroCallFucChangeMaxHp(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetFloatArg(args, 0, "ChangeMaxHp", "变化量", out float num)) return;
            bool showInfo = CommonHandlers.GetBoolArg(args, 1, false);
            RunHeroAction(hero, "ChangeMaxHp", $"{num}, {showInfo}", () => hero.ChangeMaxHp(num, showInfo));
        }

        private static void HeroCallFucChangeMaxMana(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetFloatArg(args, 0, "ChangeMaxMana", "变化量", out float num)) return;
            bool showInfo = CommonHandlers.GetBoolArg(args, 1, false);
            RunHeroAction(hero, "ChangeMaxMana", $"{num}, {showInfo}", () => hero.ChangeMaxMana(num, showInfo));
        }

        private static void HeroCallFucChangeMaxPower(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetFloatArg(args, 0, "ChangeMaxPower", "变化量", out float num)) return;
            bool showInfo = CommonHandlers.GetBoolArg(args, 1, false);
            RunHeroAction(hero, "ChangeMaxPower", $"{num}, {showInfo}", () => hero.ChangeMaxPower(num, showInfo));
        }

        private static void HeroCallFucChangeAttri(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "ChangeAttri", "属性ID", out int id)) return;
            if (!CommonHandlers.TryGetFloatArg(args, 1, "ChangeAttri", "变化量", out float num)) return;
            bool showText = CommonHandlers.GetBoolArg(args, 2, false);
            bool skillUpgrade = CommonHandlers.GetBoolArg(args, 3, false);
            RunHeroAction(hero, "ChangeAttri", $"{id}, {num}, {showText}, {skillUpgrade}", () => hero.ChangeAttri(id, num, showText, skillUpgrade));
        }

        private static void HeroCallFucChangeFightSkill(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "ChangeFightSkill", "技能ID", out int id)) return;
            if (!CommonHandlers.TryGetFloatArg(args, 1, "ChangeFightSkill", "变化量", out float num)) return;
            bool showText = CommonHandlers.GetBoolArg(args, 2, false);
            bool skillUpgrade = CommonHandlers.GetBoolArg(args, 3, false);
            RunHeroAction(hero, "ChangeFightSkill", $"{id}, {num}, {showText}, {skillUpgrade}", () => hero.ChangeFightSkill(id, num, showText, skillUpgrade));
        }

        private static void HeroCallFucChangeLivingSkill(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "ChangeLivingSkill", "技能ID", out int id)) return;
            if (!CommonHandlers.TryGetFloatArg(args, 1, "ChangeLivingSkill", "变化量", out float num)) return;
            bool showText = CommonHandlers.GetBoolArg(args, 2, false);
            bool skillUpgrade = CommonHandlers.GetBoolArg(args, 3, false);
            RunHeroAction(hero, "ChangeLivingSkill", $"{id}, {num}, {showText}, {skillUpgrade}", () => hero.ChangeLivingSkill(id, num, showText, skillUpgrade));
        }

        private static void HeroCallFucChangeLivingSkillExp(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "ChangeLivingSkillExp", "技能ID", out int id)) return;
            if (!CommonHandlers.TryGetFloatArg(args, 1, "ChangeLivingSkillExp", "变化量", out float num)) return;
            bool showText = CommonHandlers.GetBoolArg(args, 2, false);
            RunHeroAction(hero, "ChangeLivingSkillExp", $"{id}, {num}, {showText}", () => hero.ChangeLivingSkillExp(id, num, showText));
        }

        private static void HeroCallFucChangeMaxAttri(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "ChangeMaxAttri", "属性ID", out int id)) return;
            if (!CommonHandlers.TryGetIntArg(args, 1, "ChangeMaxAttri", "变化量", out int num)) return;
            bool showInfo = CommonHandlers.GetBoolArg(args, 2, false);
            RunHeroAction(hero, "ChangeMaxAttri", $"{id}, {num}, {showInfo}", () => hero.ChangeMaxAttri(id, num, showInfo));
        }

        private static void HeroCallFucChangeMaxFightSkill(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "ChangeMaxFightSkill", "技能ID", out int id)) return;
            if (!CommonHandlers.TryGetIntArg(args, 1, "ChangeMaxFightSkill", "变化量", out int num)) return;
            bool showInfo = CommonHandlers.GetBoolArg(args, 2, false);
            RunHeroAction(hero, "ChangeMaxFightSkill", $"{id}, {num}, {showInfo}", () => hero.ChangeMaxFightSkill(id, num, showInfo));
        }

        private static void HeroCallFucChangeMaxLivingSkill(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "ChangeMaxLivingSkill", "技能ID", out int id)) return;
            if (!CommonHandlers.TryGetIntArg(args, 1, "ChangeMaxLivingSkill", "变化量", out int num)) return;
            bool showInfo = CommonHandlers.GetBoolArg(args, 2, false);
            RunHeroAction(hero, "ChangeMaxLivingSkill", $"{id}, {num}, {showInfo}", () => hero.ChangeMaxLivingSkill(id, num, showInfo));
        }

        private static void HeroCallFucChangeSelfHouseTotalAdd(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetFloatArg(args, 0, "ChangeSelfHouseTotalAdd", "变化量", out float delta)) return;
            RunHeroAction(hero, "ChangeSelfHouseTotalAdd", $"{delta}", () => hero.ChangeSelfHouseTotalAdd(delta));
        }

        /// <summary>
        /// ChangeGovernContribution=数量-是否提示
        /// 默认: 数量=0, 提示=true
        /// </summary>
        private static void HeroCallFucChangeGovernContribution(PlotController pc, HeroData hero, string[] args)
        {
            float num = args.Length > 0 && float.TryParse(args[0], out float n) ? n : 0f;
            bool showInfo = CommonHandlers.GetBoolArg(args, 1, true);
            try
            {
                hero.ChangeGovernContribution(num, showInfo);
                LoggerManager.Debug($"  HeroDataCallFuc: {hero.heroName}.ChangeGovernContribution({num}, {showInfo})");
            }
            catch (Exception e) { LoggerManager.Error($"  HeroDataCallFuc ChangeGovernContribution 失败: {e.Message}"); }
        }

        /// <summary>
        /// ChangeGovernLv=等级
        /// 默认: 等级=0
        /// </summary>
        private static void HeroCallFucChangeGovernLv(PlotController pc, HeroData hero, string[] args)
        {
            int num = args.Length > 0 && int.TryParse(args[0], out int n) ? n : 0;
            try
            {
                hero.ChangeGovernLv(num);
                LoggerManager.Debug($"  HeroDataCallFuc: {hero.heroName}.ChangeGovernLv({num})");
            }
            catch (Exception e) { LoggerManager.Error($"  HeroDataCallFuc ChangeGovernLv 失败: {e.Message}"); }
        }

        /// <summary>
        /// ChangeHornorLv=等级
        /// 默认: 等级=0
        /// </summary>
        private static void HeroCallFucChangeHornorLv(PlotController pc, HeroData hero, string[] args)
        {
            int num = args.Length > 0 && int.TryParse(args[0], out int n) ? n : 0;
            try
            {
                hero.ChangeHornorLv(num);
                LoggerManager.Debug($"  HeroDataCallFuc: {hero.heroName}.ChangeHornorLv({num})");
            }
            catch (Exception e) { LoggerManager.Error($"  HeroDataCallFuc ChangeHornorLv 失败: {e.Message}"); }
        }

        private static void HeroCallFucChangeExternalInjury(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetFloatArg(args, 0, "ChangeExternalInjury", "变化量", out float num)) return;
            bool showText = CommonHandlers.GetBoolArg(args, 1, false);
            bool gainExp = CommonHandlers.GetBoolArg(args, 2, false);
            bool extraResist = CommonHandlers.GetBoolArg(args, 3, false);
            RunHeroAction(hero, "ChangeExternalInjury", $"{num}, {showText}, {gainExp}, {extraResist}", () => hero.ChangeExternalInjury(num, showText, gainExp, extraResist));
        }

        private static void HeroCallFucChangeInternalInjury(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetFloatArg(args, 0, "ChangeInternalInjury", "变化量", out float num)) return;
            bool showText = CommonHandlers.GetBoolArg(args, 1, false);
            bool gainExp = CommonHandlers.GetBoolArg(args, 2, false);
            bool extraResist = CommonHandlers.GetBoolArg(args, 3, false);
            RunHeroAction(hero, "ChangeInternalInjury", $"{num}, {showText}, {gainExp}, {extraResist}", () => hero.ChangeInternalInjury(num, showText, gainExp, extraResist));
        }

        private static void HeroCallFucChangePoisonInjury(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetFloatArg(args, 0, "ChangePoisonInjury", "变化量", out float num)) return;
            bool showText = CommonHandlers.GetBoolArg(args, 1, false);
            bool gainExp = CommonHandlers.GetBoolArg(args, 2, false);
            bool extraResist = CommonHandlers.GetBoolArg(args, 3, false);
            RunHeroAction(hero, "ChangePoisonInjury", $"{num}, {showText}, {gainExp}, {extraResist}", () => hero.ChangePoisonInjury(num, showText, gainExp, extraResist));
        }

        private static void HeroCallFucChangeRandomInjury(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetFloatArg(args, 0, "ChangeRandomInjury", "数量", out float num)) return;
            bool showText = CommonHandlers.GetBoolArg(args, 1, false);
            bool gainExp = CommonHandlers.GetBoolArg(args, 2, false);
            RunHeroAction(hero, "ChangeRandomInjury", $"{num}, {showText}, {gainExp}", () => hero.ChangeRandomInjury(num, showText, gainExp));
        }

        /// <summary>
        /// AutoFightQuickChangeState=恢复倍率-伤势倍率-是否提示
        /// 默认: 伤势倍率=1, 提示=false
        /// </summary>
        private static void HeroCallFucAutoFightQuickChangeState(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetFloatArg(args, 0, "AutoFightQuickChangeState", "恢复倍率", out float rate)) return;
            float injuryRate = CommonHandlers.GetFloatArg(args, 1, 1f);
            bool showInfo = CommonHandlers.GetBoolArg(args, 2, false);
            RunHeroAction(hero, "AutoFightQuickChangeState", $"{rate}, {injuryRate}, {showInfo}", () => hero.AutoFightQuickChangeState(rate, injuryRate, showInfo));
        }

        private static void HeroCallFucAddBuff(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "AddBuff", "BuffID", out int id)) return;
            if (!CommonHandlers.TryGetFloatArg(args, 1, "AddBuff", "持续时间", out float time)) return;
            RunHeroAction(hero, "AddBuff", $"{id}, {time}", () => hero.AddBuff(id, time));
        }

        private static void HeroCallFucDisUnderstandTag(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "DisUnderstandTag", "TAG-ID", out int id)) return;
            RunHeroAction(hero, "DisUnderstandTag", $"{id}", () => hero.DisUnderstandTag(id));
        }

        private static void HeroCallFucClearAllTempTag(PlotController pc, HeroData hero, string[] args)
        {
            RunHeroAction(hero, "ClearAllTempTag", "", () => hero.ClearAllTempTag());
        }

        private static void HeroCallFucSetSkin(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "SetSkin", "皮肤ID", out int skinID)) return;
            if (!CommonHandlers.TryGetIntArg(args, 1, "SetSkin", "皮肤等级", out int skinLv)) return;
            RunHeroAction(hero, "SetSkin", $"{skinID}, {skinLv}", () => hero.SetSkin(skinID, skinLv));
        }

        private static void HeroCallFucResetDefaultSkin(PlotController pc, HeroData hero, string[] args)
        {
            RunHeroAction(hero, "ResetDefaultSkin", "", () => hero.ResetDefaultSkin());
        }

        private static void HeroCallFucRandomFaceData(PlotController pc, HeroData hero, string[] args)
        {
            bool includeNoRandom = CommonHandlers.GetBoolArg(args, 0, false);
            RunHeroAction(hero, "RandomFaceData", $"{includeNoRandom}", () => hero.RandomFaceData(includeNoRandom));
        }

        private static void HeroCallFucSetHeroForceLv(PlotController pc, HeroData hero, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "SetHeroForceLv", "势力等级", out int forceLv)) return;
            RunHeroAction(hero, "SetHeroForceLv", $"{forceLv}", () => hero.SetHeroForceLv(forceLv));
        }

        private static void HeroCallFucClearContributionRecord(PlotController pc, HeroData hero, string[] args)
        {
            RunHeroAction(hero, "ClearContributionRecord", "", () => hero.ClearContributionRecord());
        }

        private static void HeroCallFucCheckOutForceContribution(PlotController pc, HeroData hero, string[] args)
        {
            RunHeroAction(hero, "CheckOutForceContribution", "", () => hero.CheckOutForceContribution());
        }

        private static void HeroCallFucCheckHeroFameForceLv(PlotController pc, HeroData hero, string[] args)
        {
            RunHeroAction(hero, "CheckHeroFameForceLv", "", () => hero.CheckHeroFameForceLv());
        }

        private static void HeroCallFucRefreshHeroSalaryAndPopulation(PlotController pc, HeroData hero, string[] args)
        {
            RunHeroAction(hero, "RefreshHeroSalaryAndPopulation", "", () => hero.RefreshHeroSalaryAndPopulation());
        }

        private static void HeroCallFucRefreshMaxAttriAndSkill(PlotController pc, HeroData hero, string[] args)
        {
            RunHeroAction(hero, "RefreshMaxAttriAndSkill", "", () => hero.RefreshMaxAttriAndSkill());
        }

        private static void HeroCallFucResetAutoSetting(PlotController pc, HeroData hero, string[] args)
        {
            RunHeroAction(hero, "ResetAutoSetting", "", () => hero.ResetAutoSetting());
        }

        private static void HeroCallFucAutoGetFightExp(PlotController pc, HeroData hero, string[] args)
        {
            if (args != null && args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                if (!float.TryParse(args[0], out float rate))
                {
                    LoggerManager.Warning($"  HeroDataCallFuc AutoGetFightExp: 经验倍率无效 \"{args[0]}\"");
                    return;
                }

                RunHeroAction(hero, "AutoGetFightExp", $"{rate}", () => hero.AutoGetFightExp(rate));
                return;
            }

            RunHeroAction(hero, "AutoGetFightExp", "", () => hero.AutoGetFightExp());
        }

        private static void HeroCallFucFightReset(PlotController pc, HeroData hero, string[] args)
        {
            RunHeroAction(hero, "FightReset", "", () => hero.FightReset());
        }

        private static void HeroCallFucManageAIInPrison(PlotController pc, HeroData hero, string[] args)
        {
            bool showInfo = CommonHandlers.GetBoolArg(args, 0, false);
            RunHeroAction(hero, "ManageAIInPrison", $"{showInfo}", () => hero.ManageAIInPrison(showInfo));
        }

        private static void HeroCallFucLoseAllItem(PlotController pc, HeroData hero, string[] args)
        {
            RunHeroAction(hero, "LoseAllItem", "", () => hero.LoseAllItem());
        }

        private static void HeroCallFucLoseAllSkill(PlotController pc, HeroData hero, string[] args)
        {
            RunHeroAction(hero, "LoseAllSkill", "", () => hero.LoseAllSkill());
        }

        private static void HeroCallFucRandomBigMapMovePos(PlotController pc, HeroData hero, string[] args)
        {
            RunHeroAction(hero, "RandomBigMapMovePos", "", () => hero.RandomBigMapMovePos());
        }

        /// <summary>
        /// SetLover=目标角色ID-是否提示
        /// 默认: 目标角色ID=空, 提示=false
        /// </summary>
        private static void HeroCallFucSetLover(PlotController pc, HeroData hero, string[] args)
        {
            if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                LoggerManager.Warning("  HeroDataCallFuc SetLover: 参数不足，格式[SetLover=目标角色ID-是否提示]");
                return;
            }
            HeroData target = CommonHandlers.ResolveHeroId(pc, args[0]);
            if (target == null)
            {
                LoggerManager.Warning($"  HeroDataCallFuc SetLover: 未找到目标角色 \"{args[0]}\"");
                return;
            }
            bool showInfo = CommonHandlers.GetBoolArg(args, 1, false);
            try
            {
                hero.SetLover(target.heroID, showInfo);
                LoggerManager.Debug($"  HeroDataCallFuc: {hero.heroName}.SetLover({target.heroID}, {showInfo})");
            }
            catch (Exception e) { LoggerManager.Error($"  HeroDataCallFuc SetLover 失败: {e.Message}"); }
        }

        /// <summary>
        /// RemoveLover=是否提示
        /// 默认: 提示=false
        /// </summary>
        private static void HeroCallFucRemoveLover(PlotController pc, HeroData hero, string[] args)
        {
            bool showInfo = CommonHandlers.GetBoolArg(args, 0, false);
            try
            {
                hero.RemoveLover(showInfo);
                LoggerManager.Debug($"  HeroDataCallFuc: {hero.heroName}.RemoveLover({showInfo})");
            }
            catch (Exception e) { LoggerManager.Error($"  HeroDataCallFuc RemoveLover 失败: {e.Message}"); }
        }

        /// <summary>
        /// RemoveTeacher=是否提示
        /// 默认: 提示=false
        /// </summary>
        private static void HeroCallFucRemoveTeacher(PlotController pc, HeroData hero, string[] args)
        {
            bool showInfo = CommonHandlers.GetBoolArg(args, 0, false);
            try
            {
                hero.RemoveTeacher(showInfo);
                LoggerManager.Debug($"  HeroDataCallFuc: {hero.heroName}.RemoveTeacher({showInfo})");
            }
            catch (Exception e) { LoggerManager.Error($"  HeroDataCallFuc RemoveTeacher 失败: {e.Message}"); }
        }

        /// <summary>
        /// RemoveAllPrelover=是否提示
        /// 默认: 提示=false
        /// </summary>
        private static void HeroCallFucRemoveAllPrelover(PlotController pc, HeroData hero, string[] args)
        {
            bool showInfo = CommonHandlers.GetBoolArg(args, 0, false);
            try
            {
                hero.RemoveAllPrelover(showInfo);
                LoggerManager.Debug($"  HeroDataCallFuc: {hero.heroName}.RemoveAllPrelover({showInfo})");
            }
            catch (Exception e) { LoggerManager.Error($"  HeroDataCallFuc RemoveAllPrelover 失败: {e.Message}"); }
        }

        /// <summary>
        /// RemoveAllStudent=是否提示
        /// 默认: 提示=false
        /// </summary>
        private static void HeroCallFucRemoveAllStudent(PlotController pc, HeroData hero, string[] args)
        {
            bool showInfo = CommonHandlers.GetBoolArg(args, 0, false);
            try
            {
                hero.RemoveAllStudent(showInfo);
                LoggerManager.Debug($"  HeroDataCallFuc: {hero.heroName}.RemoveAllStudent({showInfo})");
            }
            catch (Exception e) { LoggerManager.Error($"  HeroDataCallFuc RemoveAllStudent 失败: {e.Message}"); }
        }

        /// <summary>
        /// FullRecover=是否提示
        /// 默认: 提示=false
        /// </summary>
        private static void HeroCallFucFullRecover(PlotController pc, HeroData hero, string[] args)
        {
            bool showInfo = CommonHandlers.GetBoolArg(args, 0, false);
            try
            {
                hero.FullRecover(showInfo);
                LoggerManager.Debug($"  HeroDataCallFuc: {hero.heroName}.FullRecover({showInfo})");
            }
            catch (Exception e) { LoggerManager.Error($"  HeroDataCallFuc FullRecover 失败: {e.Message}"); }
        }

        /// <summary>
        /// AddTag=标签ID-时间-来源-提示
        /// 默认: 标签ID=0, 时间=-1, 来源=null, 提示=false
        /// </summary>
        private static void HeroCallFucAddTag(PlotController pc, HeroData hero, string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int tagId))
            {
                LoggerManager.Warning("  HeroDataCallFuc AddTag: 参数不足或无效，格式[AddTag=标签ID-时间-来源-提示]");
                return;
            }
            float time = args.Length > 1 && float.TryParse(args[1], out float t) ? t : -1f;
            string source = args.Length > 2 ? args[2] : null;
            if (string.IsNullOrEmpty(source) || string.Equals(source, "null", StringComparison.OrdinalIgnoreCase)) source = null;
            bool showInfo = CommonHandlers.GetBoolArg(args, 3, false);
            try
            {
                hero.AddTag(tagId, time, source, showInfo);
                LoggerManager.Debug($"  HeroDataCallFuc: {hero.heroName}.AddTag({tagId}, {time}, {source}, {showInfo})");
            }
            catch (Exception e) { LoggerManager.Error($"  HeroDataCallFuc AddTag 失败: {e.Message}"); }
        }

        /// <summary>
        /// RemoveTag=标签ID-是否提示
        /// 默认: 标签ID=0, 提示=false
        /// </summary>
        private static void HeroCallFucRemoveTag(PlotController pc, HeroData hero, string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int tagId))
            {
                LoggerManager.Warning("  HeroDataCallFuc RemoveTag: 参数不足或无效，格式[RemoveTag=标签ID-是否提示]");
                return;
            }
            bool showInfo = CommonHandlers.GetBoolArg(args, 1, false);
            try
            {
                hero.RemoveTag(tagId, showInfo);
                LoggerManager.Debug($"  HeroDataCallFuc: {hero.heroName}.RemoveTag({tagId}, {showInfo})");
            }
            catch (Exception e) { LoggerManager.Error($"  HeroDataCallFuc RemoveTag 失败: {e.Message}"); }
        }

        /// <summary>
        /// AddLog=日志内容
        /// </summary>
        private static void HeroCallFucAddLog(PlotController pc, HeroData hero, string[] args)
        {
            if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                LoggerManager.Warning("  HeroDataCallFuc AddLog: 参数不足，格式[AddLog=日志内容]");
                return;
            }
            try
            {
                hero.AddLog(args[0]);
                LoggerManager.Debug($"  HeroDataCallFuc: {hero.heroName}.AddLog(\"{args[0]}\")");
            }
            catch (Exception e) { LoggerManager.Error($"  HeroDataCallFuc AddLog 失败: {e.Message}"); }
        }

        /// <summary>
        /// 推荐格式: SetCustomValue#属性名=属性值
        /// 兼容格式: SetCustomValue=属性名-属性值
        /// 属性值为空时删除对应 key。
        /// </summary>
        private static void HeroCallFucSetCustomValue(PlotController pc, HeroData hero, string[] args)
        {
            if (hero == null)
                return;

            if (!TryParseCustomValueArgs(args, out string propertyName, out string value))
            {
                LoggerManager.Warning("  HeroDataCallFuc SetCustomValue: 参数不足，格式[SetCustomValue#属性名=属性值]或[SetCustomValue=属性名-属性值]");
                return;
            }

            string objectID = hero.heroID.ToString();
            string key = CustomValueManager.GetKey("HeroData", objectID, propertyName);
            if (string.IsNullOrEmpty(key))
            {
                LoggerManager.Warning($"  HeroDataCallFuc SetCustomValue: 属性名无效 \"{propertyName}\"");
                return;
            }

            if (!CustomValueManager.SetRaw("HeroData", objectID, propertyName, value))
            {
                LoggerManager.Error($"  HeroDataCallFuc SetCustomValue: PlotEventLogData实例不存在，无法写入 {key}");
                return;
            }

            if (string.IsNullOrEmpty(value))
                LoggerManager.Debug($"  HeroDataCallFuc: 已删除自定义变量 {key}");
            else
                LoggerManager.Debug($"  HeroDataCallFuc: 已设置自定义变量 {key}={value}");
        }

        private static bool TryParseCustomValueArgs(string[] args, out string propertyName, out string value)
        {
            propertyName = "";
            value = "";

            if (args == null || args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
                return false;

            int eqIdx = args[0].IndexOf('=');
            if (eqIdx >= 0)
            {
                propertyName = args[0].Substring(0, eqIdx).Trim();
                value = args[0].Substring(eqIdx + 1);
                return !string.IsNullOrWhiteSpace(propertyName);
            }

            if (args.Length < 2)
                return false;

            propertyName = args[0].Trim();
            value = JoinArgs(args, 1, "-");
            return !string.IsNullOrWhiteSpace(propertyName);
        }

        private static string JoinArgs(string[] args, int startIndex, string separator)
        {
            if (args == null || startIndex >= args.Length)
                return "";

            string result = args[startIndex] ?? "";
            for (int i = startIndex + 1; i < args.Length; i++)
            {
                result += separator + (args[i] ?? "");
            }

            return result;
        }
    }
}


