using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppSystem.Collections.Generic;
using System.Reflection;
using System;

namespace TheExtensionOfLong
{
    [SpePlotFuc("SetTargetItem")]
    public static class SpePlotFucSetTargetItem
    {
        /// <summary>
        /// 尝试调用"SetTargetItem"功能，将指定物品设置到当前剧情环境中的物品槽位
        /// 格式: SetTargetItem*物品ID/关键字#对象(可选)#角色ID(可选)
        ///   物品ID/关键字: 物品来源，支持关键字(chooseItem/plotInteractItem/playerAuctionItem)
        ///                  或int物品ID(配合角色ID从角色背包查找)
        ///                  "NULL"(忽略大小写)时进入清除模式，将目标槽位设为null
        ///   对象(可选): 要设置的目标槽位，默认为plotInteractItem
        ///     支持的关键字: plotInteractItem/剧情交互物品(默认), chooseItem/选中物品,
        ///                  playerAuctionItem/玩家拍卖物品,
        ///                  tempPlotShop[-Index]/临时剧情商店[-索引]
        ///   tempPlotShop行为(正常模式)：
        ///     不指定Index → 追加到tempPlotShop.allItem末尾
        ///     指定Index且Index存在 → 替换该位置
        ///     指定Index但Index越界 → 追加到末尾
        ///   tempPlotShop行为(NULL模式)：
        ///     不指定Index → 清空tempPlotShop.allItem
        ///     指定Index且Index存在 → 移除该位置的物品
        ///     指定Index但Index越界 → 不操作并警告
        ///   角色ID(可选): 当物品ID为int时，从该角色背包中查找物品；不指定则默认从玩家背包查找
        /// 示例: SetTargetItem*plotInteractItem                        → 将剧情交互物品设为plotInteractItem（无意义，仅示例）
        ///       SetTargetItem*1001                                    → 从玩家背包查找ID=1001的物品，设为plotInteractItem
        ///       SetTargetItem*1001#chooseItem                         → 从玩家背包查找ID=1001的物品，设为选中物品
        ///       SetTargetItem*1001#plotInteractItem#小白              → 从小白背包查找ID=1001的物品，设为plotInteractItem
        ///       SetTargetItem*chooseItem#playerAuctionItem            → 将选中物品设为玩家拍卖物品
        ///       SetTargetItem*1001#tempPlotShop                       → 从玩家背包查找ID=1001的物品，追加到tempPlotShop
        ///       SetTargetItem*1001#tempPlotShop-0                     → 从玩家背包查找ID=1001的物品，设为tempPlotShop[0]，越界则追加
        ///       SetTargetItem*NULL#plotInteractItem                   → 将plotInteractItem设为null
        ///       SetTargetItem*NULL#tempPlotShop                       → 清空tempPlotShop
        ///       SetTargetItem*NULL#tempPlotShop-0                     → 移除tempPlotShop[0]
        /// </summary>
        public static void TryCall(PlotController __instance, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 1 || string.IsNullOrWhiteSpace(fucParams[0]))
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*物品ID/关键字#对象(可选)#角色ID(可选)]");
                return;
            }

            string itemRef = fucParams[0].Trim();
            string targetSlot = fucParams.Length > 1 && !string.IsNullOrWhiteSpace(fucParams[1]) ? fucParams[1].Trim() : null;
            string heroRef = fucParams.Length > 2 && !string.IsNullOrWhiteSpace(fucParams[2]) ? fucParams[2].Trim() : null;

            if (string.IsNullOrWhiteSpace(itemRef))
            {
                LoggerManager.Warning($"{fucName}: 物品引用不能为空");
                return;
            }

            // NULL模式：将目标槽位设为null（清除模式）
            bool isNullMode = itemRef.Equals("NULL", System.StringComparison.OrdinalIgnoreCase);

            // 1. 解析物品引用 → ItemData
            ItemData itemData = null;

