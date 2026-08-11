using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 让角色进入指定区域。
    /// 格式: HeroEnterArea*角色ID/角色名称/关键字角色#区域ID
    /// 示例: HeroEnterArea*8#55
    /// </summary>
    [SpePlotFuc("HeroEnterArea")]
    public static class SpePlotFucHeroEnterArea
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 2)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*角色ID/角色名称/关键字角色#区域ID]");
                return;
            }

            HeroData hero = CommonHandlers.ResolveHeroId(plotController, fucParams[0]);
            if (hero == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到角色 \"{fucParams[0]}\"");
                return;
            }

            if (!int.TryParse(fucParams[1], out int areaID))
            {
                LoggerManager.Warning($"{fucName}: 区域ID解析失败 \"{fucParams[1]}\"");
                return;
            }

            if (areaID < 0)
            {
                LoggerManager.Warning($"{fucName}: 区域ID不能为负数 \"{areaID}\"");
                return;
            }

            GameController gameController = GameController.Instance;
            if (gameController == null)
            {
                LoggerManager.Error($"{fucName}: GameController实例不存在，无法让角色进入区域");
                return;
            }

            WorldData worldData = gameController.worldData;
            if (worldData == null)
            {
                LoggerManager.Error($"{fucName}: WorldData实例不存在，无法让角色进入区域");
                return;
            }

            AreaData area = worldData.GetArea(areaID);
            if (area == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到区域 ID={areaID}");
                return;
            }

            gameController.HeroEnterArea(hero, area);
            LoggerManager.Debug($"{fucName}: 已让 {hero.heroName}(ID={hero.heroID}) 进入区域 {area.areaName}(ID={area.areaID})");
        }
    }
}
