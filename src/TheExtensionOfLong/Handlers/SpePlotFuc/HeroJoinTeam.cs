using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 让指定角色加入指定队长的队伍。
    /// 格式: HeroJoinTeam*队长角色ID/角色名称/关键字角色#角色ID/角色名称/关键字角色#自动离队时间
    /// 示例: HeroJoinTeam*0#8#-1
    /// </summary>
    [SpePlotFuc("HeroJoinTeam")]
    public static class SpePlotFucHeroJoinTeam
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 3)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*队长角色ID/角色名称/关键字角色#角色ID/角色名称/关键字角色#自动离队时间]");
                return;
            }

            HeroData teamLeader = CommonHandlers.ResolveHeroId(plotController, fucParams[0]);
            if (teamLeader == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到队长角色 \"{fucParams[0]}\"");
                return;
            }

            HeroData teamMate = CommonHandlers.ResolveHeroId(plotController, fucParams[1]);
            if (teamMate == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到入队角色 \"{fucParams[1]}\"");
                return;
            }

            if (teamLeader.heroID == teamMate.heroID)
            {
                LoggerManager.Warning($"{fucName}: 队长角色与入队角色不能相同，角色ID={teamLeader.heroID}");
                return;
            }

            if (!int.TryParse(fucParams[2], out int autoLeaveDay))
            {
                LoggerManager.Warning($"{fucName}: 自动离队时间解析失败 \"{fucParams[2]}\"");
                return;
            }

            GameController gameController = GameController.Instance;
            if (gameController == null)
            {
                LoggerManager.Error($"{fucName}: GameController实例不存在，无法让角色入队");
                return;
            }

            gameController.HeroJoinTeam(teamLeader, teamMate, autoLeaveDay);
            LoggerManager.Debug($"{fucName}: 已让 {teamMate.heroName}(ID={teamMate.heroID}) 加入 {teamLeader.heroName}(ID={teamLeader.heroID}) 的队伍，自动离队时间={autoLeaveDay}");
        }
    }
}
