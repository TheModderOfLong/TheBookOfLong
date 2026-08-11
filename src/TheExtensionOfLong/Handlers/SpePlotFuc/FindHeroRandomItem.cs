using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 从角色背包中随机查找一个道具，并设置为剧情交互物品(plotInteractItem)
    /// 格式: FindHeroRandomItem*角色ID#最小道具等级#最大道具等级#是否包含装备#道具类型(可选,默认-1)
    ///   角色ID: 角色的数字ID/名称/关键字(player等)
    ///   最小道具等级: 整数
    ///   最大道具等级: 整数
    ///   是否包含装备: True/1 包含, False/0 不包含
    ///   道具类型: 整数，默认-1表示不限类型
    /// 示例: FindHeroRandomItem*player#1#5#False        → 玩家背包随机查找等级1-5的非装备道具
    ///       FindHeroRandomItem*小白#1#10#True#3       → 小白背包随机查找等级1-10的装备，类型3
    /// </summary>
    [SpePlotFuc("FindHeroRandomItem")]
    public static class SpePlotFucFindHeroRandomItem
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 4)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*角色ID#最小道具等级#最大道具等级#是否包含装备#道具类型(可选,默认-1)]");
                return;
            }

            // 解析角色
            HeroData hero = CommonHandlers.ResolveHeroId(plotController, fucParams[0]);
            if (hero == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到角色 \"{fucParams[0]}\"");
                return;
            }

            // 解析最小道具等级
            int minItemLv;
            if (!int.TryParse(fucParams[1], out minItemLv))
            {
                LoggerManager.Warning($"{fucName}: 最小道具等级参数无效: {fucParams[1]}");
                return;
            }

            // 解析最大道具等级
            int maxItemLv;
            if (!int.TryParse(fucParams[2], out maxItemLv))
            {
                LoggerManager.Warning($"{fucName}: 最大道具等级参数无效: {fucParams[2]}");
                return;
            }

            // 解析是否包含装备
            string includeStr = fucParams[3].Trim().ToUpper();
            bool includeEquipment = includeStr == "TRUE" || includeStr == "1";

            // 解析道具类型（可选，默认-1）
            int targetItemType = -1;
            if (fucParams.Length > 4 && !string.IsNullOrWhiteSpace(fucParams[4]))
            {
                if (!int.TryParse(fucParams[4], out targetItemType))
                {
                    LoggerManager.Warning($"{fucName}: 道具类型参数无效: {fucParams[4]}");
                    return;
                }
            }

            // 调用FindRandomItem
            ItemData foundItem = hero.FindRandomItem(minItemLv, maxItemLv, includeEquipment, targetItemType);
            if (foundItem == null)
            {
                LoggerManager.Warning($"{fucName}: {hero.heroName} 未找到符合条件的道具 (等级{minItemLv}-{maxItemLv}, 包含装备={includeEquipment}, 类型={targetItemType})");
                return;
            }

            // 设置为剧情交互物品
            plotController.plotInteractItem = foundItem;
            LoggerManager.Debug($"{fucName}: {hero.heroName} 随机找到道具 {foundItem.Name(true)}(ID={foundItem.itemID}), 已设置为plotInteractItem");
        }
    }
}
