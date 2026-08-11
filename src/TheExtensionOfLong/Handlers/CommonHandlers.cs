using Il2Cpp;
using System;
//using System.Collections.Generic;
using Il2CppSystem.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TheBookOfLong;
using UnityEngine.UI;

namespace TheExtensionOfLong
{
    public enum FocusListKind
    {
        Kungfu,
        Living
    }

    public class CommonHandlers
    {
        /// <summary>
        /// 获取WorldData实例
        /// </summary>
        public static WorldData GetWorldData()
        {
            GameController instance = GameController.Instance;
            if (instance == null) return null;
            return instance.worldData;
        }

        /// <summary>
        /// 解析角色来源关键字为HeroData实例（仅处理关键字，不处理int/string ID）
        /// 支持: player/玩家, targetInteractHero/targetHero/目标互动角色, sourceInteractHero/sourceHero/源互动角色,
        ///       chooseHero/选中角色, 临时剧情角色:Index/TempPlotHero,
        ///       剧情互动角色:Index/PlotInteractHero,
        ///       任务目标角色/MissionEventTargetHero, 任务发起角色/MissionEventSourceHero,
        ///       比赛角色排名列表:Index/FightMatchHeroFinalList
        /// 非关键字时返回 null，由调用方决定后续处理
        /// 注意: 不包含 "保持/Keep"（仅GetHeroData有此语义）和 ResolveAllCommands 预处理
        /// </summary>
        public static HeroData ResolveHeroSource(PlotController plotController, string heroIdRaw)
        {
            if (string.IsNullOrEmpty(heroIdRaw)) return null;

            string id = heroIdRaw.Trim();
            string lower = id.ToLowerInvariant();

            // player / 玩家
            if (lower == "player" || lower == "玩家")
                return GetPlayerHero();

            // targetInteractHero / targetHero / 目标互动角色
            if (lower == "targetinteracthero" || lower == "targethero" || lower == "目标互动角色")
                return plotController?.targetInteractHero;

            // sourceInteractHero / sourceHero / 源互动角色
            if (lower == "sourceinteracthero" || lower == "sourcehero" || lower == "源互动角色")
                return plotController?.sourceInteractHero;

            // chooseHero / chosenHero / 选中角色
            if (lower == "choosehero" || lower == "chosenhero" || lower == "选中角色")
                return ChooseController._instance?.chooseResult?.GetComponent<HeroIconController>()?.heroData;

            // 临时剧情角色:Index / TempPlotHero（无:Index时取[0]）
            if (lower.StartsWith("tempplothero") || lower.StartsWith("临时剧情角色"))
                return ResolveIndexedHero(plotController, id, plotController?.tempPlotHero, "临时剧情角色/TempPlotHero");

            // 剧情互动角色:Index / PlotInteractHero（无:Index时取[0]）
            if (lower.StartsWith("plotinteracthero") || lower.StartsWith("plotinteractherolist") || lower.StartsWith("剧情互动角色"))
                return ResolveIndexedHero(plotController, id, plotController?.plotInteractHeroList, "剧情互动角色/PlotInteractHero");

            // 任务目标角色 / MissionEventTargetHero
            if (lower == "任务目标角色" || lower == "missioneventtargethero")
            {
                WorldData worldData = GameController.Instance?.worldData;
                if (worldData == null || plotController?.nowMission == null || plotController.nowMission.missionTargetDatas == null)
                {
                    LoggerManager.Warning("ResolveHeroSource: 任务目标角色模式，但nowMission或missionTargetDatas为null");
                    return null;
                }
                if (plotController.nowMission.missionTargetDatas.Count == 0)
                {
                    LoggerManager.Warning("ResolveHeroSource: 任务目标角色模式，但missionTargetDatas为空");
                    return null;
                }
                MissionTargetData targetData = plotController.nowMission.missionTargetDatas[0];
                if (targetData == null)
                {
                    LoggerManager.Warning("ResolveHeroSource: 任务目标角色模式，但missionTargetDatas[0]为null");
                    return null;
                }
                int heroID = int.Parse(targetData.tirggerTargetID);
                return worldData.GetHero(heroID);
            }

            // 任务发起角色 / MissionEventSourceHero
            if (lower == "任务发起角色" || lower == "missioneventsourcehero")
            {
                GameController gc = GameController.Instance;
                if (gc == null || plotController?.nowMission == null)
                {
                    LoggerManager.Warning("ResolveHeroSource: 任务发起角色模式，但GameController或nowMission为null");
                    return null;
                }
                int heroID = plotController.nowMission.sourceHeroID;
                return gc.worldData?.GetHero(heroID);
            }

            // 比赛获胜角色列表:Index / FightMatchHeroFinalList
            if (lower.StartsWith("fightmatchherofinallist") || lower.StartsWith("比赛角色排名列表"))
                return ResolveIndexedHero(plotController, id, FightMatchController._instance?.HeroFinalList, "比赛角色排名列表/FightMatchHeroFinalList");

            // 非关键字
            return null;
        }

        /// <summary>
        /// 解析带索引的关键字角色列表（如 临时剧情角色:0、剧情互动角色-1）
        /// 无索引后缀时取 [0]，支持 : 、：、- 作为索引分隔符
        /// </summary>
        public static HeroData ResolveIndexedHero(PlotController plotController, string id, List<HeroData> heroList, string label)
        {
            if (heroList == null || heroList.Count == 0)
            {
                LoggerManager.Warning($"ResolveHeroSource: {label}模式，但列表为空或null");
                return null;
            }

            int index = 0;
            // 尝试解析 ":Index" 后缀（支持 : ：- 三种分隔符）
            int colonPos = id.IndexOf('：');
            if (colonPos < 0)
                colonPos = id.IndexOf(':');
            if (colonPos < 0)
                colonPos = id.IndexOf('-');

            if (colonPos >= 0)
            {
                string indexStr = id.Substring(colonPos + 1);
                if (!int.TryParse(indexStr, out index))
                {
                    LoggerManager.Warning($"ResolveHeroSource: {label}索引解析失败: {indexStr}");
                    return null;
                }
            }

            if (index < 0 || index >= heroList.Count)
            {
                LoggerManager.Warning($"ResolveHeroSource: {label}索引越界: {index}, 列表长度: {heroList.Count}");
                return null;
            }

            return heroList[index];
        }

        /// <summary>
        /// 解析角色ID字符串为HeroData实例
        /// 支持: 空→defaultHero/targetInteractHero / 关键字(忽略大小写,支持中文别名) / int ID / string ID(角色名)
        /// 关键字: player/玩家, targetInteractHero/targetHero/目标互动角色, sourceInteractHero/sourceHero/源互动角色,
        ///         chooseHero/选中角色, 临时剧情角色:Index, 剧情互动角色:Index,
        ///         任务目标角色, 任务发起角色
        /// 空值时: 优先返回 defaultHero，若 defaultHero 为 null 则返回 targetInteractHero
        /// </summary>
        public static HeroData ResolveHeroId(PlotController plotController, string heroIdRaw, HeroData defaultHero = null)
        {
            if (string.IsNullOrEmpty(heroIdRaw))
                return defaultHero ?? plotController?.targetInteractHero;

            // 先走关键字解析
            HeroData result = ResolveHeroSource(plotController, heroIdRaw);
            if (result != null)
                return result;

            // 尝试 int ID 或角色名
            return GetHeroDataById(plotController, heroIdRaw.Trim());
        }

