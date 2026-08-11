using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 尝试调用"HaveLover"功能，根据角色是否有恋人执行不同的剧情分支
    /// 格式: HaveLoverChangePlotDataBase*TruePlotDataBaseId-FalsePlotDataBaseId(可选)#sourceHeroId(可选)
    /// </summary>
    [SpePlotFuc("HaveLoverChangePlotDataBase")]
    public static class SpePlotFucHaveLoverChangePlotDataBase
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 1)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*TruePlotDataBaseId-FalsePlotDataBaseId(可选)#sourceHeroId(可选)]");
                return;
            }

            string[] plotParams = fucParams[0].Split('-');

            HeroData targetHeroData = CommonHandlers.ResolveHeroId(plotController, fucParams.Length > 1 ? fucParams[1] : null, plotController.sourceInteractHero);

            if (targetHeroData != null && targetHeroData.HaveLover())
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
