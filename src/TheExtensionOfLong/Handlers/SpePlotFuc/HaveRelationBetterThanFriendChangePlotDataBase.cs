using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 判断源角色与目标角色之间是否有比朋友更亲密的关系
    /// 格式: HaveRelationBetterThanFriendChangePlotDataBase*TruePlotDataBaseId-FalsePlotDataBaseId(可选)#sourceInteractHeroId(可选)#targetInteractHeroId(可选)#checkTeacher(可选,默认true)#checkBrother(可选,默认true)
    /// </summary>
    [SpePlotFuc("HaveRelationBetterThanFriendChangePlotDataBase")]
    public static class SpePlotFucHaveRelationBetterThanFriendChangePlotDataBase
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 1)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*TruePlotDataBaseId-FalsePlotDataBaseId(可选)#sourceInteractHeroId(可选)#targetInteractHeroId(可选)#checkTeacher(可选)#checkBrother(可选)]");
                return;
            }

            string[] plotParams = fucParams[0].Split('-');

            HeroData sourceHeroData = CommonHandlers.ResolveHeroId(plotController, fucParams.Length > 1 ? fucParams[1] : null, plotController.sourceInteractHero);

            HeroData targetHeroData = CommonHandlers.ResolveHeroId(plotController, fucParams.Length > 2 ? fucParams[2] : null, plotController.targetInteractHero);

            bool checkTeacher = fucParams.Length <= 3 || (fucParams[3] != "FALSE" && fucParams[3] != "0");
            bool checkBrother = fucParams.Length <= 4 || (fucParams[4] != "FALSE" && fucParams[4] != "0");

            if (sourceHeroData != null && targetHeroData != null && sourceHeroData.HaveRelationBetterThanFriend(targetHeroData.heroID, checkTeacher, checkBrother))
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