        /// <summary>
        /// 解析任务来源字符串为 MissionData 实例。
        /// 支持: 空/nowMission -> PlotController.nowMission,
        ///       forceMission -> 玩家当前势力任务,
        ///       speMissionID-1001 / speMissionID=1001 / speMissionID:1001 -> 按玩家任务 speMissionID 查找,
        ///       纯整数 -> 按玩家任务 id 查找,
        ///       其他文本 -> 按玩家任务 name 查找。
        /// </summary>
        public static MissionData ResolveMissionSource(PlotController plotController, string sourceRaw, MissionData defaultMission = null)
        {
            string source = (sourceRaw ?? "").Trim();
            if (string.IsNullOrEmpty(source) || source.Equals("nowMission", StringComparison.OrdinalIgnoreCase) || source == "当前任务")
            {
                return defaultMission ?? plotController?.nowMission;
            }

            WorldData worldData = GetWorldData();
            HeroData player = worldData?.Player();
            if (player == null)
            {
                LoggerManager.Warning($"ResolveMissionSource: 玩家数据为空，无法解析任务来源 \"{source}\"");
                return null;
            }

            if (source.Equals("forceMission", StringComparison.OrdinalIgnoreCase) || source == "势力任务")
            {
                if (player.forceMission == null)
                {
                    LoggerManager.Warning("ResolveMissionSource: 玩家当前势力任务 forceMission 为空");
                    return null;
                }

                return player.forceMission;
            }

            if (TryParseSpeMissionIdSource(source, out int speMissionID))
            {
                return FindPlayerMission(player, mission => mission != null && mission.speMissionID == speMissionID, $"speMissionID={speMissionID}");
            }

            if (int.TryParse(source, out int missionID))
            {
                return FindPlayerMission(player, mission => mission != null && mission.id == missionID, $"id={missionID}");
            }

            return FindPlayerMission(player, mission => mission != null && mission.name == source, $"name={source}");
        }

        private static bool TryParseSpeMissionIdSource(string source, out int speMissionID)
        {
            speMissionID = 0;
            if (string.IsNullOrWhiteSpace(source))
                return false;

            string trimmed = source.Trim();
            string lower = trimmed.ToLowerInvariant();
            const string key = "spemissionid";
            if (!lower.StartsWith(key))
                return false;

            if (trimmed.Length <= key.Length)
                return false;

            char separator = trimmed[key.Length];
            if (separator != '-' && separator != '=' && separator != ':')
                return false;

            string valueText = trimmed.Substring(key.Length + 1).Trim();
            if (!int.TryParse(valueText, out speMissionID))
            {
                LoggerManager.Warning($"ResolveMissionSource: speMissionID格式错误 \"{source}\"，需写为 speMissionID-数字 / speMissionID=数字 / speMissionID:数字");
                return false;
            }

            return true;
        }

        private static MissionData FindPlayerMission(HeroData player, Func<MissionData, bool> predicate, string label)
        {
            if (player == null || player.missions == null)
            {
                LoggerManager.Warning($"ResolveMissionSource: 玩家任务列表为空，无法按 {label} 查找");
                return null;
            }

            for (int i = 0; i < player.missions.Count; i++)
            {
                MissionData mission = player.missions[i];
                if (predicate(mission))
                    return mission;
            }

            LoggerManager.Warning($"ResolveMissionSource: 未找到玩家任务 {label}");
            return null;
        }

        /// <summary>
        /// 获取玩家角色HeroData
        /// </summary>
        public static HeroData GetPlayerHero()
        {
            GameController instance = GameController.Instance;
            if (instance == null) return null;
            WorldData worldData = instance.worldData;
            if (worldData == null) return null;
            return worldData.Player();
        }

        /// <summary>
        /// 解析剧情数据库 ID，支持数字 ID 与 TheBookOfLong 符号 ID。
        /// </summary>
        public static bool TryResolvePlotDataBaseId(string rawId, out int plotID)
        {
            plotID = -1;

            if (int.TryParse(rawId, out plotID))
            {
                return true;
            }

            return SymbolicIdService.TryResolveId(rawId, out plotID);
        }

