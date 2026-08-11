using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 判断目标角色是否为源角色的恋人
    /// 格式: IsLoverChangePlotDataBase*TruePlotDataBaseId-FalsePlotDataBaseId(可选)#sourceInteractHeroId(可选)#targetInteractHeroId(可选)
    /// </summary>
    [SpePlotFuc("IsLoverChangePlotDataBase")]
    public static class SpePlotFucIsLoverChangePlotDataBase
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 1)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*TruePlotDataBaseId-FalsePlotDataBaseId(可选)#sourceInteractHeroId(可选)#targetInteractHeroId(可选)]");
                return;
            }

            string[] plotParams = fucParams[0].Split('-');

            HeroData sourceHeroData = CommonHandlers.ResolveHeroId(plotController, fucParams.Length > 1 ? fucParams[1] : null, plotController.sourceInteractHero);

            HeroData targetHeroData = CommonHandlers.ResolveHeroId(plotController, fucParams.Length > 2 ? fucParams[2] : null, plotController.targetInteractHero);

            if (sourceHeroData != null && targetHeroData != null && (sourceHeroData.Lover == targetHeroData.heroID || targetHeroData.Lover == sourceHeroData.heroID))
            {
                plotController.ChangePlotDataBase(plotParams[0]);
            }
            else if (plotParams.Length >= 2 && !string.IsNullOrWhiteSpace(plotParams[1]))
            {
                plotController.ChangePlotDataBase(plotParams[1]);
            }
        }
    }
}
