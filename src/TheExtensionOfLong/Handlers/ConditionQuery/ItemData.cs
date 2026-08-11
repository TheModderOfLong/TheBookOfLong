using Il2Cpp;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace TheExtensionOfLong
{
    [ConditionQuery("ItemData")]
    public static class QueryItemData
    {

        /// <summary>
        /// ItemData 查询指令
        /// 格式: ItemData:物品信息:物品来源
        ///   物品信息: ItemData属性/无参方法名(忽略大小写)，或复合方法如 IsHeroEquip=player
        ///           复合方法多参数用"-"分隔
        ///   物品来源(可选): chooseItem/选中物品, plotInteractItem/剧情交互物品, playerAuctionItem/玩家拍卖物品
        ///                  默认为chooseItem，忽略大小写
        /// 示例:
        ///   [$ItemData:itemID:chooseItem$]           → 选中物品的itemID
        ///   [$ItemData:Name:true$]                   → 选中物品的带颜色名称
        ///   [$ItemData:name:plotInteractItem$]       → 剧情交互物品的名称
        ///   [$ItemData:rareLv$]                      → 选中物品的稀有度等级
        ///   [$ItemData:value$]                       → 选中物品的价值
        ///   [$ItemData:type$]                        → 选中物品的类型枚举值
        ///   [$ItemData:IsHeroEquip=player$]          → 选中物品是否被玩家装备
        ///   [$ItemData:GetTreasureValue$]            → 选中物品的宝藏价值
        ///   [$ItemData:Equiped$]                     → 选中物品是否已装备(0/1)
        ///   [$ItemData:weight:playerAuctionItem$]    → 玩家拍卖物品的重量
        ///   [$ItemData:Count=effects:chooseItem$]    → 选中物品的效果数量
        ///   [$ItemData:Value=effects-0:chooseItem$]  → 选中物品第0个效果值
        /// </summary>
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            if (parts.Length < 2)
            {
                LoggerManager.Warning("  ItemData查询: 参数不足，格式[ItemData:物品信息:物品来源(可选)]");
                return "";
            }

            string fieldInfo = parts[1]; // 物品信息，可能含 =参数
            string itemSourceRaw = parts.Length > 2 ? parts[2] : null; // 物品来源

            // 解析物品来源 → ItemData
            ItemData item = CommonHandlers.ResolveItemSource(plotController, itemSourceRaw);
            if (item == null)
            {
                LoggerManager.Warning($"  ItemData查询: 未找到物品 (source=\"{itemSourceRaw}\")");
                return "";
            }

            // 处理复合方法（如 IsHeroEquip=player）
            int eqIdx = fieldInfo.IndexOf('=');
            if (eqIdx >= 0)
            {
                string methodName = fieldInfo.Substring(0, eqIdx);
                string methodArg = fieldInfo.Substring(eqIdx + 1);
                return ResolveItemCompositeMethod(plotController, item, methodName, methodArg);
            }

            // 无参数的复合方法也支持不带=号调用
            if (ItemCompositeMethods.ContainsKey(fieldInfo))
            {
                return ResolveItemCompositeMethod(plotController, item, fieldInfo, "");
            }

            // 普通属性/方法读取
            return ConditionQueryHandlers.ReadObjectFieldValue(item, "ItemData", fieldInfo);
        }



        /// <summary>
        /// ItemData 复合方法字典
        /// </summary>
        private static readonly Dictionary<string, Func<PlotController, ItemData, string[], string>> ItemCompositeMethods
            = new Dictionary<string, Func<PlotController, ItemData, string[], string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "IsHeroEquip",                    ItemCompositeIsHeroEquip },
            { "Name",                           ItemCompositeName },
            { "GetTreasureValue",               ItemCompositeGetTreasureValue },
            { "GetItemTypeDescribe",            ItemCompositeGetItemTypeDescribe },
            { "BadFame",                        ItemCompositeBadFame },
            { "GetShowRoomFameChange",          ItemCompositeGetShowRoomFameChange },
            { "GetContributionCost",            ItemCompositeGetContributionCost },
            { "GetReadBookContributionCost",    ItemCompositeGetReadBookContributionCost },
            { "TryIdentify",                    ItemCompositeTryIdentify },
            // 子类型数据：EquipmentData
            { "EquipEnhanceLv",                 ItemCompositeEquipEnhanceLv },
            { "EquipLittleType",                ItemCompositeEquipLittleType },
            { "EquipAttriType",                 ItemCompositeEquipAttriType },
            { "EquipEquiped",                   ItemCompositeEquipEquiped },
            { "EquipSpeEnhanceLv",              ItemCompositeEquipSpeEnhanceLv },
            { "EquipSpeWeightLv",               ItemCompositeEquipSpeWeightLv },
            { "EquipExtraAddName",              ItemCompositeEquipExtraAddName },
            // 子类型数据：MedFoodData
            { "MedEnhanceLv",                   ItemCompositeMedEnhanceLv },
            { "MedRandomSpeAddValue",           ItemCompositeMedRandomSpeAddValue },
            // 子类型数据：BookData
            { "BookSkillID",                    ItemCompositeBookSkillID },
            { "BookReadDayCost",                ItemCompositeBookReadDayCost },
            { "BookReadMoneyCost",              ItemCompositeBookReadMoneyCost },
            // 子类型数据：TreasureData
            { "TreasureFullIdentified",         ItemCompositeTreasureFullIdentified },
            { "TreasureKnowledgeNeed",          ItemCompositeTreasureKnowledgeNeed },
            // 子类型数据：HorseData
            { "HorseEquiped",                   ItemCompositeHorseEquiped },
            { "HorseSpeed",                     ItemCompositeHorseSpeed },
            { "HorsePower",                     ItemCompositeHorsePower },
            { "HorseSprint",                    ItemCompositeHorseSprint },
            { "HorseResist",                    ItemCompositeHorseResist },
            { "HorseMaxPower",                  ItemCompositeHorseMaxPower },
            { "HorseDescribe",                  ItemCompositeHorseDescribe },
            { "Count",                          ItemCompositeCount },
            { "Value",                          ItemCompositeValue },
            { "Index",                          ItemCompositeIndex },
        };

        private static string ResolveItemCompositeMethod(PlotController plotController, ItemData item, string methodName, string methodArg)
        {
            string[] args = methodArg.Split('-');

            if (ItemCompositeMethods.TryGetValue(methodName, out var handler))
                return handler(plotController, item, args);

            LoggerManager.Warning($"  ItemData查询: 未知复合方法 \"{methodName}\"");
            return "";
        }

        // ===== ItemData 复合方法实现 =====

        /// <summary>
        /// 判断物品是否被指定角色装备
        /// 格式: IsHeroEquip=角色ID/名称 或 IsHeroEquip (默认targetInteractHero)
        /// </summary>
        private static string ItemCompositeIsHeroEquip(PlotController plotController, ItemData item, string[] args)
        {
            HeroData hero = CommonHandlers.ResolveHeroId(plotController, args.Length > 0 ? args[0] : null);
            if (hero == null || item == null) return "0";
            try
            {
                return item.IsHeroEquip(hero) ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 IsHeroEquip 失败: {e.Message}");
                return "0";
            }
        }

        /// <summary>
        /// 获取物品显示名称
        /// 格式: Name=true(带颜色) 或 Name=false(不带颜色) 或 Name(默认true)
        /// </summary>
        private static string ItemCompositeName(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null) return "";
            bool colored = args.Length < 1 || args[0].ToLower() != "false";
            try
            {
                return item.Name(colored) ?? "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 Name 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取物品宝藏价值
        /// 格式: GetTreasureValue=true(猜测) 或 GetTreasureValue=false(真实) 或 GetTreasureValue(默认false)
        /// </summary>
        private static string ItemCompositeGetTreasureValue(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null) return "";
            bool guess = args.Length > 0 && args[0].ToLower() == "true";
            try
            {
                return item.GetTreasureValue(guess).ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 GetTreasureValue 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取物品类型描述
        /// 格式: GetItemTypeDescribe=true(斜体) 或 GetItemTypeDescribe=false(非斜体) 或 GetItemTypeDescribe(默认true)
        /// </summary>
        private static string ItemCompositeGetItemTypeDescribe(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null) return "";
            bool italic = args.Length < 1 || args[0].ToLower() != "false";
            try
            {
                return item.GetItemTypeDescribe(italic) ?? "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 GetItemTypeDescribe 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取物品恶名值
        /// 格式: BadFame=倍率(默认1)
        /// </summary>
        private static string ItemCompositeBadFame(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null) return "";
            float rate = 1f;
            if (args.Length > 0 && float.TryParse(args[0], out float parsedRate))
                rate = parsedRate;
            try
            {
                return item.BadFame(rate).ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 BadFame 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取展示厅声望变化
        /// 格式: GetShowRoomFameChange=倍率(默认1)
        /// </summary>
        private static string ItemCompositeGetShowRoomFameChange(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null) return "";
            float rate = 1f;
            if (args.Length > 0 && float.TryParse(args[0], out float parsedRate))
                rate = parsedRate;
            try
            {
                return item.GetShowRoomFameChange(rate).ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 GetShowRoomFameChange 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取指定角色的贡献花费
        /// 格式: GetContributionCost=角色 或 GetContributionCost(默认targetInteractHero)
        /// </summary>
        private static string ItemCompositeGetContributionCost(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null) return "";
            HeroData hero = CommonHandlers.ResolveHeroId(plotController, args.Length > 0 ? args[0] : null);
            if (hero == null) return "";
            try
            {
                return item.GetContributionCost(hero.heroID).ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 GetContributionCost 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取指定角色的读书贡献花费
        /// 格式: GetReadBookContributionCost=角色 或 GetReadBookContributionCost(默认targetInteractHero)
        /// </summary>
        private static string ItemCompositeGetReadBookContributionCost(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null) return "";
            HeroData hero = CommonHandlers.ResolveHeroId(plotController, args.Length > 0 ? args[0] : null);
            if (hero == null) return "";
            try
            {
                return item.GetReadBookContributionCost(hero.heroID).ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 GetReadBookContributionCost 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 尝试鉴定物品
        /// 格式: TryIdentify=鉴定知识值(默认50)
        /// </summary>
        private static string ItemCompositeTryIdentify(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null) return "";
            float knowledge = 50f;
            if (args.Length > 0 && float.TryParse(args[0], out float parsedK))
                knowledge = parsedK;
            try
            {
                return item.TryIdentify(knowledge).ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 TryIdentify 失败: {e.Message}");
                return "";
            }
        }

        // ===== ItemData 子类型数据复合方法 =====

        /// <summary>
        /// 装备强化等级
        /// 格式: EquipEnhanceLv
        /// </summary>
        private static string ItemCompositeEquipEnhanceLv(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null || item.equipmentData == null) return "";
            try
            {
                return item.equipmentData.enhanceLv.ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 EquipEnhanceLv 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 装备小类型
        /// 格式: EquipLittleType
        /// </summary>
        private static string ItemCompositeEquipLittleType(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null || item.equipmentData == null) return "";
            try
            {
                return item.equipmentData.littleType.ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 EquipLittleType 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 武器属性类型
        /// 格式: EquipAttriType
        /// </summary>
        private static string ItemCompositeEquipAttriType(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null || item.equipmentData == null) return "";
            try
            {
                return item.equipmentData.attriType.ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 EquipAttriType 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 装备是否已装备
        /// 格式: EquipEquiped
        /// </summary>
        private static string ItemCompositeEquipEquiped(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null || item.equipmentData == null) return "";
            try
            {
                return item.equipmentData.equiped ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 EquipEquiped 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 装备特殊强化等级
        /// 格式: EquipSpeEnhanceLv
        /// </summary>
        private static string ItemCompositeEquipSpeEnhanceLv(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null || item.equipmentData == null) return "";
            try
            {
                return item.equipmentData.speEnhanceLv.ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 EquipSpeEnhanceLv 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 装备特殊重量等级
        /// 格式: EquipSpeWeightLv
        /// </summary>
        private static string ItemCompositeEquipSpeWeightLv(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null || item.equipmentData == null) return "";
            try
            {
                return item.equipmentData.speWeightLv.ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 EquipSpeWeightLv 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 装备额外加成名称
        /// 格式: EquipExtraAddName
        /// </summary>
        private static string ItemCompositeEquipExtraAddName(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null || item.equipmentData == null) return "";
            try
            {
                return item.equipmentData.GetExtraAddName() ?? "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 EquipExtraAddName 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 药品/食物强化等级
        /// 格式: MedEnhanceLv
        /// </summary>
        private static string ItemCompositeMedEnhanceLv(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null || item.medFoodData == null) return "";
            try
            {
                return item.medFoodData.enhanceLv.ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 MedEnhanceLv 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 药品/食物随机特殊加成值
        /// 格式: MedRandomSpeAddValue
        /// </summary>
        private static string ItemCompositeMedRandomSpeAddValue(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null || item.medFoodData == null) return "";
            try
            {
                return item.medFoodData.randomSpeAddValue.ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 MedRandomSpeAddValue 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 秘籍技能ID
        /// 格式: BookSkillID
        /// </summary>
        private static string ItemCompositeBookSkillID(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null || item.bookData == null) return "";
            try
            {
                return item.bookData.skillID.ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 BookSkillID 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 秘籍阅读天数
        /// 格式: BookReadDayCost
        /// </summary>
        private static string ItemCompositeBookReadDayCost(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null || item.bookData == null) return "";
            try
            {
                return item.bookData.ReadDayCost().ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 BookReadDayCost 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 秘籍阅读金钱
        /// 格式: BookReadMoneyCost
        /// </summary>
        private static string ItemCompositeBookReadMoneyCost(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null || item.bookData == null) return "";
            try
            {
                return item.bookData.ReadMoneyCost().ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 BookReadMoneyCost 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 宝物是否完全鉴定
        /// 格式: TreasureFullIdentified
        /// </summary>
        private static string ItemCompositeTreasureFullIdentified(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null || item.treasureData == null) return "";
            try
            {
                return item.treasureData.fullIdentified ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 TreasureFullIdentified 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 宝物鉴定所需知识等级
        /// 格式: TreasureKnowledgeNeed
        /// </summary>
        private static string ItemCompositeTreasureKnowledgeNeed(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null || item.treasureData == null) return "";
            try
            {
                return item.treasureData.identifyKnowledgeNeed.ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 TreasureKnowledgeNeed 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 坐骑是否骑乘
        /// 格式: HorseEquiped
        /// </summary>
        private static string ItemCompositeHorseEquiped(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null || item.horseData == null) return "";
            try
            {
                return item.horseData.equiped ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 HorseEquiped 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 坐骑实际速度（含加成）
        /// 格式: HorseSpeed
        /// </summary>
        private static string ItemCompositeHorseSpeed(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null || item.horseData == null) return "";
            try
            {
                return item.horseData.Speed().ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 HorseSpeed 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 坐骑实际耐力（含加成）
        /// 格式: HorsePower
        /// </summary>
        private static string ItemCompositeHorsePower(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null || item.horseData == null) return "";
            try
            {
                return item.horseData.Power().ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 HorsePower 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 坐骑实际冲刺（含加成）
        /// 格式: HorseSprint
        /// </summary>
        private static string ItemCompositeHorseSprint(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null || item.horseData == null) return "";
            try
            {
                return item.horseData.Sprint().ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 HorseSprint 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 坐骑实际抗性（含加成）
        /// 格式: HorseResist
        /// </summary>
        private static string ItemCompositeHorseResist(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null || item.horseData == null) return "";
            try
            {
                return item.horseData.Resist().ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 HorseResist 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 坐骑最大耐力
        /// 格式: HorseMaxPower
        /// </summary>
        private static string ItemCompositeHorseMaxPower(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null || item.horseData == null) return "";
            try
            {
                return item.horseData.MaxPower().ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 HorseMaxPower 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 坐骑描述文本
        /// 格式: HorseDescribe
        /// </summary>
        private static string ItemCompositeHorseDescribe(PlotController plotController, ItemData item, string[] args)
        {
            if (item == null || item.horseData == null) return "";
            try
            {
                return item.horseData.GetDescribe() ?? "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ItemData查询 HorseDescribe 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// ItemData Count 复合方法适配器
        /// 格式: Count=属性/方法名
        /// 示例:
        ///   [$ItemData:Count=effects:chooseItem$] → 选中物品的效果数量
        /// </summary>
        private static string ItemCompositeCount(PlotController plotController, ItemData item, string[] args)
        {
            if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                LoggerManager.Warning("  ItemData查询 Count: 参数不足，格式[Count=属性/方法名]");
                return "";
            }
            return ConditionQueryHandlers.GenericCount(item, "ItemData", args[0], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        }

        /// <summary>
        /// ItemData Value 复合方法适配器
        /// 格式: Value=属性/方法名-索引
        /// </summary>
        private static string ItemCompositeValue(PlotController plotController, ItemData item, string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || !int.TryParse(args[1], out int index))
            {
                LoggerManager.Warning("  ItemData查询 Value: 参数不足，格式[Value=属性/方法名-索引]");
                return "";
            }
            return ConditionQueryHandlers.GenericValue(item, "ItemData", args[0], index, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        }

        /// <summary>
        /// ItemData Index 复合方法适配器
        /// 格式: Index=属性/方法名-查找值
        /// </summary>
        private static string ItemCompositeIndex(PlotController plotController, ItemData item, string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
            {
                LoggerManager.Warning("  ItemData查询 Index: 参数不足，格式[Index=属性/方法名-查找值]");
                return "";
            }
            return ConditionQueryHandlers.GenericIndex(item, "ItemData", args[0], args[1], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        }
    }
}