        /// <summary>
        /// 根据 PlotData.plotID 查找剧情数据，而不是只依赖 PlotDataBase 的字典 key。
        /// </summary>
        public static bool TryGetPlotDataByPlotId(Dictionary<int, PlotData> plotDataBase, int plotID, out PlotData plotData)
        {
            plotData = null;
            if (plotDataBase == null)
            {
                return false;
            }

            if (plotDataBase.ContainsKey(plotID))
            {
                plotData = plotDataBase[plotID];
                if (plotData != null)
                {
                    return true;
                }
            }

            foreach (int key in plotDataBase.Keys)
            {
                if (key == plotID)
                {
                    continue;
                }

                PlotData current = plotDataBase[key];
                if (current != null && current.plotID == plotID)
                {
                    plotData = current;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 通过角色ID字符串获取HeroData实例
        /// 如果ID可转为int，使用worldData.GetHero获取；否则使用PlotController.GetHeroData获取
        /// </summary>
        public static HeroData GetHeroDataById(PlotController plotController, string heroId, HeroData defaultHero = null)
        {
            if (string.IsNullOrEmpty(heroId)) return defaultHero;

            if (int.TryParse(heroId, out int heroIdInt))
            {
                GameController instance = GameController.Instance;
                if (instance == null) return defaultHero;

                WorldData worldData = instance.worldData;
                if (worldData == null) return defaultHero;

                return worldData.GetHero(heroIdInt);
            }
            else if (plotController != null)
            {
                return plotController.GetHeroData(PlotTargetHeroType.HeroName, heroId, defaultHero);
            }
            else
            {
                return defaultHero;
            }
        }

        /// <summary>
        /// 根据技能来源关键字解析KungfuSkillLvData
        /// 支持关键字: plotInteractSkill/剧情交互技能, chooseSkill/选中技能
        /// 支持格式: HeroSkill-HeroID-SkillID（从角色武学列表按技能ID查找）
        ///   HeroID: 通过ResolveHeroId解析（角色ID/名称/关键字）
        ///   SkillID: 技能ID（int）
        /// 默认(空): plotInteractSkill
        /// </summary>
        public static KungfuSkillLvData ResolveKungfuSkillSource(PlotController plotController, string sourceRaw)
        {
            string trimmed = (sourceRaw ?? "").Trim();
            if (string.IsNullOrEmpty(trimmed))
                return plotController != null ? plotController.plotInteractSkill : null;

            // 按 分隔符 分割，取首段作为关键字判断
            char[] separators = { '=', ':', '-' };
            string[] parts = trimmed.Split(separators);
            string lower = parts[0].ToLower();

            switch (lower)
            {
                case "plotinteractskill":
                case "剧情交互技能":
                case "剧情交互武学":
                    return plotController != null ? plotController.plotInteractSkill : null;
                case "chooseskill":
                case "chosenskill":
                case "选中技能":
                case "选中武学":
                    {
                        ChooseController cc = ChooseController._instance;
                        if (cc == null || cc.chooseResult == null) return null;
                        SkillIconController ctrl = cc.chooseResult.GetComponent<SkillIconController>();
                        return ctrl != null ? ctrl.skillLvData : null;
                    }
                case "heroskill":
                    {
                        if (parts.Length < 3)
                        {
                            LoggerManager.Warning($"  KungfuSkillLvData查询: HeroSkill格式参数不足，需要 HeroSkill-HeroID-SkillID");
                            return null;
                        }

                        HeroData hero = ResolveHeroId(plotController, parts[1]);
                        if (hero == null)
                        {
                            LoggerManager.Warning($"  KungfuSkillLvData查询: HeroSkill格式 - 未找到角色 \"{parts[1]}\"");
                            return null;
                        }

                        if (!int.TryParse(parts[2], out int skillID))
                        {
                            LoggerManager.Warning($"  KungfuSkillLvData查询: HeroSkill格式 - 技能ID \"{parts[2]}\" 无法解析为整数");
                            return null;
                        }

                        var kungfuSkills = hero.kungfuSkills;
                        if (kungfuSkills == null)
                        {
                            LoggerManager.Warning($"  KungfuSkillLvData查询: HeroSkill格式 - 角色 {hero.heroName} 的武学列表为空");
                            return null;
                        }

                        KungfuSkillLvData skill = null;
                        for (int i = 0; i < kungfuSkills.Count; i++)
                        {
                            KungfuSkillLvData s = kungfuSkills[i];
                            if (s != null && s.skillID == skillID)
                            {
                                skill = s;
                                break;
                            }
                        }

                        if (skill == null)
                            LoggerManager.Warning($"  KungfuSkillLvData查询: HeroSkill格式 - 角色 {hero.heroName} 未找到技能ID={skillID}");

                        return skill;
                    }
                default:
                    LoggerManager.Warning($"  KungfuSkillLvData查询: 未知的技能来源 \"{sourceRaw}\"，支持: plotInteractSkill/剧情交互技能/chooseSkill/选中技能/HeroSkill-HeroID-SkillID");
                    return null;
            }
        }

        /// <summary>
        /// 根据物品来源关键字解析ItemData
        /// 支持关键字: chooseItem/选中物品, plotInteractItem/剧情交互物品, playerAuctionItem/玩家拍卖物品
        /// 支持格式: HeroItem-HeroID-ItemID-IncludeStorage（从角色背包/仓库按物品ID查找）
        ///   HeroID: 通过ResolveHeroId解析（角色ID/名称/关键字）
        ///   ItemID: 物品ID（int）
        ///   IncludeStorage: 可选，True/1时背包找不到则到selfStorage查找
        /// 默认(空或未识别): plotInteractItem
        /// </summary>
        public static ItemData ResolveItemSource(PlotController plotController, string sourceRaw)
        {
            string trimmed = (sourceRaw ?? "").Trim();
            if (string.IsNullOrEmpty(trimmed))
                return plotController != null ? plotController.plotInteractItem : null;

            // 按 分隔符 分割，取首段作为关键字判断
            char[] separators = { '=', ':', '-' };
            string[] parts = trimmed.Split(separators);
            string lower = parts[0].ToLower();

            switch (lower)
            {
                case "plotinteractitem":
                case "剧情交互物品":
                    return plotController != null ? plotController.plotInteractItem : null;
                case "chooseitem":
                case "chosenitem":
                case "选中物品":
                    {
                        ChooseController cc = ChooseController._instance;
                        if (cc == null || cc.chooseResult == null) return null;
                        ItemIconController ctrl = cc.chooseResult.GetComponent<ItemIconController>();
                        return ctrl != null ? ctrl.itemData : null;
                    }
                case "playerauctionitem":
                case "玩家拍卖物品":
                    {
                        WorldData worldData = GetWorldData();
                        return worldData != null ? worldData.PlayerAuctionItem : null;
                    }
                case "heroitem":
                    {
                        if (parts.Length < 3)
                        {
                            LoggerManager.Warning($"  ItemData查询: HeroItem格式参数不足，需要 HeroItem-HeroID-ItemID-IncludeStorage(可选)");
                            return null;
                        }

                        HeroData hero = ResolveHeroId(plotController, parts[1]);
                        if (hero == null)
                        {
                            LoggerManager.Warning($"  ItemData查询: HeroItem格式 - 未找到角色 \"{parts[1]}\"");
                            return null;
                        }

                        if (!int.TryParse(parts[2], out int itemID))
                        {
                            LoggerManager.Warning($"  ItemData查询: HeroItem格式 - 物品ID \"{parts[2]}\" 无法解析为整数");
                            return null;
                        }

                        bool includeStorage = parts.Length >= 4
                            && (parts[3].Equals("True", System.StringComparison.OrdinalIgnoreCase) || parts[3] == "1");

                        // 先从背包查找
                        ItemData item = FindItemInList(hero.itemListData, itemID);

                        // 背包未找到且启用IncludeStorage，则从仓库查找
                        if (item == null && includeStorage)
                            item = FindItemInList(hero.selfStorage, itemID);

                        if (item == null)
                            LoggerManager.Warning($"  ItemData查询: HeroItem格式 - 角色 {hero.heroName} 未找到物品ID={itemID}");

                        return item;
                    }
                case "fightmatchrewardlist":
                case "fightmatchrewards":
                case "比赛奖品列表":
                    {
                        FightMatchController fmc = FightMatchController._instance;
                        if (fmc == null || fmc.rewardList == null || fmc.rewardList.Count == 0)
                        {
                            LoggerManager.Warning($"  ItemData查询: fightMatchRewardList - FightMatchController实例或rewardList为空");
                            return null;
                        }
                        int index = 0;
                        if (parts.Length >= 2 && !int.TryParse(parts[1], out index))
                        {
                            LoggerManager.Warning($"  ItemData查询: fightMatchRewardList - 索引 \"{parts[1]}\" 无法解析为整数");
                            return null;
                        }
                        if (index < 0 || index >= fmc.rewardList.Count)
                        {
                            LoggerManager.Warning($"  ItemData查询: fightMatchRewardList - 索引越界: {index}, 列表长度: {fmc.rewardList.Count}");
                            return null;
                        }
                        return fmc.rewardList[index];
                    }
                case "tempplotshop":
                case "临时剧情商店":
                case "tempplotshopitems":
                case "临时剧情商店物品":
                    {
                        if (plotController == null || plotController.tempPlotShop == null || plotController.tempPlotShop.allItem == null)
                        {
                            LoggerManager.Warning($"  ItemData查询: tempPlotShopItems - PlotController或tempPlotShop为空");
                            return null;
                        }
                        var allItem = plotController.tempPlotShop.allItem;
                        if (allItem.Count == 0)
                        {
                            LoggerManager.Warning($"  ItemData查询: tempPlotShopItems - 临时剧情商店物品列表为空");
                            return null;
                        }
                        int index = 0;
                        if (parts.Length >= 2 && !int.TryParse(parts[1], out index))
                        {
                            LoggerManager.Warning($"  ItemData查询: tempPlotShopItems - 索引 \"{parts[1]}\" 无法解析为整数");
                            return null;
                        }
                        if (index < 0 || index >= allItem.Count)
                        {
                            LoggerManager.Warning($"  ItemData查询: tempPlotShopItems - 索引越界: {index}, 列表长度: {allItem.Count}");
                            return null;
                        }
                        return allItem[index];
                    }
                default:
                    LoggerManager.Warning($"  ItemData查询: 未知的物品来源 \"{sourceRaw}\"，支持: chooseItem/plotInteractItem/playerAuctionItem/HeroItem-HeroID-ItemID-IncludeStorage/fightMatchRewardList-Index/tempPlotShopItems-Index");
                    return null;
            }
        }

        /// <summary>
        /// 在ItemListData中按物品ID查找ItemData
        /// </summary>
        public static ItemData FindItemInList(ItemListData itemListData, int itemID)
        {
            if (itemListData == null || itemListData.allItem == null)
                return null;
            for (int i = 0; i < itemListData.allItem.Count; i++)
            {
                ItemData item = itemListData.allItem[i];
                if (item != null && item.itemID == itemID)
                    return item;
            }
            return null;
        }

        /// <summary>
        /// 解析选择类型（支持int、枚举名、中文别名）
        /// </summary>
        public static ChooseType ParseChooseType(string str)
        {
            if (int.TryParse(str, out int val))
                return (ChooseType)val;

            switch (str.ToLower().Trim())
            {
                case "heroskill":
                case "武学":
                case "技能":
                    return ChooseType.HeroSkill;
                case "heroitem":
                case "物品":
                    return ChooseType.HeroItem;
                case "hero":
                case "角色":
                    return ChooseType.Hero;
                default:
                    LoggerManager.Warning($"ParseChooseType: 无法识别的选择类型 \"{str}\"，默认使用Hero");
                    return ChooseType.Hero;
            }
        }

        /// <summary>
        /// 解析筛选类型（支持int、枚举名）
        /// </summary>
        public static ChooseFilterType ParseChooseFilterType(string str)
        {
            if (int.TryParse(str, out int val))
                return (ChooseFilterType)val;

            if (Enum.TryParse<ChooseFilterType>(str, true, out ChooseFilterType result))
                return result;

            LoggerManager.Warning($"ParseChooseFilterType: 无法识别的筛选类型 \"{str}\"，默认使用None");
            return ChooseFilterType.None;
        }


        /// <summary>
        /// 解析物品列表数据
        /// 格式: "类型:参数"
        ///   -1或空: 临时剧情商店(PlotController.tempPlotShop)
        ///   0: 角色背包(HeroData.itemListData)，参数为角色ID/名称
        ///   1: 角色仓库(HeroData.selfStorage)，参数为角色ID/名称
        /// </summary>
        public static ItemListData ResolveItemListData(PlotController __instance, string fucName, string param, string sideLabel)
        {
            if (string.IsNullOrWhiteSpace(param))
            {
                // 默认使用临时剧情商店
                ItemListData tempShop = __instance.tempPlotShop;
                if (tempShop == null)
                {
                    LoggerManager.Warning($"{fucName}: {sideLabel}物品列表 - tempPlotShop为空");
                }
                return tempShop;
            }

            string trimmed = param.Trim();
            int colonIdx = trimmed.IndexOf(':');
            string typeStr = colonIdx >= 0 ? trimmed.Substring(0, colonIdx).Trim() : trimmed;
            string heroRef = colonIdx >= 0 ? trimmed.Substring(colonIdx + 1).Trim() : null;

            int listType;
            if (!int.TryParse(typeStr, out listType))
            {
                // 无法解析为数字时默认-1
                listType = -1;
            }

            switch (listType)
            {
                case -1:
                    {
                        // 临时剧情商店
                        ItemListData tempShop = __instance.tempPlotShop;
                        if (tempShop == null)
                        {
                            LoggerManager.Warning($"{fucName}: {sideLabel}物品列表 - tempPlotShop为空");
                        }
                        return tempShop;
                    }
                case 0:
                    {
                        // 角色背包
                        if (string.IsNullOrWhiteSpace(heroRef))
                        {
                            LoggerManager.Warning($"{fucName}: {sideLabel}物品列表 - 类型0(角色背包)需要指定角色ID/名称");
                            return null;
                        }
                        HeroData hero = CommonHandlers.ResolveHeroId(__instance, heroRef);
                        if (hero == null)
                        {
                            LoggerManager.Warning($"{fucName}: {sideLabel}物品列表 - 未找到角色 \"{heroRef}\"");
                            return null;
                        }
                        ItemListData itemList = hero.itemListData;
                        if (itemList == null)
                        {
                            LoggerManager.Warning($"{fucName}: {sideLabel}物品列表 - 角色 {hero.heroName} 的背包数据为空");
                        }
                        return itemList;
                    }
                case 1:
                    {
                        // 角色仓库
                        if (string.IsNullOrWhiteSpace(heroRef))
                        {
                            LoggerManager.Warning($"{fucName}: {sideLabel}物品列表 - 类型1(角色仓库)需要指定角色ID/名称");
                            return null;
                        }
                        HeroData hero = CommonHandlers.ResolveHeroId(__instance, heroRef);
                        if (hero == null)
                        {
                            LoggerManager.Warning($"{fucName}: {sideLabel}物品列表 - 未找到角色 \"{heroRef}\"");
                            return null;
                        }
                        ItemListData selfStorage = hero.selfStorage;
                        if (selfStorage == null)
                        {
                            LoggerManager.Warning($"{fucName}: {sideLabel}物品列表 - 角色 {hero.heroName} 的仓库数据为空");
                        }
                        return selfStorage;
                    }
                default:
                    LoggerManager.Warning($"{fucName}: {sideLabel}物品列表 - 不支持的类型 \"{listType}\"，支持: -1(临时剧情商店), 0(角色背包), 1(角色仓库)");
                    return null;
            }
        }

        /// <summary>
        /// 获取PlotEventLogData实例
        /// </summary>
        public static PlotEventLogData GetPlotEventLogData()
        {
            GameController instance = GameController.Instance;
            if (instance == null) return null;

            WorldData worldData = instance.worldData;
            if (worldData == null) return null;

            return worldData.PlotEventLog;
        }

        public void StartFightMatch(
            FightMatchController fightMatchController,
            FightMatchType _fightMatchType,
            List<HeroData> heroList,
            WatchFightType targetType,
            string _endMatchCallPlot,
            float _difficulty,
            bool _isForceMatch = false,
            bool _generateReward = true,
            List<ItemData> _rewardList = null,
            bool _isForceGroupMatch = false)
        {
            // 1. 赋值基本字段
            fightMatchController.fightMatchType = _fightMatchType;
            fightMatchController.watchFightType = targetType;

            // 2. 初始化 HeroFinalList
            fightMatchController.HeroFinalList = new List<HeroData>();

            // 3. 赋值难度和回调
            fightMatchController.matchDifficulty = _difficulty;
            fightMatchController.endMatchCallPlot = _endMatchCallPlot;
            fightMatchController.isForceMatch = _isForceMatch;
            fightMatchController.isForceGroupMatch = _isForceGroupMatch;

            // 4. 设置标题文本
            UnityEngine.Transform titleTransform = fightMatchController.fightMatchPanel.transform.Find("Title");
            Text titleText = titleTransform.GetComponent<Text>();

            if (!fightMatchController.isForceGroupMatch)
            {
                if (!fightMatchController.isForceMatch)
                {
                    // 普通比赛：根据难度取门派等级名称
                    string forceLvName = GlobalData.HeroFreeForceLvName[UnityEngine.Mathf.RoundToInt(fightMatchController.matchDifficulty * 0.5f)];
                    titleText.text = forceLvName + fightMatchController.GetMatchTypeName();
                }
                else
                {
                    // 门派赛：取玩家所属门派名称
                    HeroData player = GameController.Instance.worldData.Player();
                    ForceData force = player.GetForce(includeServant: true);
                    titleText.text = force.forceName + fightMatchController.GetMatchTypeName();
                }
            }
            // isForceGroupMatch == true 时标题为 StringLiteral_15049（推断为空字符串或默认标题）

            LTLocalization.SetText(titleText, titleText.text);

            // 5. 处理奖励
            if (!_generateReward)
            {
                // 不生成奖励：隐藏奖励面板
                UnityEngine.Transform rewardTransform = fightMatchController.fightMatchPanel.transform.Find("RewardItem");
                rewardTransform.gameObject.SetActive(false);
            }
            else
            {
                // 生成奖励：清空旧奖励列表
                fightMatchController.rewardList.Clear();

                UnityEngine.Transform rewardTransform = fightMatchController.fightMatchPanel.transform.Find("RewardItem");
                rewardTransform.gameObject.SetActive(true);

                // 若无自定义奖励列表，则自动生成
                if (_rewardList == null || _rewardList.Count == 0)
                {
                    _rewardList = FightMatchController.GenerateFightMatchRewardItemList(
                        fightMatchController.fightMatchType,
                        fightMatchController.matchDifficulty,
                        fightMatchController.isForceMatch,
                        fightMatchController.isForceGroupMatch);
                }
                fightMatchController.rewardList = _rewardList;

                // 为每个奖励物品创建图标
                for (int i = 0; i < 3; i++)
                {
                    UnityEngine.Transform slotTransform = rewardTransform.Find(i.ToString());
                    UnityEngine.GameObject iconObj = NGUITools.AddChild(
                        slotTransform,
                        GameObjectController._instance.itemIconPrefab);
                    fightMatchController.tempObj = iconObj;

                    ItemIconController itemIcon = iconObj.GetComponent<ItemIconController>();
                    itemIcon.itemData = fightMatchController.rewardList[i];
                    itemIcon.itemIconType = ItemIconType.Show;
                }
            }

            // 6. 启动比赛
            fightMatchController.SetRound(1);
            fightMatchController.StartCoroutine(fightMatchController.StartFightMatch(heroList));

            // 7. 播放音效
            UnityEngine.AudioClip clip = UnityEngine.Resources.Load<UnityEngine.AudioClip>("Sound/SoundEffect/紧密鼓点");
            if (clip != null)
            {
                NGUITools.PlaySound(clip);
            }
        }

        /// <summary>
        /// 按选取规则从角色列表中选取指定数量的角色
        /// </summary>
        /// <param name="heroes">候选角色列表（不会被修改）</param>
        /// <param name="count">需要选取的数量</param>
        /// <param name="rule">选取规则：-1或空=随机选取，0~6对应HeroListSortType枚举</param>
        /// <returns>选取后的角色列表</returns>
        public static List<HeroData> SelectHeroesByRule(List<HeroData> heroes, int count, int rule)
        {
            if (heroes == null || heroes.Count == 0 || count <= 0)
                return new List<HeroData>();

            count = UnityEngine.Mathf.Min(count, heroes.Count);

            if (rule >= 0)
            {
                // 按HeroListSortType枚举排序后取前count个
                List<HeroData> copyForSort = new List<HeroData>(heroes.Count);
                for (int i = 0; i < heroes.Count; i++)
                    copyForSort.Add(heroes[i]);
                List<HeroData> sorted = GlobalData.SortHeroList(copyForSort, (HeroListSortType)rule);
                List<HeroData> result = new List<HeroData>(count);
                for (int i = 0; i < count; i++)
                    result.Add(sorted[i]);
                return result;
            }
            else
            {
                // 随机选取：Fisher-Yates 部分洗牌
                List<HeroData> shuffled = new List<HeroData>(heroes.Count);
                for (int i = 0; i < heroes.Count; i++)
                    shuffled.Add(heroes[i]);
                for (int i = shuffled.Count - 1; i > 0 && i >= shuffled.Count - count; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    HeroData temp = shuffled[i];
                    shuffled[i] = shuffled[j];
                    shuffled[j] = temp;
                }
                List<HeroData> randomResult = new List<HeroData>(count);
                for (int i = shuffled.Count - count; i < shuffled.Count; i++)
                    randomResult.Add(shuffled[i]);
                return randomResult;
            }
        }


        #region 通用工具方法

        public static int ResolveToForceID(string raw, int defaultForceID = -1)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return defaultForceID;

            string value = raw.Trim();
            if (IsDefaultText(value))
                return defaultForceID;
            if (IsNoneText(value))
                return -1;

            WorldData world = GetWorldData();
            if (int.TryParse(value, out int forceID))
            {
                if (forceID < 0)
                    return forceID;
                if (world?.GetForce(forceID) != null || GameDataController._instance?.forceDataBase?.ContainsKey(forceID) == true)
                    return forceID;

                LoggerManager.Warning($"ResolveToForceID: forceID not found: {value}");
                return defaultForceID;
            }

            ForceData force = world?.GetForce(value);
            if (force != null)
                return force.forceID;

            GameDataController data = GameDataController._instance;
            if (data?.forceDataBase != null)
            {
                foreach (int key in data.forceDataBase.Keys)
                {
                    ForceData item = data.forceDataBase[key];
                    if (item != null && SameName(item.forceName, value))
                        return item.forceID;
                }
            }

            LoggerManager.Warning($"ResolveToForceID: force not found: {value}");
            return defaultForceID;
        }

        /// <summary>
        /// 解析势力来源关键字为ForceData实例（仅处理关键字，不处理int/string ID）。
        /// 支持: player/玩家。
        /// 非关键字时返回 null，由调用方决定后续处理。
        /// </summary>
        public static ForceData ResolveForceSource(PlotController plotController, string forceIdRaw)
        {
            if (string.IsNullOrWhiteSpace(forceIdRaw)) return null;

            string id = forceIdRaw.Trim();
            string lower = id.ToLowerInvariant();

            if (lower == "player" || lower == "玩家")
            {
                HeroData player = GetPlayerHero();
                return player?.GetForce(includeServant: true);
            }

            return null;
        }

        /// <summary>
        /// 解析势力ID字符串为ForceData实例。
        /// 支持: 关键字 / int ID / string ID(势力名)。
        /// </summary>
        public static ForceData ResolveForceId(PlotController plotController, string forceIdRaw, ForceData defaultForce = null)
        {
            if (string.IsNullOrWhiteSpace(forceIdRaw))
                return defaultForce;

            ForceData source = ResolveForceSource(plotController, forceIdRaw);
            if (source != null)
                return source;

            string value = forceIdRaw.Trim();
            if (IsDefaultText(value))
                return defaultForce;
            if (IsNoneText(value))
                return null;

            WorldData world = GetWorldData();
            if (int.TryParse(value, out int forceID))
            {
                if (forceID < 0)
                    return null;

                ForceData force = world?.GetForce(forceID);
                if (force != null)
                    return force;

                ForceData baseForce = null;
                if (GameDataController._instance?.forceDataBase?.ContainsKey(forceID) == true)
                    baseForce = GameDataController._instance.forceDataBase[forceID];
                if (baseForce != null)
                    return baseForce;

                LoggerManager.Warning($"ResolveForceId: forceID not found: {value}");
                return defaultForce;
            }

            ForceData byName = world?.GetForce(value);
            if (byName != null)
                return byName;

            GameDataController data = GameDataController._instance;
            if (data?.forceDataBase != null)
            {
                foreach (int key in data.forceDataBase.Keys)
                {
                    ForceData item = data.forceDataBase[key];
                    if (item != null && SameName(item.forceName, value))
                        return item;
                }
            }

            LoggerManager.Warning($"ResolveForceId: force not found: {value}");
            return defaultForce;
        }

        /// <summary>
        /// 解析区域来源关键字为AreaData实例（仅处理关键字，不处理int/string ID）。
        /// 支持: targetArea/目标地区, player/玩家, targetHero/targetInteractHero/目标互动角色,
        ///       sourceHero/sourceInteractHero/源互动角色。
        /// 非关键字时返回 null，由调用方决定后续处理。
        /// </summary>
        public static AreaData ResolveAreaSource(PlotController plotController, string areaIdRaw)
        {
            string id = areaIdRaw?.Trim();
            string lower = id?.ToLowerInvariant();

            if (string.IsNullOrEmpty(lower) || lower == "targetarea" || lower == "目标地区")
                return AreaController.Instance?.areaData;

            HeroData hero = ResolveHeroSource(plotController, id);
            return hero?.GetArea();
        }

        /// <summary>
        /// 解析区域ID字符串为AreaData实例。
        /// 支持: 空/targetArea/目标地区 / 角色来源关键字 / int ID / string ID(区域名)。
        /// </summary>
        public static AreaData ResolveAreaId(PlotController plotController, string areaIdRaw, AreaData defaultArea = null)
        {
            AreaData source = ResolveAreaSource(plotController, areaIdRaw);
            if (source != null)
                return source;

            if (string.IsNullOrWhiteSpace(areaIdRaw))
                return defaultArea;

            string value = areaIdRaw.Trim();
            if (IsDefaultText(value))
                return defaultArea;
            if (IsNoneText(value))
                return null;

            WorldData world = GetWorldData();
            if (int.TryParse(value, out int areaID))
            {
                if (areaID < 0)
                    return null;

                AreaData area = world?.GetArea(areaID);
                if (area != null)
                    return area;

                AreaData baseArea = null;
                if (GameDataController._instance?.areaDataBase?.ContainsKey(areaID) == true)
                    baseArea = GameDataController._instance.areaDataBase[areaID];
                if (baseArea != null)
                    return baseArea;

                LoggerManager.Warning($"ResolveAreaId: areaID not found: {value}");
                return defaultArea;
            }

            AreaData byName = world?.GetArea(value);
            if (byName != null)
                return byName;

            GameDataController data = GameDataController._instance;
            if (data?.areaDataBase != null)
            {
                foreach (int key in data.areaDataBase.Keys)
                {
                    AreaData item = data.areaDataBase[key];
                    if (item != null && SameName(item.areaName, value))
                        return item;
                }
            }

            LoggerManager.Warning($"ResolveAreaId: area not found: {value}");
            return defaultArea;
        }

        public static int ResolveToAreaID(string raw, int defaultAreaID = -1)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return defaultAreaID;

            string value = raw.Trim();
            if (IsDefaultText(value))
                return defaultAreaID;
            if (IsNoneText(value))
                return -1;

            WorldData world = GetWorldData();
            if (int.TryParse(value, out int areaID))
            {
                if (areaID < 0)
                    return areaID;
                if (world?.GetArea(areaID) != null || GameDataController._instance?.areaDataBase?.ContainsKey(areaID) == true)
                    return areaID;

                LoggerManager.Warning($"ResolveToAreaID: areaID not found: {value}");
                return defaultAreaID;
            }

            AreaData area = world?.GetArea(value);
            if (area != null)
                return area.areaID;

            GameDataController data = GameDataController._instance;
            if (data?.areaDataBase != null)
            {
                foreach (int key in data.areaDataBase.Keys)
                {
                    AreaData item = data.areaDataBase[key];
                    if (item != null && SameName(item.areaName, value))
                        return item.areaID;
                }
            }

            LoggerManager.Warning($"ResolveToAreaID: area not found: {value}");
            return defaultAreaID;
        }

        public static int ResolveSkillID(string raw, int defaultSkillID = -1)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return defaultSkillID;

            string value = raw.Trim();
            if (IsDefaultText(value) || IsNoneText(value))
                return defaultSkillID;

            GameDataController data = GameDataController._instance;
            if (int.TryParse(value, out int skillID))
            {
                if (data?.GetSkillDataBase(skillID) != null)
                    return skillID;

                LoggerManager.Warning($"ResolveSkillID: skillID not found: {value}");
                return defaultSkillID;
            }

            int resolvedID = data != null ? data.GetSkillID(value) : -1;
            if (resolvedID >= 0 && data?.GetSkillDataBase(resolvedID) != null)
                return resolvedID;

            if (data?.kungfuSkillDataBase != null)
            {
                foreach (int key in data.kungfuSkillDataBase.Keys)
                {
                    KungfuSkillData skill = data.kungfuSkillDataBase[key];
                    if (skill != null && (SameName(skill.name, value) || SameName(skill.Name(false), value)))
                        return skill.skillID;
                }
            }

            LoggerManager.Warning($"ResolveSkillID: skill not found: {value}");
            return defaultSkillID;
        }

