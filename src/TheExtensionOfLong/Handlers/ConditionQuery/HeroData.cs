using Il2Cpp;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace TheExtensionOfLong
{
    [ConditionQuery("HeroData")]
    public static class QueryHeroData
    {

        /// <summary>
        /// HeroData 查询指令
        /// 格式: HeroData:角色信息:角色ID
        ///   角色信息: HeroData属性/无参方法名(忽略大小写)，或复合方法如 IsLover=指定角色ID
        ///           复合方法多参数用"-"分隔，如 HaveRelationBetterThanFriend=小白-0-0
        ///   角色ID(可选): int ID / string ID / player / sourceInteractHero / targetInteractHero / 空(默认targetInteractHero)
        /// 示例:
        ///   [$HeroData:heroID:player$]           → 玩家的heroID
        ///   [$HeroData:GetTravelSpeed:小白$]      → 小白的旅行速度
        ///   [$HeroData:isFemale$]                → targetInteractHero的性别(0/1)
        ///   [$HeroData:IsLover=小白:player$]     → 小白是否为玩家的恋人(0/1)
        ///   [$HeroData:HaveRelationBetterThanFriend=小白-0-0:player$] → 不检查师徒和结义
        ///   [$HeroData:GetBaseAttriNum=Sword:player$]   → 玩家的基础剑法属性值
        ///   [$HeroData:GetMaxAttri=Str:小白$]           → 小白的力道属性上限
        ///   [$HeroData:GetAttriRate=Agl:player$]        → 玩家的身法修炼速率
        ///   [$HeroData:Count=PreLovers:player$]  → 玩家的准恋人数量
        ///   [$HeroData:Value=PreLovers-0:player$]  → 玩家第0个准恋人ID
        ///   [$HeroData:Index=hobby-3:player$]     → 爱好值3在列表中的索引(-1=未找到)
        ///   [$HeroData:GetForceLeaderID:player$]     → 玩家所在门派掌门的角色ID
        ///   [$HeroData:GetForceLeaderName:player$]   → 玩家所在门派掌门的角色名称
        ///   [$HeroData:GetForceID:player$]           → 玩家所在门派ID(不含仆从门派)
        ///   [$HeroData:GetForceID=True:player$]      → 玩家所在门派ID(含仆从门派)
        ///   [$HeroData:GetForceName:player$]         → 玩家所在门派名称(不含仆从门派)
        ///   [$HeroData:GetForceName=True:player$]    → 玩家所在门派名称(含仆从门派)
        ///   [$HeroData:GetMainAreaID:player$]        → 玩家所在门派主区域ID
        ///   [$HeroData:GetMainAreaName:player$]      → 玩家所在门派主区域名称
        ///   [$HeroData:GetAreaName:player$]          → 玩家所在区域名称
        ///   Count/Value/Index 为通用方法，也适用于 ItemData/WorldData/GlobalData 查询
        /// 属性类型(BaseAttriType): Str/Agl/Inte/Wil/Con/Mag/Internal/Dodge/Unique/
        ///   Fist/Sword/Knife/Long/Strange/Shoot/Med/Poison/Knowledge/Speech/
        ///   DigAndCut/Plant/CraftEquip/CraftMed/CraftFood
        /// </summary>
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            if (parts.Length < 2)
            {
                LoggerManager.Warning("  HeroData查询: 参数不足，格式[HeroData:角色信息:角色ID(可选)]");
                return "";
            }

            string fieldInfo = parts[1]; // 角色信息，可能含 =参数
            string heroIdRaw = parts.Length > 2 ? parts[2] : null; // 角色ID

            // 解析角色ID → HeroData
            HeroData hero = CommonHandlers.ResolveHeroId(plotController, heroIdRaw);
            if (hero == null)
            {
                LoggerManager.Warning($"  HeroData查询: 未找到角色 (id=\"{heroIdRaw}\")");
                return "";
            }

            // 处理复合方法（如 IsLover=指定角色ID）
            int eqIdx = fieldInfo.IndexOf('=');
            if (eqIdx >= 0)
            {
                string methodName = fieldInfo.Substring(0, eqIdx);
                string methodArg = fieldInfo.Substring(eqIdx + 1);
                return ResolveHeroCompositeMethod(plotController, hero, methodName, methodArg);
            }

            // 无参数的复合方法（如 GetMoney, GetWeight）也支持不带=号调用
            if (CompositeMethods.ContainsKey(fieldInfo))
            {
                return ResolveHeroCompositeMethod(plotController, hero, fieldInfo, "");
            }

            // 普通属性/方法读取
            return ConditionQueryHandlers.ReadObjectFieldValue(hero, "HeroData", fieldInfo);
        }


        /// <summary>
        /// 处理复合查询方法（含=参数的方法）
        /// 格式: 方法名=参数1-参数2-...，如 IsLover=小白，HaveRelationBetterThanFriend=小白-0-0
        /// 多参数用"-"分隔，禁止用":"分隔（":"是查询指令的外层分隔符）
        /// </summary>
        private static readonly Dictionary<string, Func<PlotController, HeroData, string[], string>> CompositeMethods
            = new Dictionary<string, Func<PlotController, HeroData, string[], string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "IsLover",                       CompositeIsLover },
            { "IsPreLover",                    CompositeIsPreLover },
            { "inActive",                      CompositeInActive },
            { "HaveFriend",                    CompositeHaveFriend },
            { "HaveHater",                     CompositeHaveHater },
            { "HaveBrother",                   CompositeHaveBrother },
            { "HaveStudent",                   CompositeHaveStudent },
            { "HaveRelationBetterThanFriend",  CompositeHaveRelationBetterThanFriend },
            { "GetBaseAttriNum",                CompositeGetBaseAttriNum },
            { "GetMaxAttri",                    CompositeGetMaxAttri },
            { "GetAttriRate",                   CompositeGetAttriRate },
            { "GetMoney",                       CompositeGetMoney },
            { "GetWeight",                      CompositeGetWeight },
            { "GetMaxWeight",                   CompositeGetMaxWeight },
            { "HaveItem",                       CompositeHaveItem },
            { "HaveHobby",                      CompositeHaveHobby },
            { "GetItemFavorValue",              CompositeGetItemFavorValue },
            { "GetItemCount",                   CompositeGetItemCount },
            // 高价值复合方法
            { "HaveTag",                        CompositeHaveTag },
            { "GetUseItemValue",                CompositeGetUseItemValue },
            { "SameForce",                      CompositeSameForce },
            { "HaveMission",                    CompositeHaveMission },
            { "CanUseSkill",                    CompositeCanUseSkill },
            { "GetStartFavor",                  CompositeGetStartFavor },
            // 中价值复合方法
            { "GetFightExpRate",                CompositeGetFightExpRate },
            { "GetBookExpRate",                 CompositeGetBookExpRate },
            { "GetSkillPowerChargeSpeed",       CompositeGetSkillPowerChargeSpeed },
            { "GetSkillRareLvExpRate",          CompositeGetSkillRareLvExpRate },
            { "GetAreaID",                      CompositeGetAreaID },
            { "HaveForceFunction",              CompositeHaveForceFunction },
            { "GetFavorRate",                   CompositeGetFavorRate },
            { "AttackSkillSlotUnlocked",        CompositeAttackSkillSlotUnlocked },
            { "heroGivenName",                  CompositeHeroGivenName },
            { "GetHeroName",                    CompositeGetHeroName },
            { "GetName",                        CompositeGetName },
            { "GetFamilyNameLength",            CompositeGetFamilyNameLength },
            { "GetGivenNameLength",             CompositeGetGivenNameLength },
            { "GetNameLength",                  CompositeGetNameLength },
            { "GetFightScore",                  CompositeGetFightScore },
            { "GetHeroItemLv",                  CompositeGetHeroItemLv },
            { "GetFavorValueRate",              CompositeGetFavorValueRate },
            { "GetFullSetName",                 CompositeGetFullSetName },
            { "GetQuickDetail",                 CompositeGetQuickDetail },
            { "GetRelationShipText",            CompositeGetRelationShipText },
            { "GetSkinName",                    CompositeGetSkinName },
            { "GetHorseTravelSpeed",            CompositeGetHorseTravelSpeed },
            { "GetTravelSpeed",                 CompositeGetTravelSpeed },
            { "GetTradeValueRate",              CompositeGetTradeValueRate },
            { "GetRecruitCost",                 CompositeGetRecruitCost },
            { "GetMaxFightSkill",               CompositeGetMaxFightSkill },
            { "GetMaxLivingSkill",              CompositeGetMaxLivingSkill },
            { "GetLivingSkillExpMax",           CompositeGetLivingSkillExpMax },
            { "GetMaxFavor",                    CompositeGetMaxFavor },
            { "GetMaxBuyValue",                 CompositeGetMaxBuyValue },
            { "GetRecoverRate",                 CompositeGetRecoverRate },
            { "GetFullRecoverTime",             CompositeGetFullRecoverTime },
            { "GetPostureValue",                CompositeGetPostureValue },
            { "HavePrelover",                   CompositeHavePrelover },
            { "HaveTeacherStudentRelation",     CompositeHaveTeacherStudentRelation },
            { "MeetForceJobRequire",            CompositeMeetForceJobRequire },
            { "HaveResource",                   CompositeHaveResource },
            // 势力与门派复合方法
            { "OutsideForceExtraContributionRate", CompositeOutsideForceExtraContributionRate },
            { "GetUpgradeForceLvNeedContribution", CompositeGetUpgradeForceLvNeedContribution },
            { "GetHeroForceLvDescribe",          CompositeGetHeroForceLvDescribe },
            { "GetForceBookStorageExpRate",      CompositeGetForceBookStorageExpRate },
            { "GetForceJobEffectSkillNum",       CompositeGetForceJobEffectSkillNum },
            { "GetForceJobSpeAddResult",         CompositeGetForceJobSpeAddResult },
            { "GetForceLeaderID",                CompositeGetForceLeaderID },
            { "GetForceLeaderName",              CompositeGetForceLeaderName },
            { "GetForceID",                      CompositeGetForceID },
            { "GetForceName",                    CompositeGetForceName },
            { "GetMainAreaID",                   CompositeGetMainAreaID },
            { "GetMainAreaName",                 CompositeGetMainAreaName },
            { "GetAreaName",                     CompositeGetAreaName },
            // 自定义变量复合方法
            { "GetCustomVal",                    CompositeGetCustomVal },
            { "GetCustomIntVal",                 CompositeGetCustomIntVal },
            { "GetCustomFloatVal",               CompositeGetCustomFloatVal },
            { "GetCustomBoolVal",                CompositeGetCustomBoolVal },
            // 通用方法
            { "Count",                          CompositeCount },
            { "Value",                          CompositeValue },
            { "Index",                          CompositeIndex },
            { "GetSpeSkeletonParams",           CompositeGetSpeSkeletonParams },
            { "GetSpeSkeletonType",             CompositeGetSpeSkeletonType },
            { "GetSpeSkeletonParam",            CompositeGetSpeSkeletonParam },
        };

        private static string ResolveHeroCompositeMethod(PlotController plotController, HeroData hero, string methodName, string methodArg)
        {
            // 用"-"拆分参数（多参数场景），单参数时数组长度为1
            string[] args = methodArg.Split('-');

            if (CompositeMethods.TryGetValue(methodName, out var handler))
                return handler(plotController, hero, args);

            LoggerManager.Warning($"  HeroData查询: 未知复合方法 \"{methodName}\"");
            return "";
        }

        // ===== HeroData 复合方法实现 =====

        private static string CompositeInActive(PlotController plotController, HeroData hero, string[] args)
        {
            return HeroInActiveManager.IsInActive(hero) ? "1" : "0";
        }

        private static string CompositeGetCustomVal(PlotController plotController, HeroData hero, string[] args)
        {
            if (!TryGetCustomPropertyName(args, "GetCustomVal", out string propertyName))
                return "";

            return CustomValueManager.GetRaw("HeroData", hero.heroID.ToString(), propertyName);
        }

        private static string CompositeGetCustomIntVal(PlotController plotController, HeroData hero, string[] args)
        {
            if (!TryGetCustomPropertyName(args, "GetCustomIntVal", out string propertyName))
                return "0";

            return CustomValueManager.GetIntString("HeroData", hero.heroID.ToString(), propertyName);
        }

        private static string CompositeGetCustomFloatVal(PlotController plotController, HeroData hero, string[] args)
        {
            if (!TryGetCustomPropertyName(args, "GetCustomFloatVal", out string propertyName))
                return "0.0";

            return CustomValueManager.GetFloatString("HeroData", hero.heroID.ToString(), propertyName);
        }

        private static string CompositeGetCustomBoolVal(PlotController plotController, HeroData hero, string[] args)
        {
            if (!TryGetCustomPropertyName(args, "GetCustomBoolVal", out string propertyName))
                return "0";

            return CustomValueManager.GetBoolString("HeroData", hero.heroID.ToString(), propertyName);
        }

        private static bool TryGetCustomPropertyName(string[] args, string methodName, out string propertyName)
        {
            propertyName = args != null && args.Length > 0 ? args[0]?.Trim() : "";
            if (!string.IsNullOrWhiteSpace(propertyName))
                return true;

            LoggerManager.Warning($"  HeroData查询 {methodName}: 参数不足，格式[{methodName}=属性名]");
            return false;
        }

        private static string CompositeIsLover(PlotController plotController, HeroData hero, string[] args)
        {
            HeroData target = CommonHandlers.ResolveHeroId(plotController, args.Length > 0 ? args[0] : null);
            if (target == null) return "0";
            return (hero.Lover == target.heroID) ? "1" : "0";
        }

        private static string CompositeIsPreLover(PlotController plotController, HeroData hero, string[] args)
        {
            HeroData target = CommonHandlers.ResolveHeroId(plotController, args.Length > 0 ? args[0] : null);
            if (target == null) return "0";
            return hero.HavePrelover(target.heroID) ? "1" : "0";
        }

        private static string CompositeHaveFriend(PlotController plotController, HeroData hero, string[] args)
        {
            HeroData target = CommonHandlers.ResolveHeroId(plotController, args.Length > 0 ? args[0] : null);
            if (target == null) return "0";
            return hero.HaveFriend(target.heroID) ? "1" : "0";
        }

        private static string CompositeHaveHater(PlotController plotController, HeroData hero, string[] args)
        {
            HeroData target = CommonHandlers.ResolveHeroId(plotController, args.Length > 0 ? args[0] : null);
            if (target == null) return "0";
            return hero.HaveHater(target.heroID) ? "1" : "0";
        }

        private static string CompositeHaveBrother(PlotController plotController, HeroData hero, string[] args)
        {
            HeroData target = CommonHandlers.ResolveHeroId(plotController, args.Length > 0 ? args[0] : null);
            if (target == null) return "0";
            return hero.HaveBrother(target.heroID) ? "1" : "0";
        }

        private static string CompositeHaveStudent(PlotController plotController, HeroData hero, string[] args)
        {
            HeroData target = CommonHandlers.ResolveHeroId(plotController, args.Length > 0 ? args[0] : null);
            if (target == null) return "0";
            return hero.HaveStudent(target.heroID) ? "1" : "0";
        }

        private static string CompositeHaveRelationBetterThanFriend(PlotController plotController, HeroData hero, string[] args)
        {
            HeroData target = CommonHandlers.ResolveHeroId(plotController, args.Length > 0 ? args[0] : null);
            if (target == null) return "0";
            // 第2个参数: checkTeacher（默认true，0为false；兼容FALSE）
            bool checkTeacher = args.Length <= 1 || (args[1] != "FALSE" && args[1] != "0");
            // 第3个参数: checkBrother（默认true，0为false；兼容FALSE）
            bool checkBrother = args.Length <= 2 || (args[2] != "FALSE" && args[2] != "0");
            return hero.HaveRelationBetterThanFriend(target.heroID, checkTeacher, checkBrother) ? "1" : "0";
        }

        /// <summary>
        /// 解析属性类型字符串为 BaseAttriType 枚举值（忽略大小写）
        /// 支持枚举名（如 Sword）和数字（如 10）
        /// </summary>
        private static bool TryParseAttriType(string attriName, out BaseAttriType attriType)
        {
            attriType = default(BaseAttriType);
            if (string.IsNullOrEmpty(attriName)) return false;

            // 尝试按枚举名解析（忽略大小写）
            if (Enum.TryParse(attriName, true, out BaseAttriType parsed))
            {
                attriType = parsed;
                return true;
            }

            // 尝试按数字解析
            if (int.TryParse(attriName, out int intVal))
            {
                if (Enum.IsDefined(typeof(BaseAttriType), intVal))
                {
                    attriType = (BaseAttriType)intVal;
                    return true;
                }
            }

            return false;
        }

        private static string CompositeGetBaseAttriNum(PlotController plotController, HeroData hero, string[] args)
        {
            string attriName = args.Length > 0 ? args[0] : null;
            if (!TryParseAttriType(attriName, out BaseAttriType attriType))
            {
                LoggerManager.Warning($"  HeroData查询 GetBaseAttriNum: 无效的属性类型 \"{attriName}\"");
                return "";
            }
            try
            {
                return hero.GetBaseAttriNum(attriType).ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetBaseAttriNum({attriType}) 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetMaxAttri(PlotController plotController, HeroData hero, string[] args)
        {
            string attriName = args.Length > 0 ? args[0] : null;
            if (!TryParseAttriType(attriName, out BaseAttriType attriType))
            {
                LoggerManager.Warning($"  HeroData查询 GetMaxAttri: 无效的属性类型 \"{attriName}\"");
                return "";
            }
            try
            {
                return hero.GetMaxAttri((int)attriType).ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetMaxAttri({attriType}) 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetAttriRate(PlotController plotController, HeroData hero, string[] args)
        {
            string attriName = args.Length > 0 ? args[0] : null;
            if (!TryParseAttriType(attriName, out BaseAttriType attriType))
            {
                LoggerManager.Warning($"  HeroData查询 GetAttriRate: 无效的属性类型 \"{attriName}\"");
                return "";
            }
            try
            {
                return hero.GetAttriRate((int)attriType).ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetAttriRate({attriType}) 失败: {e.Message}");
                return "";
            }
        }

        // ===== 物品与背包查询 =====

        /// <summary>
        /// 获取角色持有金钱
        /// 格式: GetMoney 或 GetMoney=storage（storage=个人仓库）
        /// </summary>
        private static string CompositeGetMoney(PlotController plotController, HeroData hero, string[] args)
        {
            bool useStorage = args.Length > 0 && args[0].Equals("storage", StringComparison.OrdinalIgnoreCase);
            try
            {
                ItemListData itemList = useStorage ? hero.selfStorage : hero.itemListData;
                if (itemList == null) return "0";
                return itemList.money.ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetMoney 失败: {e.Message}");
                return "0";
            }
        }

        /// <summary>
        /// 获取角色当前负重
        /// 格式: GetWeight 或 GetWeight=storage
        /// </summary>
        private static string CompositeGetWeight(PlotController plotController, HeroData hero, string[] args)
        {
            bool useStorage = args.Length > 0 && args[0].Equals("storage", StringComparison.OrdinalIgnoreCase);
            try
            {
                ItemListData itemList = useStorage ? hero.selfStorage : hero.itemListData;
                if (itemList == null) return "0";
                return itemList.weight.ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetWeight 失败: {e.Message}");
                return "0";
            }
        }

        /// <summary>
        /// 获取角色负重上限
        /// 格式: GetMaxWeight 或 GetMaxWeight=storage
        /// </summary>
        private static string CompositeGetMaxWeight(PlotController plotController, HeroData hero, string[] args)
        {
            bool useStorage = args.Length > 0 && args[0].Equals("storage", StringComparison.OrdinalIgnoreCase);
            try
            {
                ItemListData itemList = useStorage ? hero.selfStorage : hero.itemListData;
                if (itemList == null) return "0";
                return itemList.maxWeight.ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetMaxWeight 失败: {e.Message}");
                return "0";
            }
        }

        /// <summary>
        /// 判断角色是否持有指定ID的物品
        /// 格式: HaveItem=物品ID 或 HaveItem=物品ID-storage
        /// 返回 "1" 或 "0"
        /// </summary>
        private static string CompositeHaveItem(PlotController plotController, HeroData hero, string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int targetItemID))
            {
                LoggerManager.Warning($"  HeroData查询 HaveItem: 参数格式为 HaveItem=物品ID[-storage]，物品ID必须为整数");
                return "0";
            }
            bool useStorage = args.Length > 1 && args[1].Equals("storage", StringComparison.OrdinalIgnoreCase);
            try
            {
                ItemListData itemList = useStorage ? hero.selfStorage : hero.itemListData;
                if (itemList == null) return "0";
                var allItem = itemList.allItem;
                if (allItem == null) return "0";
                for (int i = 0; i < allItem.Count; i++)
                {
                    if (allItem[i] != null && allItem[i].itemID == targetItemID)
                        return "1";
                }
                return "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 HaveItem({targetItemID}) 失败: {e.Message}");
                return "0";
            }
        }

        /// <summary>
        /// 判断指定物品是否符合角色爱好
        /// 格式: HaveHobby=物品来源 或 HaveHobby(默认plotInteractItem)
        ///   物品来源: chooseItem/plotInteractItem/playerAuctionItem，通过ResolveItemSource解析
        /// 示例:
        ///   [$HeroData:HaveHobby=plotInteractItem:player$]  → 玩家是否喜欢剧情交互物品(0/1)
        ///   [$HeroData:HaveHobby=chooseItem$]               → targetInteractHero是否喜欢选中物品(0/1)
        /// </summary>
        private static string CompositeHaveHobby(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "0";
            string itemSourceRaw = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : null;
            try
            {
                ItemData item = CommonHandlers.ResolveItemSource(plotController, itemSourceRaw);
                if (item == null)
                {
                    LoggerManager.Warning($"  HeroData查询 HaveHobby: 未找到物品 (source=\"{itemSourceRaw}\")");
                    return "0";
                }
                return hero.HaveHobby(item) ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 HaveHobby 失败: {e.Message}");
                return "0";
            }
        }

        /// <summary>
        /// 获取物品对角色的好感度价值
        /// 格式: GetItemFavorValue=物品来源[-maxLimit] 或 GetItemFavorValue(默认plotInteractItem，maxLimit=20)
        ///   物品来源: chooseItem/plotInteractItem/playerAuctionItem，通过ResolveItemSource解析
        ///   maxLimit(可选): 好感度上限，默认20；支持float数值，True=20(默认)，False=0
        /// 示例:
        ///   [$HeroData:GetItemFavorValue=plotInteractItem:小白$]       → 剧情交互物品对小白的赠礼好感价值(默认上限20)
        ///   [$HeroData:GetItemFavorValue=chooseItem-10:player$]       → 选中物品对玩家的赠礼好感价值(上限10)
        ///   [$HeroData:GetItemFavorValue=chooseItem-False:player$]    → 选中物品对玩家的赠礼好感价值(上限0)
        /// </summary>
        private static string CompositeGetItemFavorValue(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            string itemSourceRaw = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : null;
            string maxLimitRaw = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]) ? args[1] : null;
            try
            {
                ItemData item = CommonHandlers.ResolveItemSource(plotController, itemSourceRaw);
                if (item == null)
                {
                    LoggerManager.Warning($"  HeroData查询 GetItemFavorValue: 未找到物品 (source=\"{itemSourceRaw}\")");
                    return "";
                }

                float maxLimit = 20f;
                if (maxLimitRaw != null)
                {
                    string lower = maxLimitRaw.ToLower();
                    if (lower == "true")
                        maxLimit = 20f;
                    else if (lower == "false")
                        maxLimit = 0f;
                    else if (float.TryParse(maxLimitRaw, out float parsed))
                        maxLimit = parsed;
                    else
                        LoggerManager.Warning($"  HeroData查询 GetItemFavorValue: 无法解析maxLimit \"{maxLimitRaw}\"，使用默认值20");
                }

                return hero.GetItemFavorValue(item, maxLimit).ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetItemFavorValue 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取角色持有指定ID物品的数量
        /// 格式: GetItemCount=物品ID 或 GetItemCount=物品ID-storage
        /// </summary>
        private static string CompositeGetItemCount(PlotController plotController, HeroData hero, string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int targetItemID))
            {
                LoggerManager.Warning($"  HeroData查询 GetItemCount: 参数格式为 GetItemCount=物品ID[-storage]，物品ID必须为整数");
                return "0";
            }
            bool useStorage = args.Length > 1 && args[1].Equals("storage", StringComparison.OrdinalIgnoreCase);
            try
            {
                ItemListData itemList = useStorage ? hero.selfStorage : hero.itemListData;
                if (itemList == null) return "0";
                var allItem = itemList.allItem;
                if (allItem == null) return "0";
                int count = 0;
                for (int i = 0; i < allItem.Count; i++)
                {
                    if (allItem[i] != null && allItem[i].itemID == targetItemID)
                        count++;
                }
                return count.ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetItemCount({targetItemID}) 失败: {e.Message}");
                return "0";
            }
        }

        // ===== 高价值复合方法 =====

        /// <summary>
        /// 检查角色是否有指定Tag
        /// 格式: HaveTag=标签ID
        /// 示例:
        ///   [$HeroData:HaveTag=10$]             → targetInteractHero是否有标签10(0/1)
        ///   [$HeroData:HaveTag=5:player$]       → 玩家是否有标签5(0/1)
        /// </summary>
        private static string CompositeHaveTag(PlotController plotController, HeroData hero, string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int tagID))
            {
                LoggerManager.Warning($"  HeroData查询 HaveTag: 参数格式为 HaveTag=标签ID，标签ID必须为整数");
                return "0";
            }
            try
            {
                return hero.HaveTag(tagID) ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 HaveTag({tagID}) 失败: {e.Message}");
                return "0";
            }
        }

        /// <summary>
        /// 获取物品对角色的使用价值
        /// 格式: GetUseItemValue=物品来源[-useExtraRate] 或 GetUseItemValue(默认plotInteractItem，useExtraRate=true)
        ///   物品来源: chooseItem/plotInteractItem/playerAuctionItem，通过ResolveItemSource解析
        ///   useExtraRate(可选): 是否使用额外状态倍率，默认true；支持0/1/True/False
        /// 示例:
        ///   [$HeroData:GetUseItemValue=plotInteractItem:player$]       → 剧情交互物品对玩家的使用价值
        ///   [$HeroData:GetUseItemValue=chooseItem-False:小白$]        → 选中物品对小白的使用价值(不使用额外倍率)
        /// </summary>
        private static string CompositeGetUseItemValue(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            string itemSourceRaw = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : null;
            string useExtraRateRaw = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]) ? args[1] : null;
            try
            {
                ItemData item = CommonHandlers.ResolveItemSource(plotController, itemSourceRaw);
                if (item == null)
                {
                    LoggerManager.Warning($"  HeroData查询 GetUseItemValue: 未找到物品 (source=\"{itemSourceRaw}\")");
                    return "";
                }

                bool useExtraRate = true;
                if (useExtraRateRaw != null)
                {
                    string lower = useExtraRateRaw.ToLower();
                    if (lower == "false" || lower == "0")
                        useExtraRate = false;
                    else if (lower == "true" || lower == "1")
                        useExtraRate = true;
                }

                return hero.GetUseItemValue(item, useExtraRate).ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetUseItemValue 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 判断两个角色是否属于同一势力
        /// 格式: SameForce=目标角色ID/名称 或 SameForce(默认sourceInteractHero)
        /// 示例:
        ///   [$HeroData:SameForce=小白:player$]  → 玩家是否与小白同势力(0/1)
        ///   [$HeroData:SameForce$]               → targetInteractHero是否与sourceInteractHero同势力(0/1)
        /// </summary>
        private static string CompositeSameForce(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "0";
            string targetHeroRaw = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : null;
            try
            {
                HeroData targetHero = CommonHandlers.ResolveHeroId(plotController, targetHeroRaw, plotController.sourceInteractHero);
                if (targetHero == null)
                {
                    LoggerManager.Warning($"  HeroData查询 SameForce: 未找到目标角色 (id=\"{targetHeroRaw}\")");
                    return "0";
                }
                return hero.SameForce(targetHero) ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 SameForce 失败: {e.Message}");
                return "0";
            }
        }

        /// <summary>
        /// 检查角色是否有指定任务
        /// 格式: HaveMission=任务名
        /// 示例:
        ///   [$HeroData:HaveMission=护送任务:player$]  → 玩家是否有"护送任务"(0/1)
        /// </summary>
        private static string CompositeHaveMission(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "0";
            if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                LoggerManager.Warning($"  HeroData查询 HaveMission: 参数格式为 HaveMission=任务名");
                return "0";
            }
            try
            {
                return hero.HaveMission(args[0]) ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 HaveMission({args[0]}) 失败: {e.Message}");
                return "0";
            }
        }

        /// <summary>
        /// 判断角色是否能使用指定武学
        /// 格式: CanUseSkill=技能来源 或 CanUseSkill(默认plotInteractSkill)
        ///   技能来源: plotInteractSkill/剧情交互技能, chooseSkill/选中技能，通过ResolveKungfuSkillSource解析
        /// 示例:
        ///   [$HeroData:CanUseSkill=plotInteractSkill:player$]  → 玩家是否能使用剧情交互武学(0/1)
        ///   [$HeroData:CanUseSkill$]                            → targetInteractHero是否能使用剧情交互武学(0/1)
        /// </summary>
        private static string CompositeCanUseSkill(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "0";
            string skillSourceRaw = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : null;
            try
            {
                KungfuSkillLvData skill = CommonHandlers.ResolveKungfuSkillSource(plotController, skillSourceRaw);
                if (skill == null)
                {
                    LoggerManager.Warning($"  HeroData查询 CanUseSkill: 未找到技能 (source=\"{skillSourceRaw}\")");
                    return "0";
                }
                return hero.CanUseSkill(skill) ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 CanUseSkill 失败: {e.Message}");
                return "0";
            }
        }

        /// <summary>
        /// 获取与另一角色的初始好感度
        /// 格式: GetStartFavor=目标角色ID/名称 或 GetStartFavor(默认sourceInteractHero)
        /// 示例:
        ///   [$HeroData:GetStartFavor=小白:player$]  → 玩家与小白的初始好感度
        ///   [$HeroData:GetStartFavor$]               → targetInteractHero与sourceInteractHero的初始好感度
        /// </summary>
        private static string CompositeGetStartFavor(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            string targetHeroRaw = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : null;
            try
            {
                HeroData targetHero = CommonHandlers.ResolveHeroId(plotController, targetHeroRaw, plotController.sourceInteractHero);
                if (targetHero == null)
                {
                    LoggerManager.Warning($"  HeroData查询 GetStartFavor: 未找到目标角色 (id=\"{targetHeroRaw}\")");
                    return "";
                }
                return hero.GetStartFavor(targetHero).ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetStartFavor 失败: {e.Message}");
                return "";
            }
        }

        // ===== 中价值复合方法 =====

        /// <summary>
        /// 获取角色对指定武学的战斗经验倍率
        /// 格式: GetFightExpRate=技能来源 或 GetFightExpRate(默认plotInteractSkill)
        ///   技能来源: plotInteractSkill/剧情交互技能, chooseSkill/选中技能，通过ResolveKungfuSkillSource解析
        /// 示例:
        ///   [$HeroData:GetFightExpRate=plotInteractSkill:player$]  → 玩家对剧情交互武学的战斗经验倍率
        /// </summary>
        private static string CompositeGetFightExpRate(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            string skillSourceRaw = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : null;
            try
            {
                KungfuSkillLvData skill = CommonHandlers.ResolveKungfuSkillSource(plotController, skillSourceRaw);
                if (skill == null)
                {
                    LoggerManager.Warning($"  HeroData查询 GetFightExpRate: 未找到技能 (source=\"{skillSourceRaw}\")");
                    return "";
                }
                return hero.GetFightExpRate(skill).ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetFightExpRate 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取角色对指定武学的秘籍经验倍率
        /// 格式: GetBookExpRate=技能来源 或 GetBookExpRate(默认plotInteractSkill)
        ///   技能来源: plotInteractSkill/剧情交互技能, chooseSkill/选中技能，通过ResolveKungfuSkillSource解析
        /// 示例:
        ///   [$HeroData:GetBookExpRate=plotInteractSkill:player$]  → 玩家对剧情交互武学的秘籍经验倍率
        /// </summary>
        private static string CompositeGetBookExpRate(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            string skillSourceRaw = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : null;
            try
            {
                KungfuSkillLvData skill = CommonHandlers.ResolveKungfuSkillSource(plotController, skillSourceRaw);
                if (skill == null)
                {
                    LoggerManager.Warning($"  HeroData查询 GetBookExpRate: 未找到技能 (source=\"{skillSourceRaw}\")");
                    return "";
                }
                return hero.GetBookExpRate(skill).ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetBookExpRate 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取角色指定战斗技能类型的充能速度
        /// 格式: GetSkillPowerChargeSpeed=技能类型
        ///   技能类型: FightSkillType枚举值(int)或枚举名
        /// 示例:
        ///   [$HeroData:GetSkillPowerChargeSpeed=0:player$]  → 玩家的技能充能速度(类型0)
        /// </summary>
        private static string CompositeGetSkillPowerChargeSpeed(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            if (args.Length < 1 || !int.TryParse(args[0], out int skillType))
            {
                LoggerManager.Warning($"  HeroData查询 GetSkillPowerChargeSpeed: 参数格式为 GetSkillPowerChargeSpeed=技能类型(整数)");
                return "";
            }
            try
            {
                return hero.GetSkillPowerChargeSpeed((FightSkillType)skillType).ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetSkillPowerChargeSpeed({skillType}) 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取角色指定稀有度的经验倍率
        /// 格式: GetSkillRareLvExpRate=稀有度等级
        /// 示例:
        ///   [$HeroData:GetSkillRareLvExpRate=3:player$]  → 玩家对稀有度3武学的经验倍率
        /// </summary>
        private static string CompositeGetSkillRareLvExpRate(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            if (args.Length < 1 || !int.TryParse(args[0], out int rareLv))
            {
                LoggerManager.Warning($"  HeroData查询 GetSkillRareLvExpRate: 参数格式为 GetSkillRareLvExpRate=稀有度等级(整数)");
                return "";
            }
            try
            {
                return hero.GetSkillRareLvExpRate(rareLv).ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetSkillRareLvExpRate({rareLv}) 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取角色所在区域ID
        /// 格式: GetAreaID[=includeNear] 或 GetAreaID(默认不含附近区域)
        ///   includeNear(可选): 是否包含附近区域，默认False；支持0/1/True/False
        /// 示例:
        ///   [$HeroData:GetAreaID:player$]          → 玩家所在区域ID
        ///   [$HeroData:GetAreaID=True:player$]     → 玩家所在区域ID(含附近区域)
        /// </summary>
        private static string CompositeGetAreaID(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            string includeNearRaw = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : null;
            try
            {
                bool includeNear = false;
                if (includeNearRaw != null)
                {
                    string lower = includeNearRaw.ToLower();
                    if (lower == "true" || lower == "1")
                        includeNear = true;
                }
                return hero.GetAreaID(includeNear).ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetAreaID 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 检查角色所在势力是否有指定功能
        /// 格式: HaveForceFunction=势力功能ID
        /// 示例:
        ///   [$HeroData:HaveForceFunction=1:player$]  → 玩家所在势力是否有功能1(0/1)
        /// </summary>
        private static string CompositeHaveForceFunction(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "0";
            if (args.Length < 1 || !int.TryParse(args[0], out int forceID))
            {
                LoggerManager.Warning($"  HeroData查询 HaveForceFunction: 参数格式为 HaveForceFunction=势力功能ID(整数)");
                return "0";
            }
            try
            {
                return hero.HaveForceFunction(forceID) ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 HaveForceFunction({forceID}) 失败: {e.Message}");
                return "0";
            }
        }

        /// <summary>
        /// 获取角色对指定好感度变化值的变化倍率
        /// 格式: GetFavorRate=数值
        /// 示例:
        ///   [$HeroData:GetFavorRate=10:小白$]  → 小白对好感度变化值10的倍率
        /// </summary>
        private static string CompositeGetFavorRate(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            if (args.Length < 1 || !float.TryParse(args[0], out float favorNum))
            {
                LoggerManager.Warning($"  HeroData查询 GetFavorRate: 参数格式为 GetFavorRate=数值(float)");
                return "";
            }
            try
            {
                return hero.GetFavorRate(favorNum).ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetFavorRate({favorNum}) 失败: {e.Message}");
                return "";
            }
        }

        private static bool TryGetArg(string[] args, int index, out string value)
        {
            value = args != null && args.Length > index ? args[index] : null;
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool ParseBoolArg(string[] args, int index, bool defaultValue = false)
        {
            return TryGetArg(args, index, out string raw)
                ? CommonHandlers.ParseBool(raw, defaultValue)
                : defaultValue;
        }

        private static bool TryParseIntArg(string[] args, int index, string methodName, out int value)
        {
            value = 0;
            if (TryGetArg(args, index, out string raw) && int.TryParse(raw, out value))
                return true;

            LoggerManager.Warning($"  HeroData查询 {methodName}: 第{index + 1}个参数需要为整数");
            return false;
        }

        private static bool TryParseFloatArg(string[] args, int index, string methodName, out float value)
        {
            value = 0f;
            if (TryGetArg(args, index, out string raw) && float.TryParse(raw, out value))
                return true;

            LoggerManager.Warning($"  HeroData查询 {methodName}: 第{index + 1}个参数需要为数值");
            return false;
        }

        private static string CompositeAttackSkillSlotUnlocked(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "0";
            if (!TryParseIntArg(args, 0, "AttackSkillSlotUnlocked", out int skillSlotID))
                return "0";

            try
            {
                return hero.AttackSkillSlotUnlocked(skillSlotID) ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 AttackSkillSlotUnlocked({skillSlotID}) 失败: {e.Message}");
                return "0";
            }
        }

        private static string CompositeGetHeroName(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            bool fullDescribe = ParseBoolArg(args, 0, false);
            try
            {
                return hero.GetHeroName(fullDescribe) ?? "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetHeroName 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeHeroGivenName(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";

            try
            {
                return GetHeroGivenName(hero);
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 heroGivenName 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetName(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            bool colored = ParseBoolArg(args, 0, true);
            try
            {
                return hero.Name(colored) ?? "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetName 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetFamilyNameLength(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "0";

            try
            {
                string familyName = hero.HeroFamilyName() ?? "";
                return familyName.Length.ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetFamilyNameLength 失败: {e.Message}");
                return "0";
            }
        }

        private static string CompositeGetGivenNameLength(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "0";

            try
            {
                string givenName = GetHeroGivenName(hero);
                return givenName.Length.ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetGivenNameLength 失败: {e.Message}");
                return "0";
            }
        }

        private static string CompositeGetNameLength(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "0";

            try
            {
                string fullName = hero.HeroName(false) ?? "";
                return fullName.Length.ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetNameLength 失败: {e.Message}");
                return "0";
            }
        }

        private static string GetHeroGivenName(HeroData hero)
        {
            string fullName = hero.HeroName(false) ?? "";
            string familyName = hero.HeroFamilyName() ?? "";

            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(familyName))
                return fullName;

            if (fullName.StartsWith(familyName, StringComparison.Ordinal))
                return fullName.Substring(familyName.Length);

            return fullName;
        }

        private static string CompositeGetFightScore(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            bool includeTeamMate = ParseBoolArg(args, 0, false);
            try
            {
                return hero.GetFightScore(includeTeamMate).ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetFightScore 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetHeroItemLv(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            bool useStrengthLv = ParseBoolArg(args, 0, false);
            try
            {
                return hero.GetHeroItemLv(useStrengthLv).ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetHeroItemLv 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetFavorValueRate(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            bool buy = ParseBoolArg(args, 0, false);
            try
            {
                return hero.GetFavorValueRate(buy).ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetFavorValueRate 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetFullSetName(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            bool useGrayColor = ParseBoolArg(args, 0, false);
            try
            {
                return hero.GetFullSetName(useGrayColor) ?? "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetFullSetName 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetQuickDetail(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            bool fullDetail = ParseBoolArg(args, 0, true);
            bool showForceContribution = ParseBoolArg(args, 1, false);
            bool showTagPoint = ParseBoolArg(args, 2, false);
            bool showSkillNum = ParseBoolArg(args, 3, false);
            try
            {
                return hero.GetQuickDetail(fullDetail, showForceContribution, showTagPoint, showSkillNum) ?? "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetQuickDetail 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetRelationShipText(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            HeroData target = CommonHandlers.ResolveHeroId(plotController, TryGetArg(args, 0, out string targetRaw) ? targetRaw : null);
            if (target == null) return "";

            bool fullDescribe = ParseBoolArg(args, 1, true);
            bool useColor = ParseBoolArg(args, 2, false);
            try
            {
                return hero.GetRelationShipText(target.heroID, fullDescribe, useColor) ?? "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetRelationShipText 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetSkinName(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            bool skeletonAnim = ParseBoolArg(args, 0, true);
            int forceSkinID = TryGetArg(args, 1, out string skinRaw) && int.TryParse(skinRaw, out int skinID) ? skinID : -99;
            int forceSkinLv = TryGetArg(args, 2, out string lvRaw) && int.TryParse(lvRaw, out int skinLv) ? skinLv : -1;
            try
            {
                return hero.GetSkinName(skeletonAnim, forceSkinID, forceSkinLv) ?? "";
            }

            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetSkinName 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetSpeSkeletonParams(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            LoggerManager.Debug($"[GetSpeSkeletonParams] 角色 {hero.heroName}(ID={hero.heroID})");

            // 第1级：运行时覆盖变量 SpeHeroSkeleton_{HeroID}
            string key = "SpeHeroSkeleton_" + hero.heroID;
            WorldData worldData = CommonHandlers.GetWorldData();
            if (worldData != null && worldData.PlotEventLog != null)
            {
                string cached = worldData.PlotEventLog.Get(key);
                if (!string.IsNullOrEmpty(cached))
                {
                    LoggerManager.Debug($"[GetSpeSkeletonParams] 第1级命中: key={key} value={cached}");
                    return cached;
                }
                LoggerManager.Debug($"[GetSpeSkeletonParams] 第1级未命中: key={key}");
            }

            // 第2级：通过反射查 TheResourceOfLong 的 Mapping 规则
            if (TryGetMappingRuleId(hero, out string ruleId))
            {
                LoggerManager.Debug($"[GetSpeSkeletonParams] 第2级命中: ruleId={ruleId}");
                return "2#" + ruleId;
            }
            LoggerManager.Debug($"[GetSpeSkeletonParams] 第2级未命中: heroID={hero.heroID} 无Mapping规则");

            // 第3级：保底
            string fallback = hero.heroID > 0 ? "1#" + hero.heroID : "0#";
            LoggerManager.Debug($"[GetSpeSkeletonParams] 第3级保底: heroID={hero.heroID} -> {fallback}");
            return fallback;
        }

        private static string CompositeGetSpeSkeletonType(PlotController plotController, HeroData hero, string[] args)
        {
            string full = CompositeGetSpeSkeletonParams(plotController, hero, args);
            int idx = full.IndexOf('#');
            return idx >= 0 ? full.Substring(0, idx) : full;
        }

        private static string CompositeGetSpeSkeletonParam(PlotController plotController, HeroData hero, string[] args)
        {
            string full = CompositeGetSpeSkeletonParams(plotController, hero, args);
            int idx = full.IndexOf('#');
            return (idx >= 0 && idx + 1 < full.Length) ? full.Substring(idx + 1) : "";
        }


        /// <summary>
        /// 通过反射调用 TheResourceOfLong.SpeHeroSkeletonOverrideRegistry.TryGet，
        /// 尝试获取角色的 Mapping 覆盖规则编号。TheResourceOfLong 未安装时返回 false。
        /// </summary>
        private static bool TryGetMappingRuleId(HeroData hero, out string ruleId)
        {
            ruleId = null;
            try
            {
                Type regType = Type.GetType("TheResourceOfLong.SpeHeroSkeletonOverrideRegistry, TheResourceOfLong", false);
                if (regType == null)
                {
                    LoggerManager.Debug($"[GetSpeSkeletonParams] 第2级跳过: TheResourceOfLong 未安装");
                    return false;
                }

                MethodInfo tryGet = regType.GetMethod("TryGet",
                    BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(HeroData), regType.Assembly.GetType("TheResourceOfLong.SpeHeroSkeletonOverrideEntry").MakeByRefType() }, null);
                if (tryGet == null)
                {
                    LoggerManager.Warning($"[GetSpeSkeletonParams] 第2级警告: SpeHeroSkeletonOverrideRegistry.TryGet 方法未找到");
                    return false;
                }

                object[] prms = new object[] { hero, null };
                bool found = (bool)tryGet.Invoke(null, prms);
                if (!found)
                {
                    LoggerManager.Debug($"[GetSpeSkeletonParams] 第2级未命中: heroID={hero.heroID} 无规则");
                    return false;
                }

                object entry = prms[1];
                object rule = entry?.GetType().GetField("Rule")?.GetValue(entry);
                if (rule != null)
                {
                    string id = rule.GetType().GetField("Id")?.GetValue(rule) as string;
                    if (!string.IsNullOrEmpty(id))
                    {
                        ruleId = id;
                        LoggerManager.Debug($"[GetSpeSkeletonParams] 第2级命中: ruleId={id} (来自Rule.Id)");
                        return true;
                    }
                }

                ruleId = hero.heroID.ToString();
                LoggerManager.Debug($"[GetSpeSkeletonParams] 第2级命中: ruleId={ruleId} (无编号，使用heroID)");
                return true;
            }
            catch (Exception e)
            {
                LoggerManager.Warning($"[GetSpeSkeletonParams] 第2级警告: 反射调用异常: {e.Message}");
                return false;
            }
        }

        private static string CompositeGetHorseTravelSpeed(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            try
            {
                if (!TryGetArg(args, 0, out _) && !TryGetArg(args, 1, out _))
                    return hero.GetHorseTravelSpeed().ToString("G");

                bool havePower = ParseBoolArg(args, 0, true);
                bool isSprint = ParseBoolArg(args, 1, false);
                return hero.GetHorseTravelSpeed(havePower, isSprint).ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetHorseTravelSpeed 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetTravelSpeed(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            try
            {
                if (!TryGetArg(args, 0, out _) && !TryGetArg(args, 1, out _))
                    return hero.GetTravelSpeed().ToString("G");

                bool havePower = ParseBoolArg(args, 0, true);
                bool isSprint = ParseBoolArg(args, 1, false);
                return hero.GetTravelSpeed(havePower, isSprint).ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetTravelSpeed 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetTradeValueRate(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            bool buy = ParseBoolArg(args, 0, false);
            try
            {
                if (TryGetArg(args, 1, out _))
                {
                    bool useLivingSkill = ParseBoolArg(args, 1, false);
                    return hero.GetTradeValueRate(buy, useLivingSkill).ToString("G");
                }

                return hero.GetTradeValueRate(buy).ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetTradeValueRate 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetRecruitCost(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            bool tempRecruit = ParseBoolArg(args, 0, false);
            float rate = TryGetArg(args, 1, out string rateRaw) && float.TryParse(rateRaw, out float parsedRate) ? parsedRate : 1f;
            try
            {
                return hero.GetRecruitCost(tempRecruit, rate).ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetRecruitCost 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetMaxFightSkill(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            if (!TryParseIntArg(args, 0, "GetMaxFightSkill", out int id))
                return "";

            try { return hero.GetMaxFightSkill(id).ToString("G"); }
            catch (Exception e) { LoggerManager.Error($"  HeroData查询 GetMaxFightSkill({id}) 失败: {e.Message}"); return ""; }
        }

        private static string CompositeGetMaxLivingSkill(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            if (!TryParseIntArg(args, 0, "GetMaxLivingSkill", out int id))
                return "";

            try { return hero.GetMaxLivingSkill(id).ToString("G"); }
            catch (Exception e) { LoggerManager.Error($"  HeroData查询 GetMaxLivingSkill({id}) 失败: {e.Message}"); return ""; }
        }

        private static string CompositeGetLivingSkillExpMax(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            if (!TryParseIntArg(args, 0, "GetLivingSkillExpMax", out int id))
                return "";

            try { return hero.GetLivingSkillExpMax(id).ToString("G"); }
            catch (Exception e) { LoggerManager.Error($"  HeroData查询 GetLivingSkillExpMax({id}) 失败: {e.Message}"); return ""; }
        }

        private static string CompositeGetMaxFavor(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            float maxFavor = TryGetArg(args, 0, out string raw) && float.TryParse(raw, out float parsed) ? parsed : 100f;
            try { return hero.GetMaxFavor(maxFavor).ToString("G"); }
            catch (Exception e) { LoggerManager.Error($"  HeroData查询 GetMaxFavor 失败: {e.Message}"); return ""; }
        }

        private static string CompositeGetMaxBuyValue(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            float discount = TryGetArg(args, 0, out string raw) && float.TryParse(raw, out float parsed) ? parsed : 1f;
            try { return hero.GetMaxBuyValue(discount).ToString("G"); }
            catch (Exception e) { LoggerManager.Error($"  HeroData查询 GetMaxBuyValue 失败: {e.Message}"); return ""; }
        }

        private static string CompositeGetRecoverRate(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            if (!TryParseFloatArg(args, 0, "GetRecoverRate", out float baseRecoverRate))
                return "";

            try { return hero.GetRecoverRate(baseRecoverRate).ToString("G"); }
            catch (Exception e) { LoggerManager.Error($"  HeroData查询 GetRecoverRate({baseRecoverRate}) 失败: {e.Message}"); return ""; }
        }

        private static string CompositeGetFullRecoverTime(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            if (!TryParseFloatArg(args, 0, "GetFullRecoverTime", out float baseRecoverRate))
                return "";

            try { return hero.GetFullRecoverTime(baseRecoverRate).ToString(); }
            catch (Exception e) { LoggerManager.Error($"  HeroData查询 GetFullRecoverTime({baseRecoverRate}) 失败: {e.Message}"); return ""; }
        }

        private static string CompositeGetPostureValue(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            try
            {
                if (TryGetArg(args, 0, out string raw) && float.TryParse(raw, out float recoverRate))
                    return hero.GetPostureValue(recoverRate).ToString("G");

                return hero.GetPostureValue().ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetPostureValue 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeHavePrelover(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "0";
            HeroData target = CommonHandlers.ResolveHeroId(plotController, TryGetArg(args, 0, out string targetRaw) ? targetRaw : null);
            if (target == null) return "0";
            return hero.HavePrelover(target.heroID) ? "1" : "0";
        }

        private static string CompositeHaveTeacherStudentRelation(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "0";
            HeroData target = CommonHandlers.ResolveHeroId(plotController, TryGetArg(args, 0, out string targetRaw) ? targetRaw : null);
            if (target == null) return "0";
            return hero.HaveTeacherStudentRelation(target.heroID) ? "1" : "0";
        }

        private static string CompositeMeetForceJobRequire(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "0";
            if (!TryParseIntArg(args, 0, "MeetForceJobRequire", out int jobType))
                return "0";

            try { return hero.MeetForceJobRequire(jobType) ? "1" : "0"; }
            catch (Exception e) { LoggerManager.Error($"  HeroData查询 MeetForceJobRequire({jobType}) 失败: {e.Message}"); return "0"; }
        }

        private static string CompositeHaveResource(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "0";
            if (!TryParseIntArg(args, 0, "HaveResource", out int id) ||
                !TryParseFloatArg(args, 1, "HaveResource", out float num))
                return "0";

            try { return hero.HaveResource(id, num) ? "1" : "0"; }
            catch (Exception e) { LoggerManager.Error($"  HeroData查询 HaveResource({id}, {num}) 失败: {e.Message}"); return "0"; }
        }

        // ===== 势力与门派复合方法 =====

        /// <summary>
        /// 获取外部势力额外贡献率
        /// 格式: OutsideForceExtraContributionRate=势力ID
        /// 示例:
        ///   [$HeroData:OutsideForceExtraContributionRate=2:player$]  → 玩家对势力2的额外贡献率
        /// </summary>
        private static string CompositeOutsideForceExtraContributionRate(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            if (args.Length < 1 || !int.TryParse(args[0], out int forceID))
            {
                LoggerManager.Warning($"  HeroData查询 OutsideForceExtraContributionRate: 参数格式为 OutsideForceExtraContributionRate=势力ID(整数)");
                return "";
            }
            try
            {
                return hero.OutsideForceExtraContributionRate(forceID).ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 OutsideForceExtraContributionRate({forceID}) 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取升级门派等级所需贡献（含倍率）
        /// 格式: GetUpgradeForceLvNeedContribution=倍率(可选)
        /// 示例:
        ///   [$HeroData:GetUpgradeForceLvNeedContribution:player$]      → 玩家升级门派等级所需贡献(默认倍率)
        ///   [$HeroData:GetUpgradeForceLvNeedContribution=1.5:player$]  → 玩家升级门派等级所需贡献(1.5倍率)
        /// </summary>
        private static string CompositeGetUpgradeForceLvNeedContribution(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            string rateRaw = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : null;
            try
            {
                if (rateRaw != null && float.TryParse(rateRaw, out float rate))
                {
                    return hero.GetUpgradeForceLvNeedContribution(rate).ToString();
                }
                else
                {
                    return hero.GetUpgradeForceLvNeedContribution().ToString();
                }
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetUpgradeForceLvNeedContribution 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取门派等级描述
        /// 格式: GetHeroForceLvDescribe=是否带颜色(可选)
        /// 示例:
        ///   [$HeroData:GetHeroForceLvDescribe:player$]        → 玩家门派等级描述(默认带颜色)
        ///   [$HeroData:GetHeroForceLvDescribe=True:player$]   → 玩家门派等级描述(带颜色)
        ///   [$HeroData:GetHeroForceLvDescribe=False:player$]  → 玩家门派等级描述(不带颜色)
        /// </summary>
        private static string CompositeGetHeroForceLvDescribe(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            string coloredRaw = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : null;
            try
            {
                bool colored = true;
                if (coloredRaw != null)
                {
                    string lower = coloredRaw.ToLower();
                    if (lower == "false" || lower == "0")
                        colored = false;
                }
                return hero.GetHeroForceLvDescribe(colored);
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetHeroForceLvDescribe 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetForceBookStorageExpRate(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            if (!TryParseIntArg(args, 0, "GetForceBookStorageExpRate", out int skillID))
                return "";

            try
            {
                return hero.GetForceBookStorageExpRate(skillID).ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetForceBookStorageExpRate({skillID}) 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetForceJobEffectSkillNum(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            try
            {
                if (!TryGetArg(args, 0, out _) && !TryGetArg(args, 1, out _))
                    return hero.GetForceJobEffectSkillNum().ToString("G");

                if (!TryParseIntArg(args, 0, "GetForceJobEffectSkillNum", out int jobType) ||
                    !TryParseIntArg(args, 1, "GetForceJobEffectSkillNum", out int jobID))
                    return "";

                return hero.GetForceJobEffectSkillNum(jobType, jobID).ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetForceJobEffectSkillNum 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetForceJobSpeAddResult(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            if (!TryParseIntArg(args, 0, "GetForceJobSpeAddResult", out int forceSpeAddDataType))
                return "";

            try
            {
                return hero.GetForceJobSpeAddResult(forceSpeAddDataType).ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetForceJobSpeAddResult({forceSpeAddDataType}) 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取门派掌门的角色ID
        /// 格式: GetForceLeaderID 或 GetForceLeaderID=includeServant(可选)
        /// 示例:
        ///   [$HeroData:GetForceLeaderID:player$]        → 玩家所在门派掌门的角色ID
        ///   [$HeroData:GetForceLeaderID=True:player$]   → 玩家所在门派掌门的角色ID(含仆从门派)
        /// </summary>
        private static string CompositeGetForceLeaderID(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            bool includeServant = args.Length > 0 && (args[0].ToLower() == "true" || args[0] == "1");
            try
            {
                ForceData force = hero.GetForce(includeServant);
                if (force == null) return "";
                return force.leader.ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetForceLeaderID 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取门派掌门的角色名称
        /// 格式: GetForceLeaderName 或 GetForceLeaderName=includeServant(可选)
        /// 示例:
        ///   [$HeroData:GetForceLeaderName:player$]        → 玩家所在门派掌门的角色名称
        ///   [$HeroData:GetForceLeaderName=True:player$]   → 玩家所在门派掌门的角色名称(含仆从门派)
        /// </summary>
        private static string CompositeGetForceLeaderName(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            bool includeServant = args.Length > 0 && (args[0].ToLower() == "true" || args[0] == "1");
            try
            {
                ForceData force = hero.GetForce(includeServant);
                if (force == null) return "";
                HeroData leader = force.GetLeader();
                if (leader == null) return "";
                return leader.heroName ?? "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetForceLeaderName 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取所在门派的ID
        /// 格式: GetForceID[=includeServant] 或 GetForceID(默认不含仆从门派)
        /// 示例:
        ///   [$HeroData:GetForceID:player$]          → 玩家所在门派ID(不含仆从门派)
        ///   [$HeroData:GetForceID=True:player$]     → 玩家所在门派ID(含仆从门派)
        /// </summary>
        private static string CompositeGetForceID(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            bool includeServant = args.Length > 0 && (args[0].ToLower() == "true" || args[0] == "1");
            try
            {
                ForceData force = hero.GetForce(includeServant);
                if (force == null) return "";
                return force.forceID.ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetForceID 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取所在门派的名称
        /// 格式: GetForceName[=includeServant] 或 GetForceName(默认不含仆从门派)
        /// 示例:
        ///   [$HeroData:GetForceName:player$]        → 玩家所在门派名称(不含仆从门派)
        ///   [$HeroData:GetForceName=True:player$]   → 玩家所在门派名称(含仆从门派)
        /// </summary>
        private static string CompositeGetForceName(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            bool includeServant = args.Length > 0 && (args[0].ToLower() == "true" || args[0] == "1");
            try
            {
                ForceData force = hero.GetForce(includeServant);
                if (force == null) return "";
                return force.GetForceName() ?? force.forceName ?? "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetForceName 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取角色主区域ID。
        /// 目前按角色所在势力的主区域实现；后续如支持自定义角色主区域，可优先读取自定义值。
        /// 格式: GetMainAreaID
        /// 示例:
        ///   [$HeroData:GetMainAreaID:player$]  → 玩家所在势力主区域ID
        /// </summary>
        private static string CompositeGetMainAreaID(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            try
            {
                ForceData force = hero.GetForce(false);
                if (force == null) return "";
                AreaData area = force.MainArea();
                return area != null ? area.areaID.ToString() : "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetMainAreaID 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取角色主区域名称。
        /// 目前按角色所在势力的主区域实现；后续如支持自定义角色主区域，可优先读取自定义值。
        /// 格式: GetMainAreaName
        /// 示例:
        ///   [$HeroData:GetMainAreaName:player$]  → 玩家所在势力主区域名称
        /// </summary>
        private static string CompositeGetMainAreaName(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            try
            {
                ForceData force = hero.GetForce(false);
                if (force == null) return "";
                AreaData area = force.MainArea();
                if (area == null) return "";
                return area.GetAreaName() ?? area.areaName ?? "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetMainAreaName 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取所在区域的名称
        /// 格式: GetAreaName
        /// 示例:
        ///   [$HeroData:GetAreaName:player$]  → 玩家所在区域名称
        /// </summary>
        private static string CompositeGetAreaName(PlotController plotController, HeroData hero, string[] args)
        {
            if (hero == null) return "";
            try
            {
                AreaData area = hero.GetArea();
                if (area == null) return "";
                return area.GetAreaName() ?? area.areaName ?? "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  HeroData查询 GetAreaName 失败: {e.Message}");
                return "";
            }
        }


        /// <summary>
        /// HeroData Count 复合方法适配器
        /// 格式: Count=属性/方法名
        /// 示例:
        ///   [$HeroData:Count=PreLovers:player$]   → 玩家的准恋人数量
        ///   [$HeroData:Count=Students:小白$]       → 小白的弟子数量
        ///   [$HeroData:Count=Brothers:player$]    → 玩家的结义兄弟数量
        ///   [$HeroData:Count=Friends:player$]     → 玩家的好友数量
        ///   [$HeroData:Count=Haters:player$]      → 玩家的仇敌数量
        ///   [$HeroData:Count=Relatives:player$]   → 玩家的亲属数量
        ///   [$HeroData:Count=missions:player$]    → 玩家的任务数量
        /// </summary>
        private static string CompositeCount(PlotController plotController, HeroData hero, string[] args)
        {
            if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                LoggerManager.Warning("  HeroData查询 Count: 参数不足，格式[Count=属性/方法名]");
                return "";
            }
            return ConditionQueryHandlers.GenericCount(hero, "HeroData", args[0], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        }


        /// <summary>
        /// HeroData Value 复合方法适配器
        /// 格式: Value=属性/方法名-索引
        /// 示例:
        ///   [$HeroData:Value=PreLovers-0:player$]    → 玩家第0个准恋人ID
        ///   [$HeroData:Value=hobby-1:player$]        → 玩家第1个爱好值
        ///   [$HeroData:Value=baseAttri-3:player$]    → 玩家第3个基础属性值
        /// </summary>
        private static string CompositeValue(PlotController plotController, HeroData hero, string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || !int.TryParse(args[1], out int index))
            {
                LoggerManager.Warning("  HeroData查询 Value: 参数不足，格式[Value=属性/方法名-索引]");
                return "";
            }
            return ConditionQueryHandlers.GenericValue(hero, "HeroData", args[0], index, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        }


        /// <summary>
        /// HeroData Index 复合方法适配器
        /// 格式: Index=属性/方法名-查找值
        /// 示例:
        ///   [$HeroData:Index=PreLovers-5:player$]    → 准恋人ID=5在列表中的索引
        ///   [$HeroData:Index=hobby-3:player$]        → 爱好值3在列表中的索引
        /// </summary>
        private static string CompositeIndex(PlotController plotController, HeroData hero, string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
            {
                LoggerManager.Warning("  HeroData查询 Index: 参数不足，格式[Index=属性/方法名-查找值]");
                return "";
            }
            return ConditionQueryHandlers.GenericIndex(hero, "HeroData", args[0], args[1], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        }
    }
}