            if (!isNullMode)
            {
                // 先尝试用 ResolveItemSource 解析关键字
                string lowerItemRef = itemRef.ToLower();
                itemData = CommonHandlers.ResolveItemSource(__instance, lowerItemRef);

                if (itemData == null)
                {
                    // 尝试按 int 物品ID查找
                    if (!int.TryParse(itemRef, out int itemId))
                    {
                        LoggerManager.Warning($"{fucName}: 未知的物品引用 \"{itemRef}\"，应为关键字(chooseItem/plotInteractItem/playerAuctionItem)或int物品ID或NULL");
                        return;
                    }

                    // 确定从哪个角色背包查找
                    HeroData searchHero;
                    if (!string.IsNullOrEmpty(heroRef))
                    {
                        searchHero = CommonHandlers.ResolveHeroId(__instance, heroRef);
                        if (searchHero == null)
                        {
                            LoggerManager.Warning($"{fucName}: 未找到角色 \"{heroRef}\"，无法从其背包查找物品");
                            return;
                        }
                    }
                    else
                    {
                        searchHero = CommonHandlers.GetPlayerHero();
                    }

                    if (searchHero == null)
                    {
                        LoggerManager.Warning($"{fucName}: 未找到可搜索的角色，无法按物品ID查找");
                        return;
                    }

                    // 在角色背包中查找指定ID的物品
                    ItemListData itemListData = searchHero.itemListData;
                    if (itemListData == null || itemListData.allItem == null)
                    {
                        LoggerManager.Warning($"{fucName}: 角色 {searchHero.heroName} 的背包数据为空");
                        return;
                    }

                    foreach (ItemData item in itemListData.allItem)
                    {
                        if (item != null && item.itemID == itemId)
                        {
                            itemData = item;
                            break;
                        }
                    }

                    if (itemData == null)
                    {
                        LoggerManager.Warning($"{fucName}: 在角色 {searchHero.heroName} 的背包中未找到ID为 {itemId} 的物品");
                        return;
                    }
                }
            }

            // 2. 解析目标槽位并设置
            string lowerSlot = (targetSlot ?? "").ToLower().Trim();