        public static int ResolveTagID(string raw, int defaultTagID = -1)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return defaultTagID;

            string value = raw.Trim();
            if (IsDefaultText(value) || IsNoneText(value))
                return defaultTagID;

            GameDataController data = GameDataController._instance;
            if (int.TryParse(value, out int tagID))
            {
                if (data?.GetTagDataBase(tagID) != null)
                    return tagID;

                LoggerManager.Warning($"ResolveTagID: tagID not found: {value}");
                return defaultTagID;
            }

            int resolvedID = data != null ? data.GetTagID(value) : -1;
            if (resolvedID >= 0 && data?.GetTagDataBase(resolvedID) != null)
                return resolvedID;

            if (data?.heroTagDataBase != null)
            {
                foreach (int key in data.heroTagDataBase.Keys)
                {
                    HeroTagDataBase tag = data.heroTagDataBase[key];
                    if (tag != null && (SameName(tag.name, value) || SameName(tag.Name(), value)))
                        return tag.id;
                }
            }

            LoggerManager.Warning($"ResolveTagID: tag not found: {value}");
            return defaultTagID;
        }

        public static List<int> ResolveTagList(string raw)
        {
            List<int> result = new List<int>();
            foreach (string part in SplitValueList(raw, ';', '-'))
            {
                int tagID = ResolveTagID(part, -1);
                if (tagID >= 0 && !result.Contains(tagID))
                    result.Add(tagID);
            }
            return result;
        }

