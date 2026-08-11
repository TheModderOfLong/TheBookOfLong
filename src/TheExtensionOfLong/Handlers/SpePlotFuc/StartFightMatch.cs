using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppSystem.Collections.Generic;
using System.Reflection;
using System;

namespace TheExtensionOfLong
{
    [SpePlotFuc("StartFightMatch")]
    public static class SpePlotFucStartFightMatch
    {
        /// <summary>
        /// 启动比武/辩论大会
        /// 格式: StartFightMatch*比赛类型#选择范围#额外角色列表#最大参赛人数#筛选条件表达式#选取规则#观战类型#结束时执行函数#结束时执行参数#难度系数#是否门派比武(可选)#是否为门派赛(可选)#奖励类型(可选)#人数不足时调用函数(可选)#人数不足时调用参数(可选)
        ///
        /// 比赛类型: 对应FightMatchType，支持int(0=BattleMatch,1=DebateMatch)和string(BattleMatch/DebateMatch)
        /// 选择范围: 0或空=全部角色+额外角色, 1=剧情互动角色+额外角色, 2=仅额外角色
        /// 额外角色列表: 以"-"分隔的角色ID或名称，如"0-1-2-3"或"小白-小红"
        /// 最大参赛人数: 筛选后超出则按选取规则截取，默认32
        /// 筛选条件表达式: 参照ChooseHero的条件表达式，对角色范围逐个筛选；空则不筛选
        /// 选取规则: 0或空=随机选取, 1=按实力高低选取；超出最大参赛人数时额外角色优先，不足再按此规则从其他角色补充
        /// 观战类型: 对应WatchFightType，支持int(0=NoWatchFight,1=AskWatchFight)和string
        /// 结束时执行函数: 比赛结束后通过SendMessage调用的函数名
        /// 结束时执行参数: SendMessage的附加参数，为空则不带参调用
        /// 难度系数: 对应_difficulty，影响奖励
        /// 是否门派比武: 对应_isForceMatch，默认false
        /// 是否为门派赛: 对应isForceGroupMatch，默认false，为true时以门派为单位比赛
        /// 奖励类型: 0或空=随机生成奖励, 1=从tempPlotShop获取特定奖励
        /// 人数不足时的调用函数: 参赛人数不足时以SendMessage调用此函数
        /// 人数不足时的调用参数: SendMessage的参数
        ///
        /// 示例: StartFightMatch*0#0##8#{{[$HeroData:dead$][=]0}}#0#0#EndMatch##1.0
        ///       StartFightMatch*0#1#小白-小红#4#{{[$HeroData:heroForceLv$][<=]3}}#1#1#EndMatch#param1#2.0#true#false#0#SomeEmptyFunc#param1
        /// </summary>
        public static void TryCall(PlotController __instance, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 10)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*比赛类型#选择范围#额外角色列表#最大参赛人数#筛选条件表达式#选取规则#观战类型#结束时执行函数#结束时执行参数#难度系数#是否门派比武(可选)#是否为门派赛(可选)#奖励类型(可选)#人数不足时调用函数(可选)#人数不足时调用参数(可选)]");
                return;
            }

            // ---- 解析必选参数 ----

            // 比赛类型 (FightMatchType)：支持int或枚举名
            FightMatchType matchType;
            string matchTypeStr = fucParams[0];
            if (int.TryParse(matchTypeStr, out int matchTypeInt))
            {
                matchType = (FightMatchType)matchTypeInt;
            }
            else if (!System.Enum.TryParse<FightMatchType>(matchTypeStr, out matchType))
            {
                LoggerManager.Warning($"{fucName}: 无法识别的比赛类型 \"{matchTypeStr}\"");
                return;
            }

            // 选择范围
            int scope = 0;
            if (!string.IsNullOrWhiteSpace(fucParams[1]))
            {
                if (!int.TryParse(fucParams[1], out scope)) scope = 0;
            }

