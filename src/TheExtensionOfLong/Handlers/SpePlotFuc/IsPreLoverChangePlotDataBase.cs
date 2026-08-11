using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 判断目标角色是否为源角色的准恋人（PreLover）
    /// 格式: IsPreLoverChangePlotDataBase*TruePlotDataBaseId-FalsePlotDataBaseId(可选)#sourceInteractHeroId(可选)#targetInteractHeroId(可选)
    /// </summary>
    [SpePlotFuc("IsPreLoverChangePlotDataBase")]
    public static class SpePlotFucIsPreLoverChangePlotDataBase
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 1)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*TruePlotDataBaseId-FalsePlotDataBaseId(可选)#sourceInteractHeroId(可选)#targetInteractHeroId(可选)]");
                return;
            }

            string[] plotParams = fucParams[0].Split('-');
            if (plotParams.Length < 1 || string.IsNullOrWhiteSpace(plotParams[0]))
            {
                LoggerManager.Warning($"{fucName}: TruePlotDataBaseId不能为空");
                return;
            }

            HeroData sourceHeroData = CommonHandlers.ResolveHeroId(plotController, fucParams.Length > 1 ? fucParams[1] : null, plotController.sourceInteractHero);

            HeroData targetHeroData = CommonHandlers.ResolveHeroId(plotController, fucParams.Length > 2 ? fucParams[2] : null, plotController.targetInteractHero);

            if (sourceHeroData != null)
                LoggerManager.Debug($"{fucName}: sourceHeroData: {sourceHeroData.heroID}={sourceHeroData.heroName}");
            else
                LoggerManager.Debug($"{fucName}: sourceHeroData: null");

            if (targetHeroData != null)
                LoggerManager.Debug($"{fucName}: targetHeroData: {targetHeroData.heroID}={targetHeroData.heroName}");
            else
                LoggerManager.Debug($"{fucName}: targetHeroData: null");

            if (sourceHeroData != null && targetHeroData != null && sourceHeroData.HavePrelover(targetHeroData.heroID))
            {
                LoggerManager.Debug($"{fucName}: 条件符合，跳转至{plotParams[0]}");
                plotController.ChangePlotDataBase(plotParams[0]);
            }
            else if (plotParams.Length >= 2 && !string.IsNullOrWhiteSpace(plotParams[1]))
            {
                LoggerManager.Debug($"{fucName}: 条件不符，跳转至{plotParams[1]}");
                plotController.ChangePlotDataBase(plotParams[1]);
            }
            else
            {
                LoggerManager.Debug($"{fucName}: 条件不符，无FalsePlotId，不做跳转");
            }
        }
    }
}