        public static List<int> ResolveFocusList(string raw, FocusListKind kind)
        {
            List<int> result = new List<int>();
            foreach (string part in SplitValueList(raw, '/', '-'))
            {
                int focusID = ResolveFocusID(part, kind, -1);
                if (focusID >= 0 && !result.Contains(focusID))
                    result.Add(focusID);
            }
            return result;
        }

        public static SexLimit ParseSexLimit(string raw, SexLimit defaultValue = SexLimit.None)
        {
            if (string.IsNullOrWhiteSpace(raw) || IsDefaultText(raw))
                return defaultValue;

            string value = raw.Trim();
            if (int.TryParse(value, out int intVal))
                return (SexLimit)intVal;

            switch (value)
            {
                case "无":
                    return SexLimit.None;
                case "男":
                    return SexLimit.Male;
                case "女":
                    return SexLimit.Female;
            }

            if (Enum.TryParse<SexLimit>(value, true, out SexLimit parsed))
                return parsed;

            LoggerManager.Warning($"ParseSexLimit: parse failed: {value}");
            return defaultValue;
        }

        public static int ParseNature(string raw, int defaultValue = -1)
        {
            if (string.IsNullOrWhiteSpace(raw) || IsDefaultText(raw))
                return defaultValue;
            if (int.TryParse(raw.Trim(), out int intVal))
                return intVal;

            int index = IndexOfTextList(GlobalData.NatureText, raw.Trim());
            if (index >= 0)
                return index;

            LoggerManager.Warning($"ParseNature: parse failed: {raw}");
            return defaultValue;
        }