            // 额外角色列表
            List<HeroData> extraHeroes = new List<HeroData>();
            if (!string.IsNullOrWhiteSpace(fucParams[2]))
            {
                string[] heroRefs = fucParams[2].Split('-');
                for (int i = 0; i < heroRefs.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(heroRefs[i])) continue;
                    HeroData hero = CommonHandlers.ResolveHeroId(__instance, heroRefs[i].Trim(), null);
                    if (hero != null)
                    {
                        extraHeroes.Add(hero);
                    }
                    else
                    {
                        LoggerManager.Warning($"{fucName}: 额外角色列表中未找到角色 \"{heroRefs[i].Trim()}\"");
                    }
                }
            }

            // 最大参赛人数
            int maxCount = 32;
            if (!string.IsNullOrWhiteSpace(fucParams[3]))
            {
                if (!int.TryParse(fucParams[3], out maxCount) || maxCount < 1)
                {
                    maxCount = 32;
                }
            }

            // 筛选条件表达式
            string conditionExpression = fucParams[4];

            // 选取规则: -1或空=随机, 0~6对应HeroListSortType枚举，也支持枚举名（如"FightScoreMax"）
            int selectionRule = -1;
            if (!string.IsNullOrWhiteSpace(fucParams[5]))
            {
                if (int.TryParse(fucParams[5], out selectionRule))
                {
                    // int解析成功，直接使用
                }
                else if (System.Enum.TryParse<HeroListSortType>(fucParams[5], out HeroListSortType sortType))
                {
                    selectionRule = (int)sortType;
                }
                else
                {
                    LoggerManager.Warning($"{fucName}: 无法识别的选取规则 \"{fucParams[5]}\"");
                    selectionRule = -1;
                }
            }

            // 观战类型 (WatchFightType)：支持int或枚举名
            WatchFightType watchType;
            string watchTypeStr = fucParams[6];
            if (int.TryParse(watchTypeStr, out int watchTypeInt))
            {
                watchType = (WatchFightType)watchTypeInt;
            }
            else if (!System.Enum.TryParse<WatchFightType>(watchTypeStr, out watchType))
            {
                LoggerManager.Warning($"{fucName}: 无法识别的观战类型 \"{watchTypeStr}\"");
                return;
            }

            // 结束时执行函数
            string endMatchCallFuc = fucParams[7];
            // 结束时执行参数
            string endMatchCallParam = fucParams[8];
            // 组合 endMatchCallPlot: "函数名;参数" 或 仅"函数名"
            string endMatchCallPlot;
            if (!string.IsNullOrWhiteSpace(endMatchCallParam))
                endMatchCallPlot = endMatchCallFuc + ";" + endMatchCallParam;
            else
                endMatchCallPlot = endMatchCallFuc;

            // 难度系数
            float difficulty = 1f;
            if (!string.IsNullOrWhiteSpace(fucParams[9]))
            {
                if (!float.TryParse(fucParams[9], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out difficulty))
                {
                    difficulty = 1f;
                }
            }

            // ---- 解析可选参数 ----

            // 是否门派比武
            bool isForceMatch = false;
            if (fucParams.Length > 10 && !string.IsNullOrWhiteSpace(fucParams[10]))
            {
                isForceMatch = fucParams[10] == "true" || fucParams[10] == "1";
            }

            // 是否为门派赛
            bool isForceGroupMatch = false;
            if (fucParams.Length > 11 && !string.IsNullOrWhiteSpace(fucParams[11]))
            {
                isForceGroupMatch = fucParams[11] == "true" || fucParams[11] == "1";
            }

            // 奖励类型
            int rewardType = 0;
            if (fucParams.Length > 12 && !string.IsNullOrWhiteSpace(fucParams[12]))
            {
                int.TryParse(fucParams[12], out rewardType);
            }

            // 人数不足时的调用函数
            string emptyCallFuc = fucParams.Length > 13 && !string.IsNullOrWhiteSpace(fucParams[13]) ? fucParams[13] : null;
            // 人数不足时的调用参数
            string emptyCallParam = fucParams.Length > 14 && !string.IsNullOrWhiteSpace(fucParams[14]) ? fucParams[14] : null;

            // ---- 构建候选角色范围 ----

            List<HeroData> candidateList = new List<HeroData>();
            HashSet<int> addedHeroIDs = new HashSet<int>();

            if (scope == 2)
            {
                // 仅额外角色列表
                foreach (var hero in extraHeroes)
                {
                    if (hero != null && addedHeroIDs.Add(hero.heroID))
                    {
                        candidateList.Add(hero);
                    }
                }
            }
            else
            {
                // scope 0: 全部角色列表; scope 1: 剧情互动角色列表
                if (scope == 1)
                {
                    List<HeroData> plotHeroList = __instance.plotInteractHeroList;
                    if (plotHeroList != null)
                    {
                        foreach (var hero in plotHeroList)
                        {
                            if (hero != null && addedHeroIDs.Add(hero.heroID))
                            {
                                candidateList.Add(hero);
                            }
                        }
                    }
                }
                else
                {
                    // scope 0: 全部角色
                    WorldData worldData = CommonHandlers.GetWorldData();
                    if (worldData == null || worldData.Heros == null)
                    {
                        LoggerManager.Error($"{fucName}: WorldData或Heros为空");
                        return;
                    }
                    var heros = worldData.Heros;
                    for (int i = 0; i < heros.Count; i++)
                    {
                        HeroData hero = heros[i];
                        if (hero != null && addedHeroIDs.Add(hero.heroID))
                        {
                            candidateList.Add(hero);
                        }
                    }
                }

                // scope 0或1时，额外包含额外角色列表的有效角色
                foreach (var hero in extraHeroes)
                {
                    if (hero != null && addedHeroIDs.Add(hero.heroID))
                    {
                        candidateList.Add(hero);
                    }
                }
            }

            LoggerManager.Debug($"{fucName}: 角色范围(scope={scope})共{candidateList.Count}个候选角色");

            // ---- 按筛选条件表达式筛选 ----

            if (!string.IsNullOrWhiteSpace(conditionExpression))
            {
                List<HeroData> filteredList = new List<HeroData>();
                HeroData originalTargetHero = __instance.targetInteractHero;

                try
                {
                    for (int i = 0; i < candidateList.Count; i++)
                    {
                        HeroData hero = candidateList[i];
                        if (hero == null) continue;

                        __instance.targetInteractHero = hero;

                        try
                        {
                            LoggerManager.Debug($"{fucName}: 条件筛选评估角色: {hero.heroName}");
                            bool match = ConditionExpressionEvaluator.Evaluate(__instance, conditionExpression);
                            if (match)
                            {
                                filteredList.Add(hero);
                            }
                        }
                        catch (System.Exception e)
                        {
                            LoggerManager.Warning($"{fucName}: 评估角色 {hero.heroName} 条件时出错: {e.Message}");
                        }
                    }
                }
                finally
                {
                    __instance.targetInteractHero = originalTargetHero;
                }

                candidateList = filteredList;
                LoggerManager.Debug($"{fucName}: 条件筛选后共{candidateList.Count}个角色");
            }

            // ---- 人数不足处理 ----

            if (candidateList.Count < 1)
            {
                LoggerManager.Debug($"{fucName}: 无符合条件的角色，不开展比赛");

                if (!string.IsNullOrWhiteSpace(emptyCallFuc))
                {
                    if (!string.IsNullOrWhiteSpace(emptyCallParam))
                    {
                        __instance.gameObject.SendMessage(emptyCallFuc, emptyCallParam);
                        LoggerManager.Debug($"{fucName}: 人数不足，SendMessage(\"{emptyCallFuc}\", \"{emptyCallParam}\")");
                    }
                    else
                    {
                        __instance.gameObject.SendMessage(emptyCallFuc);
                        LoggerManager.Debug($"{fucName}: 人数不足，SendMessage(\"{emptyCallFuc}\")");
                    }
                }
                return;
            }

            // ---- 超出最大参赛人数时选取（额外角色优先，不足再按选取规则补充） ----

            if (candidateList.Count > maxCount)
            {
                // 构建额外角色ID集合（用于优先选取）
                HashSet<int> extraHeroIDs = new HashSet<int>();
                foreach (var hero in extraHeroes)
                {
                    if (hero != null) extraHeroIDs.Add(hero.heroID);
                }

                List<HeroData> priorityList = new List<HeroData>();   // 额外角色列表中的角色
                List<HeroData> otherList = new List<HeroData>();       // 其他角色

                foreach (var hero in candidateList)
                {
                    if (extraHeroIDs.Contains(hero.heroID))
                        priorityList.Add(hero);
                    else
                        otherList.Add(hero);
                }

                // 先按选取规则从额外角色中选取
                int extraTake = UnityEngine.Mathf.Min(priorityList.Count, maxCount);
                List<HeroData> finalList = CommonHandlers.SelectHeroesByRule(priorityList, extraTake, selectionRule);

                // 不足再按选取规则从其他角色中补充
                int remaining = maxCount - finalList.Count;
                if (remaining > 0 && otherList.Count > 0)
                {
                    List<HeroData> supplemented = CommonHandlers.SelectHeroesByRule(otherList, remaining, selectionRule);
                    for (int i = 0; i < supplemented.Count; i++)
                        finalList.Add(supplemented[i]);
                }

                candidateList = finalList;
                string ruleName = selectionRule >= 0 ? $"HeroListSortType.{(HeroListSortType)selectionRule}" : "随机";
                LoggerManager.Debug($"{fucName}: 优先{ruleName}选取{extraTake}个额外角色，{ruleName}补充{remaining}个其他角色，共{candidateList.Count}个参赛");
            }

            {
                string[] names = new string[candidateList.Count];
                for (int i = 0; i < candidateList.Count; i++)
                    names[i] = candidateList[i].heroName;
                LoggerManager.Debug($"{fucName}: 最终参赛角色({candidateList.Count}): {string.Join(", ", names)}");
            }

            // ---- 隐藏交互UI ----

            // __instance.HideInteractUI();

            // ---- 处理奖励列表 ----

            bool generateReward = true;
            List<ItemData> rewardList = null;

            if (rewardType == 1)
            {
                // 特定奖励：从tempPlotShop获取
                ItemListData tempPlotShop = __instance.tempPlotShop;
                if (tempPlotShop != null && tempPlotShop.allItem != null && tempPlotShop.allItem.Count > 0)
                {
                    rewardList = tempPlotShop.allItem;
                }
                else
                {
                    LoggerManager.Warning($"{fucName}: 奖励类型=1但tempPlotShop为空或无物品，将使用随机奖励");
                }
            }

            // ---- 启动比赛 ----

            FightMatchController fightMatchController = FightMatchController._instance;
            if (fightMatchController == null)
            {
                LoggerManager.Error($"{fucName}: FightMatchController实例不存在");
                return;
            }

            fightMatchController.RestartFightMatch(
                matchType,
                candidateList,
                watchType,
                endMatchCallPlot,
                difficulty,
                isForceMatch,
                generateReward,
                rewardList,
                isForceGroupMatch
            );

            LoggerManager.Debug($"{fucName}: 比赛已启动 type={matchType} difficulty={difficulty} isForceMatch={isForceMatch} isForceGroupMatch={isForceGroupMatch} rewardType={rewardType}");
        }
    }
}
