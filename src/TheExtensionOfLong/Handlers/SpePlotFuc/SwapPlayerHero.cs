using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

namespace TheExtensionOfLong
{
    [SpePlotFuc("SwapPlayerHero")]
    public static class SpePlotFucSwapPlayerHero
    {
        private const string CACHE_KEY  = "SwapPlayerHero_Record";

        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 1)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*目标角色ID/角色名称]");
                return;
            }

            WorldData worldData = CommonHandlers.GetWorldData();
            if (worldData == null) { LoggerManager.Error($"{fucName}: WorldData实例不存在"); return; }

            PlotEventLogData log = worldData.PlotEventLog;
            if (log == null) { LoggerManager.Error($"{fucName}: PlotEventLog实例不存在"); return; }

            HeroData targetHero = CommonHandlers.ResolveHeroId(plotController, fucParams[0]);
            if (targetHero == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到目标角色 \"{fucParams[0]}\"");
                return;
            }

            List<HeroData> heros = worldData.Heros;
            int targetIdx = -1;
            for (int i = 0; i < heros.Count; i++)
                if (heros[i] == targetHero) { targetIdx = i; break; }

            if (targetIdx <= 0)
            {
                LoggerManager.Warning($"{fucName}: {targetHero.heroName}(ID={targetHero.heroID}) 不在列表中或已是玩家");
                return;
            }

            GameController gc = GameController.Instance;

            // 目标角色如果在某队伍中，先离队（清理两面数据：队长队列表 + 自身队伍状态）
            if (targetHero.inTeam)
                gc.HeroLeaveTeam(targetHero);

            string cached = log.Get(CACHE_KEY);
            if (cached != null)
            {
                if (!int.TryParse(cached, out int prevIdx))
                {
                    log.Set(CACHE_KEY,  null);
                    return;
                }

                // 还原：DoSwapHeroes 是对称操作，再调一次即还原
                DoSwapHeroes(worldData, heros, 0, prevIdx);
                heros[prevIdx].inTeam = false;
                heros[prevIdx].teamLeader = -1;
                heros[prevIdx].teamMates?.Clear();
                heros[prevIdx].autoLeaveTeamDay = -1;
                // missions / forceMission 交换
                var mTmp = heros[0].missions;
                var fmTmp = heros[0].forceMission;
                heros[0].missions = heros[prevIdx].missions;
                heros[0].forceMission = heros[prevIdx].forceMission;
                heros[prevIdx].missions = mTmp;
                heros[prevIdx].forceMission = fmTmp;

                // heroID 已变，双方都需要重算
                gc.CountHeroData(heros[0]);
                gc.CountHeroData(heros[prevIdx]);

                log.Set(CACHE_KEY,  null);

                // 被还原回 NPC 的角色（heros[prevIdx]）如果在大地图上，分配到最近区域
                HeroData restoredNpc = heros[prevIdx];
                if (restoredNpc.atAreaID < 0 && restoredNpc.bigMapPos != null)
                {
                    int nearID = BigMapController.Instance.GetNearAreaID(restoredNpc.bigMapPos);
                    if (nearID >= 0)
                    {
                        AreaData nearArea = worldData.GetArea(nearID);
                        if (nearArea != null)
                        {
                            gc.HeroEnterArea(restoredNpc, nearArea);
                            LoggerManager.Debug($"{fucName}: 还原角色 {restoredNpc.heroName} 已分配到最近区域 {nearArea.areaName}(ID={nearArea.areaID})");
                        }
                    }
                }

                if (targetIdx == prevIdx)
                {
                    // 同一角色 = 取消扮演
                    RefreshWorldUI(heros[0]);
                    LoggerManager.Debug($"{fucName}: 已取消扮演 heros[{prevIdx}]");
                    return;
                }
            }

            // 执行新交换
            // 解散当前玩家队伍（让所有队员离队），HeroLeaveTeam 同时处理数据+位置+AI
            DisbandPlayerTeam(worldData, heros[0], gc);
            log.Set(CACHE_KEY, targetIdx.ToString());
            DoSwapHeroes(worldData, heros, 0, targetIdx);

            if (heros[0].teamMates == null)
                heros[0].teamMates = new Il2CppSystem.Collections.Generic.List<int>();
            else
                heros[0].teamMates.Clear();
            heros[targetIdx].inTeam = false;
            heros[targetIdx].teamLeader = -1;
            heros[targetIdx].teamMates?.Clear();
            heros[targetIdx].autoLeaveTeamDay = -1;
            var missionsTmp = heros[0].missions;
            var forceMissionTmp = heros[0].forceMission;
            heros[0].missions = heros[targetIdx].missions;
            heros[0].forceMission = heros[targetIdx].forceMission;
            heros[targetIdx].missions = missionsTmp;
            heros[targetIdx].forceMission = forceMissionTmp;

            // heroID 已变，双方都需要重算
            gc.CountHeroData(heros[0]);
            gc.CountHeroData(heros[targetIdx]);

            // 如果原玩家在大地图上（atAreaID < 0），将其分配到最近区域
            // 确保 NPC 能被 RecreateAllBigMapHeroIcon 识别并创建大地图图标
            HeroData oldPlayer = heros[targetIdx];
            if (oldPlayer.atAreaID < 0 && oldPlayer.bigMapPos != null)
            {
                int nearAreaID = BigMapController.Instance.GetNearAreaID(oldPlayer.bigMapPos);
                if (nearAreaID >= 0)
                {
                    AreaData nearest = worldData.GetArea(nearAreaID);
                    gc.HeroEnterArea(oldPlayer, nearest);
                    LoggerManager.Debug($"{fucName}: 原玩家 {oldPlayer.heroName} 已分配到最近区域 {nearest.areaName}(ID={nearest.areaID})");
                }
                else
                {
                    LoggerManager.Warning($"{fucName}: 未找到最近区域(GetNearAreaID返回-1)，原玩家 {oldPlayer.heroName} 保持当前状态");
                }
            }

            // 初始化关系的结识状态和好感度
            InitializeRelationships(worldData, heros[0]);

            RefreshWorldUI(heros[0]);
            LoggerManager.Debug($"{fucName}: 已扮演 {heros[0].heroName}(ID={heros[0].heroID})，原玩家 {heros[targetIdx].heroName}(ID={heros[targetIdx].heroID})");
        }

        // ============================================================
        // 核心交换逻辑
        // ============================================================

        private static void DoSwapHeroes(WorldData worldData, List<HeroData> heros, int idxA, int idxB)
        {
            HeroData a = heros[idxA];
            HeroData b = heros[idxB];
            heros[idxA] = b;
            heros[idxB] = a;
            int heroIdA = a.heroID;
            int heroIdB = b.heroID;

            SwapPlayerOnlyData(a, b);
            a.SetHeroID(heroIdB);
            b.SetHeroID(heroIdA);
            SwapAllHeroIDReferences(worldData, heroIdA, heroIdB);

            // 注意：不交换 atAreaID 和 bigMapPos。
            // 这两个字段代表角色实际所在的位置，交换后新玩家应保留自己的位置
            //（"从替换角色所在位置继续游戏"）。
            // RefreshWorldUI 的区域退出判断改用 AreaController.areaData 是否存在，
            // 不依赖 hero.atAreaID，避免大地图上的伪触发。
        }

        /// <summary>
        /// 转移玩家专属数据：仓库 + 房屋容量加成。
        /// 这些数据属于"玩家身份"(heroID=0)，不属于具体角色对象。
        /// </summary>
        private static void SwapPlayerOnlyData(HeroData a, HeroData b)
        {
            var tmpStorage = a.selfStorage;
            a.selfStorage = b.selfStorage;
            b.selfStorage = tmpStorage;

            float tmpHouse = a.selfHouseTotalAdd;
            a.selfHouseTotalAdd = b.selfHouseTotalAdd;
            b.selfHouseTotalAdd = tmpHouse;
        }

        // ============================================================
        // 全局 heroID 引用替换（4系统）
        // ============================================================

        private static void SwapAllHeroIDReferences(WorldData worldData, int idA, int idB)
        {
            // 1. 所有角色的关系字段
            foreach (var h in worldData.Heros)
            {
                if (h == null) continue;
                h.Lover      = Swap(h.Lover,      idA, idB);
                h.Teacher    = Swap(h.Teacher,    idA, idB);
                h.teamLeader = Swap(h.teamLeader, idA, idB);
                SwapIntInList(h.PreLovers, idA, idB);
                SwapIntInList(h.Relatives, idA, idB);
                SwapIntInList(h.Brothers,  idA, idB);
                SwapIntInList(h.Friends,   idA, idB);
                SwapIntInList(h.Haters,    idA, idB);
                SwapIntInList(h.Students,  idA, idB);
                SwapIntInList(h.teamMates, idA, idB);
            }

            // 2. 势力数据
            foreach (var force in worldData.Forces)
            {
                if (force == null) continue;
                force.leader = Swap(force.leader, idA, idB);
                SwapIntInList(force.ownHeros, idA, idB);
                var forceJobs = force.forceJobSettingData?.ForceJobs;
                if (forceJobs != null)
                    for (int j = 0; j < forceJobs.Count; j++)
                        SwapIntInList(forceJobs[j], idA, idB);
                if (force.bookWriterList != null)
                    for (int j = 0; j < force.bookWriterList.Count; j++)
                        if (force.bookWriterList[j] != null)
                            force.bookWriterList[j].bookWriterHeroID = Swap(force.bookWriterList[j].bookWriterHeroID, idA, idB);
            }

            // 3. 区域数据
            foreach (var area in worldData.Areas)
            {
                if (area == null) continue;
                SwapIntInList(area.insideHeros, idA, idB);
                area.branchLeaderID = Swap(area.branchLeaderID, idA, idB);
            }

            // 4. 玩家著书列表
            if (worldData.playerBookWriter != null)
                for (int i = 0; i < worldData.playerBookWriter.Count; i++)
                    if (worldData.playerBookWriter[i] != null)
                        worldData.playerBookWriter[i].bookWriterHeroID = Swap(worldData.playerBookWriter[i].bookWriterHeroID, idA, idB);
        }

        private static int Swap(int field, int idA, int idB)
        {
            if (field == idA) return idB;
            if (field == idB) return idA;
            return field;
        }

        private static void SwapIntInList(List<int> list, int idA, int idB)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == idA) list[i] = idB;
                else if (list[i] == idB) list[i] = idA;
            }
        }

        // ============================================================
        // 队伍解散
        // ============================================================

        /// <summary>
        /// 解散玩家的队伍：对所有队员调用 HeroLeaveTeam（同时处理数据/位置/AI），
        /// 然后清空队长的 teamMates 列表。
        /// </summary>
        private static void DisbandPlayerTeam(WorldData worldData, HeroData player, GameController gc)
        {
            if (player.teamMates == null || player.teamMates.Count == 0) return;
            for (int i = player.teamMates.Count - 1; i >= 0; i--)
            {
                HeroData member = worldData.GetHero(player.teamMates[i]);
                if (member != null)
                    gc.HeroLeaveTeam(member);
            }
            player.teamMates.Clear();
        }

        /// <summary>
        /// 初始化新玩家所有关系的结识状态和好感度：
        /// 对关系列表中的每个角色调用 SetMeetFavor，强制设置好感到指定值。
        /// </summary>
        private static void InitializeRelationships(WorldData worldData, HeroData player)
        {
            void SetFavor(HeroData target, float favor)
            {
                if (target == null) return;
                target.SetMeetFavor(false, favor);
            }

            void InitList(List<int> list, float favor)
            {
                if (list == null) return;
                for (int i = 0; i < list.Count; i++)
                    SetFavor(worldData.GetHero(list[i]), favor);
            }

            // 权重从低到高执行，让高好感度最后写入覆盖
            InitList(player.Haters, -40f);
            InitList(player.Students, 40f);
            InitList(player.Friends, 50f);
            SetFavor(worldData.GetHero(player.Teacher), 50f);
            InitList(player.Brothers, 80f);
            InitList(player.Relatives, 80f);
            InitList(player.PreLovers, 90f);
            SetFavor(worldData.GetHero(player.Lover), 100f);
        }

        // ============================================================
        // UI 刷新
        // ============================================================

        private static void RefreshWorldUI(HeroData playerHero)
        {
            if (playerHero == null) return;
            GameController gc = GameController.Instance;
            if (gc == null) return;

            // 0. HUD 头像 + 立绘（两个分支都需要）
            HudController hud = HudController.Instance;
            if (hud != null)
            {
                if (hud.heroFace != null)
                {
                    var detail = hud.heroFace.GetComponent<ShowHeroDetail>();
                    if (detail != null) detail.heroData = playerHero;
                }
                hud.RefreshHeroSkeleton();
            }

            // 1. 根据 SpeHero 的位置分支
            if (playerHero.atAreaID >= 0)
            {
                // 分支 A：SpeHero 在区域内 → TeleportPlayerToArea 完整过渡
                // 内部做：
                //   BigMapController._instance.MovePlayerIconToArea(areaID)  — 更新 playerArmy 位置
                //   this.PlayerEnterArea(area, checkPlot)                     — 场景切换 + 相机过渡
                // 传入 checkPlot=false 避免在剧情中二次触发剧情
                try
                {
                    // 先退出当前区域场景（如果存在），避免场景叠加导致状态混乱
                    try { AreaController.Instance?.PlayerLeaveArea(); }
                    catch { /* 当前不在区域场景中，正常 */ }

                    // 更新 playerArmy 的 heroData 引用 + 重新生成骨骼
                    // playerArmy 的 BigmapNpcController.heroData 仍是旧引用，
                    // 不更新的话 MovePlayerIconToArea 移动的图标会显示旧玩家骨架
                    BigMapController bmc = BigMapController.Instance;
                    if (bmc != null && bmc.playerArmy != null)
                    {
                        var npcCtrl = bmc.playerArmy.GetComponent<BigmapNpcController>();
                        if (npcCtrl != null)
                        {
                            npcCtrl.heroData = playerHero;
                            // 销毁旧骨架
                            if (npcCtrl.selfSkeleton != null)
                                UnityEngine.Object.Destroy(npcCtrl.selfSkeleton);
                            // 用新玩家数据生成新骨架
                            var skeleton = playerHero.GenerateHeroSkeleton(bmc.playerArmy, Vector3.one * 0.2f);
                            if (skeleton != null)
                            {
                                skeleton.gameObject.AddComponent<SkeletonAutoPause>().skeletonAnimation = skeleton;
                                npcCtrl.selfSkeleton = skeleton.gameObject;
                            }
                        }
                    }

                    gc.TeleportPlayerToArea(playerHero.atAreaID, false);
                    LoggerManager.Debug($"SwapPlayerHero: 传送进入区域 areaID={playerHero.atAreaID}");
                    return;  // 成功后直接返回（场景已切换，不需要 FallbackToBigMap）
                }
                catch (System.Exception e)
                {
                    LoggerManager.Warning($"SwapPlayerHero: TeleportPlayerToArea({playerHero.atAreaID}) 失败，回退到大地图模式: {e.Message}");
                }
            }

            // 分支 B（默认）：SpeHero 不在区域内或 AreaData 未找到 → 大地图模式
            FallbackToBigMap(playerHero);
        }

        /// <summary>
        /// 大地图模式：重建 NPC 图标 + 创建玩家图标 + 视角聚焦。
        /// </summary>
        private static void FallbackToBigMap(HeroData playerHero)
        {
            BigMapController bmc = BigMapController.Instance;
            if (bmc == null) return;

            try { bmc.RecreateAllBigMapHeroIcon(); }
            catch (System.Exception e) { LoggerManager.Error($"SwapPlayerHero: RecreateAllBigMapHeroIcon 失败: {e.Message}"); }

            try { bmc.CreateBigMapNpc(playerHero); }
            catch (System.Exception e) { LoggerManager.Error($"SwapPlayerHero: CreateBigMapNpc 失败: {e.Message}"); }

            try { bmc.QuickFocusOnSelf(); }
            catch (System.Exception e) { LoggerManager.Error($"SwapPlayerHero: QuickFocusOnSelf 失败: {e.Message}"); }
        }

        /// <summary>
        /// 从所有区域中查找离指定大地图坐标最近的区域。
        /// </summary>
        private static AreaData FindNearestArea(WorldData worldData, BigMapPos fromPos)
        {
            if (fromPos == null) return null;
            Vector3 origin = fromPos.ToVector3();
            AreaData nearest = null;
            float minDist = float.MaxValue;
            for (int i = 0; i < worldData.Areas.Count; i++)
            {
                AreaData area = worldData.Areas[i];
                if (area == null || area.bigMapPos == null) continue;
                float dist = Vector3.Distance(origin, area.bigMapPos.ToVector3());
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = area;
                }
            }
            return nearest;
        }
    }
}