        public static int ParseTalent(string raw, int defaultValue = -1)
        {
            if (string.IsNullOrWhiteSpace(raw) || IsDefaultText(raw))
                return defaultValue;
            if (int.TryParse(raw.Trim(), out int intVal))
                return intVal;

            int index = IndexOfTextList(GlobalData.TalentText, raw.Trim());
            if (index >= 0)
                return index;

            LoggerManager.Warning($"ParseTalent: parse failed: {raw}");
            return defaultValue;
        }

        public static float ParseHeroForceLv(string raw, float defaultValue = -1f)
        {
            if (string.IsNullOrWhiteSpace(raw) || IsDefaultText(raw))
                return defaultValue;
            if (float.TryParse(raw.Trim(), out float floatVal))
                return floatVal;

            string value = raw.Trim();
            int index = IndexOfTextList(GlobalData.HeroForceLvName, value);
            if (index >= 0) return index;
            index = IndexOfTextList(GlobalData.HeroFreeForceLvName, value);
            if (index >= 0) return index;
            index = IndexOfTextList(GlobalData.HeroBadForceLvName, value);
            if (index >= 0) return index;

            LoggerManager.Warning($"ParseHeroForceLv: parse failed: {raw}");
            return defaultValue;
        }

        public static float ParseChaos(string raw, float defaultValue = 0f)
        {
            if (string.IsNullOrWhiteSpace(raw) || IsDefaultText(raw))
                return defaultValue;
            if (float.TryParse(raw.Trim(), out float floatVal))
                return floatVal;

            string value = raw.Trim().ToLowerInvariant();
            switch (value)
            {
                case "守序":
                case "秩序":
                case "戒律":
                    return 0f;
                case "中立":
                case "平常":
                case "普通":
                    return 50f;
                case "混乱":
                case "无序":
                    return 100f;
                default:
                    LoggerManager.Warning($"ParseChaos: parse failed: {raw}");
                    return defaultValue;
            }
        }

