using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 根据关系类型批量改变玩家与该关系所有角色的好感度
    /// 格式: ChangeFavorByRelation*关系#改变好感度#最大好感度(可选,默认100)#门派倍率(可选,默认0)#是否播放音效(可选,默认False)
    ///   关系关键字: 夫妻/Lover, 情侣/PreLover, 朋友/Friend, 结义/Brother, 徒弟/Student, 师父/师傅/Teacher, 亲属/Relative, 仇敌/Hater
    /// 示例: ChangeFavorByRelation*PreLover#-25          → 所有情侣好感-25
    ///       ChangeFavorByRelation*结义#-10#100#0#True  → 所有结义兄弟好感-10，播放音效
    ///       ChangeFavorByRelation*Lover#-25            → 配偶好感-25
    /// </summary>
    [SpePlotFuc("ChangeFavorByRelation")]
    public static class SpePlotFucChangeFavorByRelation
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 2)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*关系#改变好感度#最大好感度(可选,默认100)#门派倍率(可选,默认0)#是否播放音效(可选,默认False)]");
                return;
            }

            string relation = fucParams[0].Trim();

            float favorChange;
            if (!float.TryParse(fucParams[1], out favorChange))
            {
                LoggerManager.Warning($"{fucName}: 改变好感度参数无效: {fucParams[1]}");
                return;
            }

            float maxFavor = 100f;
            if (fucParams.Length > 2 && !string.IsNullOrWhiteSpace(fucParams[2]))
            {
                if (!float.TryParse(fucParams[2], out maxFavor))
                {
                    LoggerManager.Warning($"{fucName}: 最大好感度参数无效: {fucParams[2]}");
                    return;
                }
            }

            float forceRate = 0f;
            if (fucParams.Length > 3 && !string.IsNullOrWhiteSpace(fucParams[3]))
            {
                if (!float.TryParse(fucParams[3], out forceRate))
                {
                    LoggerManager.Warning($"{fucName}: 门派倍率参数无效: {fucParams[3]}");
                    return;
                }
            }

            bool playSound = false;
            if (fucParams.Length > 4 && !string.IsNullOrWhiteSpace(fucParams[4]))
            {
                string soundStr = fucParams[4].Trim().ToUpper();
                playSound = soundStr == "TRUE" || soundStr == "1";
            }

            // 获取玩家数据
            WorldData worldData = CommonHandlers.GetWorldData();
            if (worldData == null)
            {
                LoggerManager.Error($"{fucName}: WorldData实例不存在");
                return;
            }

            HeroData player = worldData.Player();
            if (player == null)
            {
                LoggerManager.Error($"{fucName}: 玩家角色不存在");
                return;
            }

            // 解析关系关键字，获取角色ID列表
            string lowerRelation = relation.ToLower();
            var heroIds = new System.Collections.Generic.List<int>();
            string relationName = "";

            if (lowerRelation == "夫妻" || lowerRelation == "lover")
            {
                relationName = "夫妻";
                if (player.HaveLover())
                    heroIds.Add(player.Lover);
            }
            else if (lowerRelation == "情侣" || lowerRelation == "prelover")
            {
                relationName = "情侣";
                if (player.PreLovers != null)
                {
                    for (int i = 0; i < player.PreLovers.Count; i++)
                        heroIds.Add(player.PreLovers[i]);
                }
            }
            else if (lowerRelation == "朋友" || lowerRelation == "friend")
            {
                relationName = "朋友";
                if (player.Friends != null)
                {
                    for (int i = 0; i < player.Friends.Count; i++)
                        heroIds.Add(player.Friends[i]);
                }
            }
            else if (lowerRelation == "结义" || lowerRelation == "brother")
            {
                relationName = "结义";
                if (player.Brothers != null)
                {
                    for (int i = 0; i < player.Brothers.Count; i++)
                        heroIds.Add(player.Brothers[i]);
                }
            }
            else if (lowerRelation == "徒弟" || lowerRelation == "student")
            {
                relationName = "徒弟";
                if (player.Students != null)
                {
                    for (int i = 0; i < player.Students.Count; i++)
                        heroIds.Add(player.Students[i]);
                }
            }
            else if (lowerRelation == "师父" || lowerRelation == "师傅" || lowerRelation == "teacher")
            {
                relationName = "师父";
                if (player.HaveTeacher())
                    heroIds.Add(player.Teacher);
            }
            else if (lowerRelation == "亲属" || lowerRelation == "relative")
            {
                relationName = "亲属";
                if (player.Relatives != null)
                {
                    for (int i = 0; i < player.Relatives.Count; i++)
                        heroIds.Add(player.Relatives[i]);
                }
            }
            else if (lowerRelation == "仇敌" || lowerRelation == "hater")
            {
                relationName = "仇敌";
                if (player.Haters != null)
                {
                    for (int i = 0; i < player.Haters.Count; i++)
                        heroIds.Add(player.Haters[i]);
                }
            }
            else
            {
                LoggerManager.Warning($"{fucName}: 未知关系类型 \"{relation}\"，支持: 夫妻/Lover, 情侣/PreLover, 朋友/Friend, 结义/Brother, 徒弟/Student, 师父/师傅/Teacher, 亲属/Relative, 仇敌/Hater");
                return;
            }

            if (heroIds.Count == 0)
            {
                LoggerManager.Debug($"{fucName}: 玩家没有{relationName}关系，无需改变好感度");
                return;
            }

            // 批量改变好感度
            int changedCount = 0;
            foreach (int heroId in heroIds)
            {
                HeroData hero = worldData.GetHero(heroId);
                if (hero != null)
                {
                    plotController.PlotChangeHeroFavor(hero, favorChange, maxFavor, forceRate, playSound);
                    changedCount++;
                }
            }

            LoggerManager.Debug($"{fucName}: 已为玩家{changedCount}位{relationName}改变好感度 {favorChange}");
        }
    }
}