            if (string.IsNullOrEmpty(lowerSlot) || lowerSlot == "plotinteractitem" || lowerSlot == "剧情交互物品")
            {
                __instance.plotInteractItem = itemData;
                if (isNullMode)
                    LoggerManager.Debug($"{fucName}: 已将 plotInteractItem 设为 null");
                else
                    LoggerManager.Debug($"{fucName}: 已将 {itemData.Name(true)}(ID={itemData.itemID}) 设为 plotInteractItem");
            }
            else if (lowerSlot == "chooseitem" || lowerSlot == "chosenitem" || lowerSlot == "选中物品")
            {
                ChooseController chooseController = ChooseController._instance;
                if (isNullMode)
                {
                    if (chooseController == null || chooseController.chooseResult == null)
                    {
                        LoggerManager.Warning($"{fucName}: 选择器未打开或无选中对象，无法将chooseItem设为null");
                    }
                    else
                    {
                        ItemIconController itemIconCtrl = chooseController.chooseResult.GetComponent<ItemIconController>();
                        if (itemIconCtrl == null)
                        {
                            LoggerManager.Warning($"{fucName}: chooseResult无ItemIconController，无法将chooseItem设为null");
                        }
                        else
                        {
                            itemIconCtrl.itemData = null;
                            LoggerManager.Debug($"{fucName}: 已将 chooseItem 设为 null");
                        }
                    }
                }
                else if (chooseController == null || chooseController.chooseResult == null)
                {
                    LoggerManager.Warning($"{fucName}: 选择器未打开或无选中对象，无法设为chooseItem，改为设为plotInteractItem");
                    __instance.plotInteractItem = itemData;
                }
                else
                {
                    ItemIconController itemIconCtrl = chooseController.chooseResult.GetComponent<ItemIconController>();
                    if (itemIconCtrl == null)
                    {
                        LoggerManager.Warning($"{fucName}: chooseResult无ItemIconController，无法设为chooseItem，改为设为plotInteractItem");
                        __instance.plotInteractItem = itemData;
                    }
                    else
                    {
                        itemIconCtrl.itemData = itemData;
                        LoggerManager.Debug($"{fucName}: 已将 {itemData.Name(true)}(ID={itemData.itemID}) 设为 chooseItem");
                    }
                }
            }
            else if (lowerSlot == "playerauctionitem" || lowerSlot == "玩家拍卖物品")
            {
                WorldData worldData = CommonHandlers.GetWorldData();
                if (worldData == null)
                {
                    LoggerManager.Warning($"{fucName}: WorldData为空，无法设置玩家拍卖物品");
                    return;
                }
                worldData.PlayerAuctionItem = itemData;
                if (isNullMode)
                    LoggerManager.Debug($"{fucName}: 已将 playerAuctionItem 设为 null");
                else
                    LoggerManager.Debug($"{fucName}: 已将 {itemData.Name(true)}(ID={itemData.itemID}) 设为 playerAuctionItem");
            }
            else if (lowerSlot.StartsWith("tempplotshop") || lowerSlot.StartsWith("临时剧情商店"))
            {
                ItemListData tempPlotShop = __instance.tempPlotShop;

                // NULL模式下，若tempPlotShop为null则先初始化
                if (tempPlotShop == null)
                {
                    if (isNullMode)
                    {
                        tempPlotShop = new ItemListData();
                        __instance.tempPlotShop = tempPlotShop;
                        LoggerManager.Debug($"{fucName}: tempPlotShop为空，已初始化为新的ItemListData");
                    }
                    else
                    {
                        LoggerManager.Warning($"{fucName}: tempPlotShop为空，无法操作临时剧情商店");
                        return;
                    }
                }
                if (tempPlotShop.allItem == null)
                {
                    LoggerManager.Warning($"{fucName}: tempPlotShop.allItem为空，无法操作临时剧情商店");
                    return;
                }

                // 尝试解析 "-Index" 后缀
                int dashPos = targetSlot.IndexOf('-');
                if (dashPos < 0)
                {
                    // 不指定Index
                    if (isNullMode)
                    {
                        // NULL模式 → 清空tempPlotShop
                        tempPlotShop.allItem.Clear();
                        LoggerManager.Debug($"{fucName}: 已清空 tempPlotShop.allItem");
                    }
                    else
                    {
                        // 正常模式 → 追加到末尾
                        tempPlotShop.allItem.Add(itemData);
                        LoggerManager.Debug($"{fucName}: 已将 {itemData.Name(true)}(ID={itemData.itemID}) 追加到 tempPlotShop.allItem[{tempPlotShop.allItem.Count - 1}]");
                    }
                }
                else
                {
                    string indexPart = targetSlot.Substring(dashPos + 1);
                    if (!int.TryParse(indexPart, out int index))
                    {
                        LoggerManager.Warning($"{fucName}: 临时剧情商店索引解析失败: {indexPart}");
                        return;
                    }

                    if (isNullMode)
                    {
                        // NULL模式 → 移除指定Index
                        if (index >= 0 && index < tempPlotShop.allItem.Count)
                        {
                            tempPlotShop.allItem.RemoveAt(index);
                            LoggerManager.Debug($"{fucName}: 已移除 tempPlotShop.allItem[{index}]");
                        }
                        else
                        {
                            LoggerManager.Warning($"{fucName}: 索引{index}越界(列表长度:{tempPlotShop.allItem.Count})，无法移除");
                        }
                    }
                    else
                    {
                        if (index >= 0 && index < tempPlotShop.allItem.Count)
                        {
                            // Index存在 → 替换
                            tempPlotShop.allItem[index] = itemData;
                            LoggerManager.Debug($"{fucName}: 已将 {itemData.Name(true)}(ID={itemData.itemID}) 设为 tempPlotShop.allItem[{index}]");
                        }
                        else
                        {
                            // Index越界 → 追加到末尾
                            tempPlotShop.allItem.Add(itemData);
                            LoggerManager.Debug($"{fucName}: 索引{index}越界(列表长度:{tempPlotShop.allItem.Count - 1})，已将 {itemData.Name(true)}(ID={itemData.itemID}) 追加到 tempPlotShop.allItem[{tempPlotShop.allItem.Count - 1}]");
                        }
                    }
                }
            }
            else
            {
                LoggerManager.Warning($"{fucName}: 不支持的目标槽位 \"{targetSlot}\"，支持: plotInteractItem/chooseItem/playerAuctionItem/tempPlotShop[-Index]");
            }
        }
    }
}