        public static float ParseEvil(string raw, float defaultValue = 0f)
        {
            if (string.IsNullOrWhiteSpace(raw) || IsDefaultText(raw))
                return defaultValue;
            if (float.TryParse(raw.Trim(), out float floatVal))
                return floatVal;

            string value = raw.Trim().ToLowerInvariant();
            switch (value)
            {
                case "善":
                case "善良":
                case "正派":
                    return 0f;
                case "中立":
                case "平常":
                case "普通":
                    return 50f;
                case "恶":
                case "邪恶":
                case "冷酷":
                    return 100f;
                default:
                    LoggerManager.Warning($"ParseEvil: parse failed: {raw}");
                    return defaultValue;
            }
        }

        public static bool ParseBool(string raw, bool defaultValue = false)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return defaultValue;

            string lower = raw.Trim().ToLowerInvariant();
            if (lower == "true" || lower == "1" || lower == "yes" || lower == "y" || lower == "on")
                return true;
            if (lower == "false" || lower == "0" || lower == "no" || lower == "n" || lower == "off")
                return false;

            return defaultValue;
        }

        public static bool GetBoolArg(string[] args, int index, bool defaultValue = false)
        {
            return args != null && args.Length > index
                ? ParseBool(args[index], defaultValue)
                : defaultValue;
        }

        public static int GetIntArg(string[] args, int index, int defaultValue = 0)
        {
            return args != null && args.Length > index && int.TryParse(args[index], out int value)
                ? value
                : defaultValue;
        }

        public static float GetFloatArg(string[] args, int index, float defaultValue = 0f)
        {
            return args != null && args.Length > index && float.TryParse(args[index], out float value)
                ? value
                : defaultValue;
        }

        public static bool TryGetIntArg(string[] args, int index, string actionName, string argName, out int value)
        {
            value = 0;
            if (args == null || args.Length <= index || !int.TryParse(args[index], out value))
            {
                LoggerManager.Warning($"{actionName}: 参数不足或无效，缺少{argName}");
                return false;
            }

            return true;
        }

        public static bool TryGetFloatArg(string[] args, int index, string actionName, string argName, out float value)
        {
            value = 0f;
            if (args == null || args.Length <= index || !float.TryParse(args[index], out value))
            {
                LoggerManager.Warning($"{actionName}: 参数不足或无效，缺少{argName}");
                return false;
            }

            return true;
        }

        public static bool TryGetHeroIdArg(PlotController plotController, string[] args, int index, string actionName, out int heroID)
        {
            heroID = 0;
            if (args == null || args.Length <= index || string.IsNullOrWhiteSpace(args[index]))
            {
                LoggerManager.Warning($"{actionName}: 参数不足，缺少目标角色ID");
                return false;
            }

            HeroData target = ResolveHeroId(plotController, args[index]);
            if (target == null)
            {
                LoggerManager.Warning($"{actionName}: 未找到目标角色 \"{args[index]}\"");
                return false;
            }

            heroID = target.heroID;
            return true;
        }

        /// <summary>
        /// 通过反射写入对象的公开实例属性或字段。仅支持简单值类型、字符串和枚举。
        /// </summary>
        public static bool TryWriteObjectMemberValue(object target, string typeName, string memberName, string rawValue, out string oldValue, out string newValue)
        {
            oldValue = "";
            newValue = "";

            if (target == null)
            {
                LoggerManager.Warning($"{typeName}写入: 目标对象为空");
                return false;
            }

            if (string.IsNullOrWhiteSpace(memberName))
            {
                LoggerManager.Warning($"{typeName}写入: 成员名为空");
                return false;
            }

            Type targetType = target.GetType();
            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

            PropertyInfo prop = targetType.GetProperty(memberName, flags);
            if (prop != null)
            {
                if (!prop.CanWrite)
                {
                    LoggerManager.Warning($"{typeName}写入: 属性 {memberName} 不可写");
                    return false;
                }

                if (!TryConvertStringToMemberValue(rawValue, prop.PropertyType, out object convertedValue, out string error))
                {
                    LoggerManager.Warning($"{typeName}写入: 属性 {memberName} 类型转换失败，目标类型={prop.PropertyType.Name}, 值=\"{rawValue}\", 原因={error}");
                    return false;
                }

                try
                {
                    object oldObj = prop.GetValue(target);
                    oldValue = FormatObjectMemberValue(oldObj, prop.PropertyType);
                    prop.SetValue(target, convertedValue);
                    object newObj = prop.GetValue(target);
                    newValue = FormatObjectMemberValue(newObj, prop.PropertyType);
                    return true;
                }
                catch (Exception e)
                {
                    LoggerManager.Error($"{typeName}写入: 设置属性 {memberName} 失败: {e.Message}");
                    return false;
                }
            }

            FieldInfo field = targetType.GetField(memberName, flags);
            if (field != null)
            {
                if (field.IsInitOnly)
                {
                    LoggerManager.Warning($"{typeName}写入: 字段 {memberName} 为只读字段");
                    return false;
                }

                if (!TryConvertStringToMemberValue(rawValue, field.FieldType, out object convertedValue, out string error))
                {
                    LoggerManager.Warning($"{typeName}写入: 字段 {memberName} 类型转换失败，目标类型={field.FieldType.Name}, 值=\"{rawValue}\", 原因={error}");
                    return false;
                }

                try
                {
                    object oldObj = field.GetValue(target);
                    oldValue = FormatObjectMemberValue(oldObj, field.FieldType);
                    field.SetValue(target, convertedValue);
                    object newObj = field.GetValue(target);
                    newValue = FormatObjectMemberValue(newObj, field.FieldType);
                    return true;
                }
                catch (Exception e)
                {
                    LoggerManager.Error($"{typeName}写入: 设置字段 {memberName} 失败: {e.Message}");
                    return false;
                }
            }

            LoggerManager.Warning($"{typeName}写入: 未找到公开实例属性或字段 \"{memberName}\"");
            return false;
        }

        public static bool TryConvertStringToMemberValue(string rawValue, Type targetType, out object value, out string error)
        {
            value = null;
            error = "";

            Type valueType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            string raw = rawValue ?? "";
            string text = raw.Trim();
            string numberText = text.Replace("负", "-");

            if (valueType == typeof(string))
            {
                value = raw;
                return true;
            }

            if (valueType == typeof(bool))
            {
                if (TryParseStrictBool(text, out bool boolValue))
                {
                    value = boolValue;
                    return true;
                }

                error = "布尔值仅支持 0/1/true/false";
                return false;
            }

            if (valueType.IsEnum)
            {
                if (int.TryParse(numberText, out int enumInt))
                {
                    value = Enum.ToObject(valueType, enumInt);
                    return true;
                }

                try
                {
                    value = Enum.Parse(valueType, text, true);
                    return true;
                }
                catch
                {
                    error = "枚举值必须为数字或枚举名";
                    return false;
                }
            }

            if (valueType == typeof(int))
            {
                if (int.TryParse(numberText, out int intValue)) { value = intValue; return true; }
                error = "不是有效整数";
                return false;
            }

            if (valueType == typeof(float))
            {
                if (float.TryParse(numberText, out float floatValue)) { value = floatValue; return true; }
                error = "不是有效浮点数";
                return false;
            }

            if (valueType == typeof(double))
            {
                if (double.TryParse(numberText, out double doubleValue)) { value = doubleValue; return true; }
                error = "不是有效浮点数";
                return false;
            }

            if (valueType == typeof(long))
            {
                if (long.TryParse(numberText, out long longValue)) { value = longValue; return true; }
                error = "不是有效长整数";
                return false;
            }

            error = $"不支持写入类型 {valueType.Name}";
            return false;
        }

        public static bool TryParseStrictBool(string raw, out bool value)
        {
            value = false;
            if (string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }

            if (string.Equals(raw, "0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }

            return false;
        }

        public static string FormatObjectMemberValue(object value, Type valueType)
        {
            if (value == null) return "null";

            Type type = Nullable.GetUnderlyingType(valueType) ?? valueType;
            if (type == typeof(bool))
                return (bool)value ? "1" : "0";

            if (type.IsEnum)
                return Convert.ToInt32(value).ToString();

            if (value is float f)
                return f.ToString("G");

            if (value is double d)
                return d.ToString("G");

            return value.ToString();
        }

        public static int ResolveFocusID(string raw, FocusListKind kind, int defaultValue)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return defaultValue;

            string value = raw.Trim();
            if (int.TryParse(value, out int intVal))
                return intVal;

            List<string> source = kind == FocusListKind.Kungfu ? GlobalData.FightSkillName : GlobalData.LivingSkillName;
            int index = IndexOfTextList(source, value);
            if (index >= 0)
                return index;

            LoggerManager.Warning($"ResolveFocusList: focus not found: {value}");
            return defaultValue;
        }

        public static string[] SplitValueList(string raw, params char[] separators)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new string[0];

            string normalized = raw.Trim()
                .Replace('；', ';')
                .Replace('、', '/')
                .Replace('，', '/')
                .Replace('／', '/')
                .Replace('－', '-')
                .Replace('—', '-');

            return normalized.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        }

        public static int IndexOfTextList(List<string> source, string value)
        {
            if (source == null || string.IsNullOrWhiteSpace(value))
                return -1;

            for (int i = 0; i < source.Count; i++)
            {
                if (SameName(source[i], value))
                    return i;
            }

            return -1;
        }

        public static bool SameName(string left, string right)
        {
            return string.Equals((left ?? "").Trim(), (right ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsNoneText(string value)
        {
            string lower = (value ?? "").Trim().ToLowerInvariant();
            return lower == "-1" || lower == "none" || lower == "null" || lower == "无";
        }

        public static bool IsDefaultText(string value)
        {
            string lower = (value ?? "").Trim().ToLowerInvariant();
            return lower == "" || lower == "default" || lower == "keep" || lower == "默认" || lower == "保留";
        }

        #endregion

    }
}
