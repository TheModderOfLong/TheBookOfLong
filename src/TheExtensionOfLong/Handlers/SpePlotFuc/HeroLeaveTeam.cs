using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 让指定角色离开当前队伍。
    /// 格式: HeroLeaveTeam*角色ID/角色名称/关键字角色
    /// 示例: HeroLeaveTeam*8
    /// </summary>
    [SpePlotFuc("HeroLeaveTeam")]
    public static class SpePlotFucHeroLeaveTeam
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 1)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*角色ID/角色名称/关键字角色]");
                return;
            }

            HeroData hero = CommonHandlers.ResolveHeroId(plotController, fucParams[0]);
            if (hero == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到角色 \"{fucParams[0]}\"");
                return;
            }

            GameController gameController = GameController.Instance;
            if (gameController == null)
            {
                LoggerManager.Error($"{fucName}: GameController实例不存在，无法让角色离队");
                return;
            }

            if (!hero.inTeam)
            {
                LoggerManager.Debug($"{fucName}: {hero.heroName}(ID={hero.heroID}) 当前不在队伍中，无需离队");
                return;
            }

            int oldTeamLeaderID = hero.teamLeader;
            gameController.HeroLeaveTeam(hero);
            LoggerManager.Debug($"{fucName}: 已让 {hero.heroName}(ID={hero.heroID}) 离开队伍，原队长ID={oldTeamLeaderID}");
        }
    }
}
