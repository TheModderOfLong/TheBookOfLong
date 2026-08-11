using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 设置自定义角色关系文本，文本保存在剧情事件日志中的字符串变量
    /// 格式: SetHeroRelationShipText*RelationShipText#sourceHeroId(可选)#targetHeroId(可选)
    ///   RelationShipText为空字符串时删除自定义关系文本
    /// </summary>
    [SpePlotFuc("SetHeroRelationShipText")]
    public static class SpePlotFucSetHeroRelationShipText
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 1)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*RelationShipText#sourceHeroId(可选)#targetHeroId(可选)]");
                return;
            }

            string relationShipText = fucParams[0];

            GameController instance = GameController.Instance;
            if (instance == null)
            {
                LoggerManager.Error($"{fucName}: GameController实例不存在，无法调用此指令");
                return;
            }

            WorldData worldData = instance.worldData;
            if (worldData == null)
            {
                LoggerManager.Error($"{fucName}: WorldData实例不存在，无法调用此指令");
                return;
            }

            PlotEventLogData plotEventLogData = worldData.PlotEventLog;
            if (plotEventLogData == null)
            {
                LoggerManager.Error($"{fucName}: plotEventLogData实例不存在，无法调用此指令");
                return;
            }

            HeroData sourceHeroData = CommonHandlers.ResolveHeroId(plotController, fucParams.Length > 1 ? fucParams[1] : null, plotController.sourceInteractHero);
            if (sourceHeroData == null)
            {
                LoggerManager.Warning($"{fucName}: 源角色不存在，无法设置角色关系文本");
                return;
            }

            HeroData targetHeroData = CommonHandlers.ResolveHeroId(plotController, fucParams.Length > 2 ? fucParams[2] : null, plotController.targetInteractHero);
            if (targetHeroData == null)
            {
                LoggerManager.Warning($"{fucName}: 目标角色不存在，无法设置角色关系文本");
                return;
            }

            string targetCallKey = "call_" + sourceHeroData.heroID + "_" + targetHeroData.heroID;
            if (string.IsNullOrEmpty(relationShipText))
            {
                plotEventLogData.Set(targetCallKey, null);
                LoggerManager.Debug($"{fucName}: 已删除关系文本: {sourceHeroData.heroName}-{targetHeroData.heroName}");
            }
            else
            {
                plotEventLogData.Set(targetCallKey, relationShipText);
                LoggerManager.Debug($"{fucName}: 已设置关系文本: {sourceHeroData.heroName}-{targetHeroData.heroName}-{relationShipText}");
            }
        }
    }
}
