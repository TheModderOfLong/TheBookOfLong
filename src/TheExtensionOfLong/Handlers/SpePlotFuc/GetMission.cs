using Il2Cpp;
using Il2CppInterop.Runtime;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 统一获取任务指令，支持所有任务类型和多种查找方式
    /// 格式: GetMission*查找关键字#查找属性(可选)#任务类型(可选)#命名参数...
    /// 
    /// 【查找属性】支持:
    ///   "Index" 或 0 — 按列表索引查找（默认）
    ///   "name"  或 1 — 按 MissionData.name 查找
    ///   "speMissionID" 或 2 — 按 MissionData.speMissionID 查找
    ///   不指定时默认为 "Index"
    /// 
    /// 【任务类型】支持:
    ///   "Main"/"主线"       或 0 — MainMissionDataBase
    ///   "Branch"/"支线"     或 1 — BranchMissionDataBase
    ///   "Little"/"小型"     或 2 — LittleMissionDataBase
    ///   "bounty"/"悬赏"     或 3 — bountyMissionDataBase
    ///   "SpeKiller"/"杀手"  或 4 — SpeKillerMissionDataBase
    ///   "TreasureMap"/"藏宝图" 或 5 — TreasureMapMissionDataBase
    /// 
    /// 当查找属性为 "Index" 时，任务类型必须指定；
    /// 当查找属性为 "name" 或 "speMissionID" 时，任务类型可省略（从所有任务库中查找）
    /// 
    /// 示例: GetMission*0#Index#Main                 → 获取主线任务列表中索引为0的任务
    ///       GetMission*任务名称#name#Branch          → 获取支线任务中name匹配的任务
    ///       GetMission*101#speMissionID              → 从所有任务库中查找speMissionID=101的任务
    ///       GetMission*5#0#2                         → 获取小任务列表中索引为5的任务
    ///       GetMission*某任务#1                      → 从所有任务库中查找name="某任务"的任务
    ///       GetMission*5#Index#bounty#name=追捕恶徒-郭勇锐-1#targetID=88 → 获取悬赏模板索引5，并覆写实例字段
    /// </summary>
    [SpePlotFuc("GetMission")]
    public static class SpePlotFucGetMission
    {
        private struct GetMissionParams
        {
            public string name;
            public bool hasName;
            public int leftTime;
            public bool hasLeftTime;
            public float difficulty;
            public bool hasDifficulty;
            public float difficultyRate;
            public bool hasDifficultyRate;
            public float rewardRate;
            public bool hasRewardRate;
            public float treasureLv;
            public bool hasTreasureLv;
            public int sourceHeroID;
            public bool hasSourceHeroID;
            public int sourceForceID;
            public bool hasSourceForceID;
            public int missionHeroID;
            public bool hasMissionHeroID;
            public BountyType missionBountyType;
            public bool hasMissionBountyType;
            public bool missionDisableQuickTravel;
            public bool hasMissionDisableQuickTravel;
            public bool missionHideTargetPlace;
            public bool hasMissionHideTargetPlace;
            public string missionHideTargetPlaceString;
            public bool hasMissionHideTargetPlaceString;
            public bool noAutoFinish;
            public bool hasNoAutoFinish;
            public int targetIndex;
            public int needIndex;
            public string describe;
            public bool hasDescribe;
            public MissionTriggerType missionTriggerType;
            public bool hasMissionTriggerType;
            public MissionTargetAreaTypeLimit missionTargetAreaTypeLimit;
            public bool hasMissionTargetAreaTypeLimit;
            public string tirggerTargetID;
            public bool hasTirggerTargetID;
            public int missionTargetFinishCallPlotID;
            public bool hasMissionTargetFinishCallPlotID;
            public MissionTargetType missionTargetType;
            public bool hasMissionTargetType;
            public string missionTargetID;
            public bool hasMissionTargetID;
            public int missionResourceNeed;
            public bool hasMissionResourceNeed;
            public float missionNumCount;
            public bool hasMissionNumCount;
            public float missionNumNeed;
            public bool hasMissionNumNeed;

            public static GetMissionParams Default => new GetMissionParams
            {
                targetIndex = 0,
                needIndex = 0
            };
        }

        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 1 || string.IsNullOrWhiteSpace(fucParams[0]))
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*查找关键字#查找属性(可选)#任务类型(可选)#命名参数...]");
                return;
            }

            string keyWord = fucParams[0];
            string searchAttrRaw = fucParams.Length > 1 && !string.IsNullOrWhiteSpace(fucParams[1]) ? fucParams[1] : "Index";
            string missionTypeRaw = fucParams.Length > 2 && !string.IsNullOrWhiteSpace(fucParams[2]) ? fucParams[2] : null;
            GetMissionParams customParams = ParseNamedParams(fucParams, 3, fucName);

            // 解析查找属性
            int searchAttr;
            string searchAttrLower = searchAttrRaw.ToLower().Trim();
            if (searchAttrLower == "index" || searchAttrRaw == "0")
                searchAttr = 0;
            else if (searchAttrLower == "name" || searchAttrRaw == "1")
                searchAttr = 1;
            else if (searchAttrLower == "spemissionid" || searchAttrRaw == "2")
                searchAttr = 2;
            else
            {
                LoggerManager.Warning($"{fucName}: 不支持的查找属性 \"{searchAttrRaw}\"，支持: Index/0, name/1, speMissionID/2");
                return;
            }

            // 解析任务类型
            int? missionType = null;
            if (missionTypeRaw != null)
            {
                string typeLower = missionTypeRaw.ToLower().Trim();
                if (typeLower == "main" || typeLower == "主线" || missionTypeRaw == "0")
                    missionType = 0;
                else if (typeLower == "branch" || typeLower == "支线" || missionTypeRaw == "1")
                    missionType = 1;
                else if (typeLower == "little" || typeLower == "小型" || missionTypeRaw == "2")
                    missionType = 2;
                else if (typeLower == "bounty" || typeLower == "悬赏" || missionTypeRaw == "3")
                    missionType = 3;
                else if (typeLower == "spekiller" || typeLower == "杀手" || missionTypeRaw == "4")
                    missionType = 4;
                else if (typeLower == "treasuremap" || typeLower == "藏宝图" || missionTypeRaw == "5")
                    missionType = 5;
                else
                {
                    LoggerManager.Warning($"{fucName}: 不支持的任务类型 \"{missionTypeRaw}\"，支持: Main/主线/0, Branch/支线/1, Little/小型/2, bounty/悬赏/3, SpeKiller/杀手/4, TreasureMap/藏宝图/5");
                    return;
                }
            }

            // Index查找必须指定任务类型
            if (searchAttr == 0 && missionType == null)
            {
                LoggerManager.Warning($"{fucName}: 按Index查找时必须指定任务类型");
                return;
            }

            MissionDataController mdc = MissionDataController._instance;
            if (mdc == null)
            {
                LoggerManager.Error($"{fucName}: MissionDataController实例不存在");
                return;
            }

            GameController gameController = GameController.Instance;
            if (gameController == null)
            {
                LoggerManager.Error($"{fucName}: GameController实例不存在");
                return;
            }

            // 构建要搜索的任务库列表
            var searchList = new System.Collections.Generic.List<Il2CppSystem.Collections.Generic.List<MissionData>>();
            var searchTypeNames = new System.Collections.Generic.List<string>();

            if (missionType != null)
            {
                // 指定类型：只搜索该类型
                var db = GetMissionDataBase(mdc, missionType.Value);
                if (db == null)
                {
                    LoggerManager.Error($"{fucName}: 任务类型 {missionType} 对应的数据库为空");
                    return;
                }
                searchList.Add(db);
                searchTypeNames.Add(GetMissionTypeName(missionType.Value));
            }
            else
            {
                // 未指定类型：搜索所有任务库
                for (int t = 0; t <= 5; t++)
                {
                    var db = GetMissionDataBase(mdc, t);
                    if (db != null)
                    {
                        searchList.Add(db);
                        searchTypeNames.Add(GetMissionTypeName(t));
                    }
                }
            }

            // 执行查找
            MissionData found = null;
            string foundTypeName = null;

            if (searchAttr == 0)
            {
                // Index查找
                if (!int.TryParse(keyWord, out int index))
                {
                    LoggerManager.Warning($"{fucName}: Index查找时关键字必须为整数，实际为 \"{keyWord}\"");
                    return;
                }
                var db = searchList[0];
                if (index < 0 || index >= db.Count)
                {
                    LoggerManager.Warning($"{fucName}: Index越界 {index}，范围[0,{db.Count})");
                    return;
                }
                found = db[index];
                foundTypeName = searchTypeNames[0];
            }
            else if (searchAttr == 1)
            {
                // name查找
                for (int t = 0; t < searchList.Count; t++)
                {
                    var db = searchList[t];
                    for (int i = 0; i < db.Count; i++)
                    {
                        MissionData md = db[i];
                        if (md != null && md.name == keyWord)
                        {
                            found = md;
                            foundTypeName = searchTypeNames[t];
                            break;
                        }
                    }
                    if (found != null) break;
                }
            }
            else if (searchAttr == 2)
            {
                // speMissionID查找
                if (!int.TryParse(keyWord, out int speId))
                {
                    LoggerManager.Warning($"{fucName}: speMissionID查找时关键字必须为整数，实际为 \"{keyWord}\"");
                    return;
                }
                for (int t = 0; t < searchList.Count; t++)
                {
                    var db = searchList[t];
                    for (int i = 0; i < db.Count; i++)
                    {
                        MissionData md = db[i];
                        if (md != null && md.speMissionID == speId)
                        {
                            found = md;
                            foundTypeName = searchTypeNames[t];
                            break;
                        }
                    }
                    if (found != null) break;
                }
            }

            if (found == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到匹配的任务 (关键字=\"{keyWord}\", 属性={SearchAttrName(searchAttr)}, 类型={missionType?.ToString() ?? "全部"})");
                return;
            }

            MissionData cloned = found.Clone().Cast<MissionData>();
            if (!ApplyNamedParams(cloned, customParams, gameController, fucName))
                return;

            gameController.GetFullMission(cloned);
            LoggerManager.Debug($"{fucName}: 已获取任务 name={cloned.name}, 原任务名={found.name}, speMissionID={cloned.speMissionID}, 类型={foundTypeName}");
        }

        private static GetMissionParams ParseNamedParams(string[] fucParams, int startIdx, string fucName)
        {
            GetMissionParams p = GetMissionParams.Default;

            for (int i = startIdx; i < fucParams.Length; i++)
            {
                string param = fucParams[i];
                if (string.IsNullOrWhiteSpace(param)) continue;

                int eqIdx = param.IndexOf('=');
                if (eqIdx < 0)
                {
                    LoggerManager.Warning($"{fucName}: 已忽略未命名参数 \"{param}\"，命名参数格式应为 key=value");
                    continue;
                }

                string key = param.Substring(0, eqIdx).Trim().ToLowerInvariant();
                string val = param.Substring(eqIdx + 1).Trim();

                switch (key)
                {
                    case "name":
                        p.name = val;
                        p.hasName = true;
                        break;
                    case "lefttime":
                        if (TryParseInt(val, key, fucName, out p.leftTime)) p.hasLeftTime = true;
                        break;
                    case "difficulty":
                        if (TryParseFloat(val, key, fucName, out p.difficulty)) p.hasDifficulty = true;
                        break;
                    case "difficultyrate":
                        if (TryParseFloat(val, key, fucName, out p.difficultyRate)) p.hasDifficultyRate = true;
                        break;
                    case "rewardrate":
                        if (TryParseFloat(val, key, fucName, out p.rewardRate)) p.hasRewardRate = true;
                        break;
                    case "treasurelv":
                        if (TryParseFloat(val, key, fucName, out p.treasureLv)) p.hasTreasureLv = true;
                        break;
                    case "sourceheroid":
                        if (TryParseInt(val, key, fucName, out p.sourceHeroID)) p.hasSourceHeroID = true;
                        break;
                    case "sourceforceid":
                        p.sourceForceID = CommonHandlers.ResolveToForceID(val, -1);
                        p.hasSourceForceID = true;
                        break;
                    case "missionheroid":
                        if (TryParseInt(val, key, fucName, out p.missionHeroID)) p.hasMissionHeroID = true;
                        break;
                    case "missionbountytype":
                    case "bountytype":
                        if (TryParseEnum(val, key, fucName, out p.missionBountyType)) p.hasMissionBountyType = true;
                        break;
                    case "missiondisablequicktravel":
                    case "disablequicktravel":
                        p.missionDisableQuickTravel = CommonHandlers.ParseBool(val, false);
                        p.hasMissionDisableQuickTravel = true;
                        break;
                    case "missionhidetargetplace":
                    case "hidetargetplace":
                        p.missionHideTargetPlace = CommonHandlers.ParseBool(val, false);
                        p.hasMissionHideTargetPlace = true;
                        break;
                    case "missionhidetargetplacestring":
                    case "hidetargetplacestring":
                        p.missionHideTargetPlaceString = val;
                        p.hasMissionHideTargetPlaceString = true;
                        break;
                    case "noautofinish":
                        p.noAutoFinish = CommonHandlers.ParseBool(val, false);
                        p.hasNoAutoFinish = true;
                        break;
                    case "targetindex":
                        TryParseInt(val, key, fucName, out p.targetIndex);
                        break;
                    case "needindex":
                        TryParseInt(val, key, fucName, out p.needIndex);
                        break;
                    case "describe":
                        p.describe = val;
                        p.hasDescribe = true;
                        break;
                    case "missiontriggertype":
                    case "triggertype":
                        if (TryParseEnum(val, key, fucName, out p.missionTriggerType)) p.hasMissionTriggerType = true;
                        break;
                    case "missiontargetareatypelimit":
                    case "areatypelimit":
                        if (TryParseEnum(val, key, fucName, out p.missionTargetAreaTypeLimit)) p.hasMissionTargetAreaTypeLimit = true;
                        break;
                    case "tirggertargetid":
                    case "triggertargetid":
                        p.tirggerTargetID = val;
                        p.hasTirggerTargetID = true;
                        break;
                    case "missiontargetfinishcallplotid":
                    case "finishcallplotid":
                        if (TryParseInt(val, key, fucName, out p.missionTargetFinishCallPlotID)) p.hasMissionTargetFinishCallPlotID = true;
                        break;
                    case "missiontargettype":
                    case "targettype":
                        if (TryParseEnum(val, key, fucName, out p.missionTargetType)) p.hasMissionTargetType = true;
                        break;
                    case "missiontargetid":
                    case "targetid":
                        p.missionTargetID = val;
                        p.hasMissionTargetID = true;
                        break;
                    case "missionresourceneed":
                    case "resourceneed":
                        if (TryParseInt(val, key, fucName, out p.missionResourceNeed)) p.hasMissionResourceNeed = true;
                        break;
                    case "missionnumcount":
                    case "numcount":
                        if (TryParseFloat(val, key, fucName, out p.missionNumCount)) p.hasMissionNumCount = true;
                        break;
                    case "missionnumneed":
                    case "numneed":
                        if (TryParseFloat(val, key, fucName, out p.missionNumNeed)) p.hasMissionNumNeed = true;
                        break;
                    default:
                        LoggerManager.Warning($"{fucName}: 未识别的命名参数 \"{key}\"，已忽略");
                        break;
                }
            }

            return p;
        }

        private static bool ApplyNamedParams(MissionData mission, GetMissionParams p, GameController gameController, string fucName)
        {
            if (mission == null)
            {
                LoggerManager.Error($"{fucName}: 任务实例为空，无法应用命名参数");
                return false;
            }

            if (p.hasName)
                mission.name = p.name;
            if (p.hasLeftTime)
                mission.leftTime = p.leftTime;
            if (p.hasDifficulty)
                mission.difficulty = p.difficulty;
            if (p.hasDifficultyRate)
                mission.difficultyRate = p.difficultyRate;
            if (p.hasRewardRate)
                mission.rewardRate = p.rewardRate;
            if (p.hasTreasureLv)
                mission.treasureLv = p.treasureLv;
            if (p.hasSourceHeroID)
                mission.sourceHeroID = p.sourceHeroID;
            if (p.hasSourceForceID)
                mission.sourceForceID = p.sourceForceID;
            if (p.hasMissionHeroID)
                mission.missionHeroID = p.missionHeroID;
            if (p.hasMissionBountyType)
                mission.missionBountyType = p.missionBountyType;
            if (p.hasMissionDisableQuickTravel)
                mission.missionDisableQuickTravel = p.missionDisableQuickTravel;
            if (p.hasMissionHideTargetPlace)
                mission.missionHideTargetPlace = p.missionHideTargetPlace;
            if (p.hasMissionHideTargetPlaceString)
                mission.missionHideTargetPlaceString = p.missionHideTargetPlaceString;
            if (p.hasNoAutoFinish)
                mission.noAutoFinish = p.noAutoFinish;

            MissionTargetData target = null;
            if (NeedTargetData(p))
            {
                if (mission.missionTargetDatas == null || p.targetIndex < 0 || p.targetIndex >= mission.missionTargetDatas.Count)
                {
                    LoggerManager.Warning($"{fucName}: targetIndex={p.targetIndex} 越界或 missionTargetDatas 为空，中止任务接取");
                    return false;
                }
                target = mission.missionTargetDatas[p.targetIndex];
                if (target == null)
                {
                    LoggerManager.Warning($"{fucName}: missionTargetDatas[{p.targetIndex}] 为空，中止任务接取");
                    return false;
                }

                if (p.hasDescribe)
                    target.describe = p.describe;
                if (p.hasMissionTriggerType)
                    target.missionTriggerType = p.missionTriggerType;
                if (p.hasMissionTargetAreaTypeLimit)
                    target.missionTargetAreaTypeLimit = p.missionTargetAreaTypeLimit;
                if (p.hasTirggerTargetID)
                    target.tirggerTargetID = p.tirggerTargetID;
                if (p.hasMissionTargetFinishCallPlotID)
                    target.missionTargetFinishCallPlotID = p.missionTargetFinishCallPlotID;
            }

            if (NeedNeedData(p))
            {
                if (target == null)
                {
                    if (mission.missionTargetDatas == null || p.targetIndex < 0 || p.targetIndex >= mission.missionTargetDatas.Count)
                    {
                        LoggerManager.Warning($"{fucName}: targetIndex={p.targetIndex} 越界或 missionTargetDatas 为空，中止任务接取");
                        return false;
                    }
                    target = mission.missionTargetDatas[p.targetIndex];
                }

                if (target == null || target.missionNeedDatas == null || p.needIndex < 0 || p.needIndex >= target.missionNeedDatas.Count)
                {
                    LoggerManager.Warning($"{fucName}: needIndex={p.needIndex} 越界或 missionNeedDatas 为空，中止任务接取");
                    return false;
                }

                MissionNeedData need = target.missionNeedDatas[p.needIndex];
                if (need == null)
                {
                    LoggerManager.Warning($"{fucName}: missionNeedDatas[{p.needIndex}] 为空，中止任务接取");
                    return false;
                }

                if (p.hasMissionTargetType)
                    need.missionTargetType = p.missionTargetType;
                if (p.hasMissionTargetID)
                    need.missionTargetID = p.missionTargetID;
                if (p.hasMissionResourceNeed)
                    need.missionResourceNeed = p.missionResourceNeed;
                if (p.hasMissionNumCount)
                    need.missionNumCount = p.missionNumCount;
                if (p.hasMissionNumNeed)
                    need.missionNumNeed = p.missionNumNeed;
            }

            if (p.hasName)
            {
                HeroData player = gameController.worldData != null ? gameController.worldData.Player() : null;
                if (player != null && player.HaveMission(mission.name))
                {
                    LoggerManager.Warning($"{fucName}: 玩家已存在同名任务 \"{mission.name}\"，后续按name查找只能命中第一个实例");
                }
            }

            return true;
        }

        private static bool NeedTargetData(GetMissionParams p)
        {
            return p.hasDescribe
                || p.hasMissionTriggerType
                || p.hasMissionTargetAreaTypeLimit
                || p.hasTirggerTargetID
                || p.hasMissionTargetFinishCallPlotID
                || NeedNeedData(p);
        }

        private static bool NeedNeedData(GetMissionParams p)
        {
            return p.hasMissionTargetType
                || p.hasMissionTargetID
                || p.hasMissionResourceNeed
                || p.hasMissionNumCount
                || p.hasMissionNumNeed;
        }

        private static bool TryParseInt(string raw, string key, string fucName, out int value)
        {
            if (int.TryParse(raw, out value))
                return true;

            LoggerManager.Warning($"{fucName}: 参数 {key} 需要整数，实际为 \"{raw}\"");
            return false;
        }

        private static bool TryParseFloat(string raw, string key, string fucName, out float value)
        {
            if (float.TryParse(raw, out value))
                return true;

            LoggerManager.Warning($"{fucName}: 参数 {key} 需要数字，实际为 \"{raw}\"");
            return false;
        }

        private static bool TryParseEnum<T>(string raw, string key, string fucName, out T value) where T : struct
        {
            if (int.TryParse(raw, out int intValue))
            {
                value = (T)System.Enum.ToObject(typeof(T), intValue);
                return true;
            }

            if (System.Enum.TryParse(raw, true, out value))
                return true;

            LoggerManager.Warning($"{fucName}: 参数 {key} 枚举解析失败，实际为 \"{raw}\"");
            return false;
        }

        /// <summary>根据任务类型索引获取对应的MissionDataBase（单对象类型会包装为仅含1个元素的List）</summary>
        public static Il2CppSystem.Collections.Generic.List<MissionData> GetMissionDataBase(MissionDataController mdc, int type)
        {
            switch (type)
            {
                case 0: return mdc.MainMissionDataBase;
                case 1: return mdc.BranchMissionDataBase;
                case 2: return mdc.LittleMissionDataBase;
                case 3: return mdc.bountyMissionDataBase;
                case 4:
                    {
                        // SpeKillerMissionDataBase 是单个 MissionData，包装为 List
                        if (mdc.SpeKillerMissionDataBase == null) return null;
                        var list = new Il2CppSystem.Collections.Generic.List<MissionData>();
                        list.Add(mdc.SpeKillerMissionDataBase);
                        return list;
                    }
                case 5:
                    {
                        // TreasureMapMissionDataBase 是单个 MissionData，包装为 List
                        if (mdc.TreasureMapMissionDataBase == null) return null;
                        var list = new Il2CppSystem.Collections.Generic.List<MissionData>();
                        list.Add(mdc.TreasureMapMissionDataBase);
                        return list;
                    }
                default: return null;
            }
        }

        /// <summary>根据任务类型索引获取中文名称</summary>
        private static string GetMissionTypeName(int type)
        {
            switch (type)
            {
                case 0: return "主线";
                case 1: return "支线";
                case 2: return "小任务";
                case 3: return "赏金";
                case 4: return "特殊击杀";
                case 5: return "藏宝图";
                default: return $"未知({type})";
            }
        }

        /// <summary>查找属性名称（用于日志）</summary>
        private static string SearchAttrName(int attr)
        {
            switch (attr)
            {
                case 0: return "Index";
                case 1: return "name";
                case 2: return "speMissionID";
                default: return $"未知({attr})";
            }
        }
    }
}
