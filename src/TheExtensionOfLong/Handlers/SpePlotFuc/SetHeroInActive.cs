using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 设置角色不活跃状态。
    /// 格式: SetHeroInActive*角色ID/角色#状态值
    ///   状态值: 1/true=不活跃，0/false=活跃；设置为活跃时会删除对应key。
    /// </summary>
    [SpePlotFuc("SetHeroInActive")]
    public static class SpePlotFucSetHeroInActive
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 2)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*角色ID/角色#状态值(1/true=不活跃,0/false=活跃)]");
                return;
            }

            HeroData hero = CommonHandlers.ResolveHeroId(plotController, fucParams[0]);
            if (hero == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到角色 \"{fucParams[0]}\"");
                return;
            }

            if (!HeroInActiveManager.TryParseState(fucParams[1], out bool inActive))
            {
                LoggerManager.Warning($"{fucName}: 状态值无效 \"{fucParams[1]}\"，仅支持 1/true/0/false");
                return;
            }

            if (!HeroInActiveManager.SetInActive(hero, inActive))
            {
                LoggerManager.Error($"{fucName}: PlotEventLogData实例不存在，无法设置角色不活跃状态");
                return;
            }

            LoggerManager.Debug($"{fucName}: 已设置 {hero.heroName}(ID={hero.heroID}) inActive={(inActive ? "1" : "0")}");
        }
    }
}
