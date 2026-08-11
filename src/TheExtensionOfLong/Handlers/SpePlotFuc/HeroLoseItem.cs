using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 让角色失去指定ID的道具
    /// 格式: HeroLoseItem*角色ID#道具ID#是否提示(可选,默认False)
    ///   角色ID: 角色的数字ID/名称/关键字(player等)
    ///   道具ID: 道具的数字ID，或固定字"plotInteractItem"/"剧情交互道具"（忽略大小写）表示使用剧情交互物品
    ///   是否提示: True/1 显示提示, False/0 不显示, 默认False
    /// 示例: HeroLoseItem*小白#1001              → 小白失去ID为1001的道具，不显示提示
    ///       HeroLoseItem*player#1001#True       → 玩家失去ID为1001的道具，显示提示
    ///       HeroLoseItem*小白#plotInteractItem   → 小白失去剧情交互物品
    ///       HeroLoseItem*player#剧情交互道具#True → 玩家失去剧情交互物品，显示提示
    /// </summary>
    [SpePlotFuc("HeroLoseItem")]
    public static class SpePlotFucHeroLoseItem
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 2 || string.IsNullOrWhiteSpace(fucParams[0]) || string.IsNullOrWhiteSpace(fucParams[1]))
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*角色ID#道具ID#是否提示(可选,默认False)]");
                return;
            }

            string heroIdStr = fucParams[0].Trim();
            string itemIdStr = fucParams[1].Trim();

            // 解析是否提示（可选，默认False）
            bool showPopInfo = false;
            if (fucParams.Length > 2 && !string.IsNullOrWhiteSpace(fucParams[2]))
            {
                string showStr = fucParams[2].Trim().ToUpper();
                showPopInfo = showStr == "TRUE" || showStr == "1";
            }

            // 解析角色
            HeroData hero = CommonHandlers.ResolveHeroId(plotController, heroIdStr);
            if (hero == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到角色 \"{heroIdStr}\"");
                return;
            }

            string lowerItemId = itemIdStr.ToLower();
            ItemData targetItem = CommonHandlers.ResolveItemSource(plotController, lowerItemId);
            // 判断是否为固定字道具对象
            if (targetItem == null)
            {
                // 按道具ID查找
                int itemId;
                if (!int.TryParse(itemIdStr, out itemId))
                {
                    LoggerManager.Warning($"{fucName}: 道具ID无效: {itemIdStr}，应为数字或固定字(plotInteractItem/剧情交互道具)");
                    return;
                }

                // 遍历角色背包查找指定ID的道具
                ItemListData itemListData = hero.itemListData;
                if (itemListData == null || itemListData.allItem == null)
                {
                    LoggerManager.Error($"{fucName}: 角色背包数据为空");
                    return;
                }

                foreach (ItemData item in itemListData.allItem)
                {
                    if (item != null && item.itemID == itemId)
                    {
                        targetItem = item;
                        break;
                    }
                }

                if (targetItem == null)
                {
                    LoggerManager.Warning($"{fucName}: 角色背包中未找到ID为 {itemId} 的道具");
                    return;
                }
            }

            // 调用LoseItem失去道具
            hero.LoseItem(targetItem, showPopInfo);
            LoggerManager.Debug($"{fucName}: {hero.heroName} 失去道具 {targetItem.Name(true)}(ID={targetItem.itemID}), showPopInfo={showPopInfo}");
        }
    }
}
