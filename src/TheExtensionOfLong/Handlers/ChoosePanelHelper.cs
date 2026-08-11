using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 通用选择面板辅助类
    /// 手动初始化ChoosePanel并填充数据，绕过ShowChoosePanel内部对List&lt;object&gt; param的解析
    /// 原因：Il2CppInterop无法正确传递装箱int给List&lt;object&gt;参数
    /// </summary>
    public static class ChoosePanelHelper
    {
        /// <summary>
        /// 弹出通用选择面板（角色/物品/武学），支持条件表达式筛选
        /// 格式: ShowChoosePanel*选择类型#筛选类型#筛选条件表达式(可选)#源角色名称/ID(可选)#回调函数名#回调参数(可选)#取消回调函数名(可选)#目标角色名称/ID(可选)
        ///   选择类型: 0/HeroSkill/武学, 1/HeroItem/物品, 2/Hero/角色
        ///   筛选类型: 0/None, 1/SkillBreakThrough, 2/BattleUseItem, ..., 25/IncludeStorage 等（支持int或枚举名）
        ///   筛选条件表达式(可选): 用{{}}包裹的条件表达式，逐项求值筛选；为空则不筛选
        ///     Hero模式: 当前遍历角色设为targetInteractHero，可用[$HeroData:...$]查询
        ///     HeroItem模式: 当前遍历物品设为plotInteractItem，可用[$ItemData:...$]查询
        ///     HeroSkill模式: 当前遍历武学设为plotInteractSkill，可用[$KungfuSkillLvData:...$]查询
        ///   源角色: HeroItem/HeroSkill时从该角色读取物品/武学；为空时从玩家读取
        ///   回调函数名: 选中后通过SendMessage调用的函数名
        ///   目标角色: 用于物品/武学选择的targetFavorHero筛选，支持角色ID/名称/关键字(player等)
        /// 注意: 筛选条件表达式必须用{{}}包裹，与ChooseHero指令一致
        ///       筛选类型和条件表达式叠加：先枚举筛选，再表达式筛选
        /// 示例: ShowChoosePanel*2#0#{{[$HeroData:isFemale$][=]1}}##OnHeroChosen   (选女性角色)
        ///       ShowChoosePanel*1#0#{{[$ItemData:rareLv$][>=]3}}#player#OnItemChosen  (选稀有度≥3的物品)
        ///       ShowChoosePanel*0#0#{{[$KungfuSkillLvData:CanUpgrade$][=]1}}#小白#OnSkillSelected  (选可升级武学)
        ///       ShowChoosePanel*2#0##OnHeroChosen  (无条件筛选)
        /// </summary>
        public static void ShowChoosePanel(PlotController __instance, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 4)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*选择类型#筛选类型#筛选条件表达式(可选)#源角色名称/ID(可选)#回调函数名#回调参数(可选)#取消回调函数名(可选)#目标角色名称/ID(可选)]");
                return;
            }

            // 1. 解析参数（新格式：增加了筛选条件表达式参数位）
            ChooseType chooseType = CommonHandlers.ParseChooseType(fucParams[0]);
            ChooseFilterType filterType = CommonHandlers.ParseChooseFilterType(fucParams[1]);
            string conditionExpr = !string.IsNullOrWhiteSpace(fucParams[2]) ? fucParams[2].Trim() : null;
            string sourceHeroStr = !string.IsNullOrWhiteSpace(fucParams[3]) ? fucParams[3] : null;
            string callbackFuc = fucParams[4];
            string callbackParam = fucParams.Length > 5 && !string.IsNullOrWhiteSpace(fucParams[5]) ? fucParams[5] : null;
            string cancelFuc = fucParams.Length > 6 && !string.IsNullOrWhiteSpace(fucParams[6]) ? fucParams[6] : null;
            string targetHeroStr = fucParams.Length > 7 && !string.IsNullOrWhiteSpace(fucParams[7]) ? fucParams[7] : null;

            HeroData targetFavorHero = null;
            if (targetHeroStr != null)
            {
                targetFavorHero = CommonHandlers.ResolveHeroId(__instance, targetHeroStr);
                if (targetFavorHero == null)
                {
                    LoggerManager.Warning($"{fucName}: 未找到目标角色 \"{targetHeroStr}\"");
                }
            }

            // 2. 解析源角色（HeroItem/HeroSkill时有效）
            HeroData sourceHero = null;
            if (sourceHeroStr != null && chooseType != ChooseType.Hero)
            {
                sourceHero = CommonHandlers.ResolveHeroId(__instance, sourceHeroStr);
                if (sourceHero == null)
                {
                    LoggerManager.Warning($"{fucName}: 未找到源角色 \"{sourceHeroStr}\"");
                }
            }

            // 3. 获取ChooseController
            ChooseController chooseController = ChooseController._instance;
            if (chooseController == null)
            {
                LoggerManager.Error($"{fucName}: ChooseController实例不存在");
                return;
            }

            // 4. 根据选择类型收集数据并显示面板
            switch (chooseType)
            {
                case ChooseType.Hero:
                    {
                        // Hero模式：直接调用重载1（List<HeroData>），不存在装箱问题
                        WorldData worldData = CommonHandlers.GetWorldData();
                        if (worldData == null || worldData.Heros == null)
                        {
                            LoggerManager.Error($"{fucName}: WorldData或Heros为空");
                            return;
                        }

                        // 保存原始上下文
                        HeroData originalTargetHero = __instance.targetInteractHero;
                        List<HeroData> candidateList = new List<HeroData>();
                        var heros = worldData.Heros;

                        try
                        {
                            for (int i = 0; i < heros.Count; i++)
                            {
                                HeroData hero = heros[i];
                                if (hero == null || hero.dead) continue;

                                // 条件表达式筛选
                                if (conditionExpr != null)
                                {
                                    __instance.targetInteractHero = hero;
                                    try
                                    {
                                        if (!ConditionExpressionEvaluator.Evaluate(__instance, conditionExpr))
                                            continue;
                                    }
                                    catch (System.Exception e)
                                    {
                                        LoggerManager.Warning($"{fucName}: 评估角色 {hero.heroName} 条件时出错: {e.Message}");
                                        continue;
                                    }
                                }

                                candidateList.Add(hero);
                            }
                        }
                        finally
                        {
                            __instance.targetInteractHero = originalTargetHero;
                        }

                        if (candidateList.Count == 0)
                        {
                            LoggerManager.Debug($"{fucName}: 没有符合条件的角色");
                            return;
                        }
                        if (sourceHero != null)
                            LoggerManager.Warning($"{fucName}: 角色选择模式不支持源角色参数，已忽略");
                        if (targetFavorHero != null)
                            LoggerManager.Warning($"{fucName}: 角色选择模式不支持目标角色参数，已忽略");
                        chooseController.ShowChoosePanel(
                            ChooseType.Hero,
                            candidateList,
                            __instance.gameObject,
                            callbackFuc,
                            callbackParam,
                            filterType,
                            cancelFuc
                        );
                        break;
                    }
                case ChooseType.HeroItem:
                    {
                        // HeroItem模式：手动初始化面板+填充物品，绕过ShowChoosePanel内部param解析
                        HeroData itemSourceHero = sourceHero ?? CommonHandlers.GetPlayerHero();
                        if (itemSourceHero == null)
                        {
                            LoggerManager.Error($"{fucName}: 无法获取物品来源角色");
                            return;
                        }

                        // 手动初始化面板
                        SetupChoosePanelCore(chooseController, ChooseType.HeroItem, __instance.gameObject,
                            callbackFuc, callbackParam, cancelFuc, targetFavorHero);

                        // 设置ItemList UI（显示ItemFlitter，隐藏SkillFlitter和HeroList）
                        SetupItemListUI(chooseController);

                        // 收集物品并填充
                        ItemListData itemListData = itemSourceHero.itemListData;
                        if (itemListData == null || itemListData.allItem == null || itemListData.allItem.Count == 0)
                        {
                            LoggerManager.Debug($"{fucName}: 角色 {itemSourceHero.heroName} 没有物品");
                            return;
                        }

                        // 保存原始上下文
                        ItemData originalPlotInteractItem = __instance.plotInteractItem;

                        try
                        {
                            // 简单筛选：根据filterType过滤 + 条件表达式过滤
                            var allItems = itemListData.allItem;
                            for (int i = 0; i < allItems.Count; i++)
                            {
                                ItemData item = allItems[i];
                                if (item == null) continue;
                                if (!PassItemFilter(item, filterType)) continue;

                                // 条件表达式筛选
                                if (conditionExpr != null)
                                {
                                    __instance.plotInteractItem = item;
                                    try
                                    {
                                        if (!ConditionExpressionEvaluator.Evaluate(__instance, conditionExpr))
                                            continue;
                                    }
                                    catch (System.Exception e)
                                    {
                                        LoggerManager.Warning($"{fucName}: 评估物品条件时出错: {e.Message}");
                                        continue;
                                    }
                                }

                                chooseController.CreateChooseItem(item, null);
                            }

                            // 如果IncludeStorage筛选类型，额外添加仓库物品
                            if (filterType == ChooseFilterType.IncludeStorage)
                            {
                                AddStorageItems(chooseController, __instance, conditionExpr);
                            }
                        }
                        finally
                        {
                            __instance.plotInteractItem = originalPlotInteractItem;
                        }

                        break;
                    }
                case ChooseType.HeroSkill:
                    {
                        // HeroSkill模式：手动初始化面板+填充武学，绕过ShowChoosePanel内部param解析
                        HeroData skillSourceHero = sourceHero ?? CommonHandlers.GetPlayerHero();
                        if (skillSourceHero == null)
                        {
                            LoggerManager.Error($"{fucName}: 无法获取武学来源角色");
                            return;
                        }

                        // 手动初始化面板
                        SetupChoosePanelCore(chooseController, ChooseType.HeroSkill, __instance.gameObject,
                            callbackFuc, callbackParam, cancelFuc, targetFavorHero);

                        // 设置SkillList UI（显示SkillFlitter，隐藏ItemFlitter和HeroList）
                        SetupSkillListUI(chooseController);

                        // 收集武学并填充
                        var kungfuSkills = skillSourceHero.kungfuSkills;
                        if (kungfuSkills == null || kungfuSkills.Count == 0)
                        {
                            LoggerManager.Debug($"{fucName}: 角色 {skillSourceHero.heroName} 没有武学");
                            return;
                        }

                        // 保存原始上下文
                        KungfuSkillLvData originalPlotInteractSkill = __instance.plotInteractSkill;

                        try
                        {
                            for (int i = 0; i < kungfuSkills.Count; i++)
                            {
                                KungfuSkillLvData skill = kungfuSkills[i];
                                if (skill == null) continue;
                                if (!PassSkillFilter(skill, filterType)) continue;

                                // 条件表达式筛选
                                if (conditionExpr != null)
                                {
                                    __instance.plotInteractSkill = skill;
                                    try
                                    {
                                        if (!ConditionExpressionEvaluator.Evaluate(__instance, conditionExpr))
                                            continue;
                                    }
                                    catch (System.Exception e)
                                    {
                                        LoggerManager.Warning($"{fucName}: 评估武学条件时出错: {e.Message}");
                                        continue;
                                    }
                                }

                                CreateChooseSkillIcon(chooseController, skill);
                            }
                        }
                        finally
                        {
                            __instance.plotInteractSkill = originalPlotInteractSkill;
                        }

                        break;
                    }
            }

            LoggerManager.Debug($"{fucName}: 已弹出选择面板, 类型={chooseType}, 筛选={filterType}, 条件={conditionExpr ?? "无"}, 源角色={sourceHeroStr ?? "null"}, 回调={callbackFuc}");
        }

        /// <summary>
        /// 手动初始化选择面板核心逻辑（复制ShowChoosePanel前半部分：Init/音效/动画/回调设置）
        /// 绕过ShowChoosePanel内部对List&lt;object&gt; param的解析
        /// </summary>
        private static void SetupChoosePanelCore(ChooseController cc, ChooseType chooseType,
            GameObject sendTarget, string sendFuc, string sendParam, string cancelFuc, HeroData targetFavorHero)
        {
            // 1. 延迟初始化
            if (!cc.inited) cc.Init();

            // 2. 播放"纸张"音效
            NGUITools.PlaySound(Resources.Load<AudioClip>("Sound/SoundEffect/Paper"), 1f, 1f);

            // 3. 清空旧选项
            GlobalData.DeleteAllChild(cc.chooseRoot);

            // 4. 激活面板
            cc.choosePanel.SetActive(true);

            // 5. 面板入场（直接设值，Il2CppInterop下DOTween扩展方法不可用）
            var panelRoot = cc.choosePanel.transform.Find("ChoosePanelRoot");
            if (panelRoot != null)
            {
                panelRoot.localPosition = Vector3.zero;
                panelRoot.localScale = Vector3.one;
            }

            var bg = cc.choosePanel.transform.Find("BlackBackground")?.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = new Color(0f, 0f, 0f, 0.5f);
            }

            // 6. 保存回调信息
            cc.sendResultFucTarget = sendTarget;
            cc.chooseType = chooseType;
            cc.sendResultFuc = sendFuc;
            cc.sendResultParam = sendParam;
            cc.cancelFuc = cancelFuc;
            cc.targetHero = targetFavorHero;
        }

        /// <summary>
        /// 设置ItemList UI：显示ItemFlitter，隐藏SkillFlitter，激活itemList，隐藏heroList
        /// </summary>
        private static void SetupItemListUI(ChooseController cc)
        {
            var itemFlitter = cc.itemList.transform.Find("ItemFlitter")?.gameObject;
            var skillFlitter = cc.itemList.transform.Find("SkillFlitter")?.gameObject;
            if (itemFlitter != null) itemFlitter.SetActive(true);
            if (skillFlitter != null) skillFlitter.SetActive(false);

            var viewport = cc.itemList.transform.Find("Viewport/Content");
            if (viewport != null) cc.targetGrid = viewport.gameObject;

            cc.itemList.SetActive(true);
            cc.heroList.SetActive(false);
        }

        /// <summary>
        /// 设置SkillList UI：显示SkillFlitter，隐藏ItemFlitter，激活itemList，隐藏heroList
        /// </summary>
        private static void SetupSkillListUI(ChooseController cc)
        {
            var itemFlitter = cc.itemList.transform.Find("ItemFlitter")?.gameObject;
            var skillFlitter = cc.itemList.transform.Find("SkillFlitter")?.gameObject;
            if (itemFlitter != null) itemFlitter.SetActive(false);
            if (skillFlitter != null) skillFlitter.SetActive(true);

            var viewport = cc.itemList.transform.Find("Viewport/Content");
            if (viewport != null) cc.targetGrid = viewport.gameObject;

            cc.itemList.SetActive(true);
            cc.heroList.SetActive(false);
        }

        /// <summary>
        /// 创建武学选项图标（模仿CreateChooseItem的模式，使用skillIconPrefab）
        /// </summary>
        private static void CreateChooseSkillIcon(ChooseController cc, KungfuSkillLvData skillLvData)
        {
            var icon = GlobalData.AddChild(cc.targetGrid, GameObjectController._instance.skillIconPrefab);
            cc.newObj = icon;

            var ctrl = icon.GetComponent<SkillIconController>();
            if (ctrl != null)
            {
                ctrl.skillLvData = skillLvData;
                ctrl.skillIconType = (SkillIconType)3; // ChoosePanel模式
            }
        }

        /// <summary>
        /// 物品筛选：根据ChooseFilterType判断物品是否应显示
        /// ItemType枚举值：0=Equip(装备), 1=Med(药品), 2=Food(食物), 3=Book(书籍), 4=Treasure(宝物), 5=Material(材料), 6=Horse(马匹)
        /// </summary>
        private static bool PassItemFilter(ItemData item, ChooseFilterType filterType)
        {
            if (item == null) return false;

            // ItemType 枚举值参考（与 ItemType.cs 保持一致）
            const int ITEMTYPE_EQUIP = 0;
            const int ITEMTYPE_MED = 1;
            const int ITEMTYPE_FOOD = 2;
            const int ITEMTYPE_BOOK = 3;
            const int ITEMTYPE_TREASURE = 4;
            const int ITEMTYPE_MATERIAL = 5;
            const int ITEMTYPE_HORSE = 6;

            int itemType = (int)item.type;

            switch (filterType)
            {
                case ChooseFilterType.None:
                    return true;

                case ChooseFilterType.BattleUseItem:
                    // 战斗可用物品：药品和食物
                    return itemType == ITEMTYPE_MED || itemType == ITEMTYPE_FOOD;

                case ChooseFilterType.CraftMaterialEquip:
                    // 锻造材料（装备类）
                    return itemType == ITEMTYPE_EQUIP;

                case ChooseFilterType.CraftMaterialMed:
                    // 炼药材料：药品类
                    return itemType == ITEMTYPE_MED;

                case ChooseFilterType.CraftMaterialFood:
                    // 烹饪材料
                    return itemType == ITEMTYPE_FOOD;

                case ChooseFilterType.Identify:
                    // 可鉴定物品：装备类
                    return itemType == ITEMTYPE_EQUIP;

                case ChooseFilterType.ReadBook:
                    // 可读书籍
                    return itemType == ITEMTYPE_BOOK;

                case ChooseFilterType.DrinkChoose:
                    // 可饮酒物品：材料类 subType == 1 (酒)
                    return itemType == ITEMTYPE_MATERIAL && item.subType == 1;

                case ChooseFilterType.GambleChoose:
                    // 赌博选择：非装备
                    return itemType != ITEMTYPE_EQUIP;

                case ChooseFilterType.NoEquip:
                    // 非装备类物品
                    return itemType != ITEMTYPE_EQUIP;

                case ChooseFilterType.IncludeStorage:
                    // 包含仓库：主列表不额外筛选，仓库物品在AddStorageItems中处理
                    return true;

                default:
                    // 未实现的筛选类型，默认通过（显示所有物品）
                    LoggerManager.Debug($"PassItemFilter: 未实现的筛选类型 {filterType}，默认显示所有物品");
                    return true;
            }
        }

        /// <summary>
        /// 武学筛选：根据ChooseFilterType判断武学是否应显示
        /// </summary>
        private static bool PassSkillFilter(KungfuSkillLvData skill, ChooseFilterType filterType)
        {
            if (skill == null) return false;

            switch (filterType)
            {
                case ChooseFilterType.None:
                    return true;

                case ChooseFilterType.SkillBreakThrough:
                    // 突破用武学：已装备的武学
                    return skill.equiped;

                case ChooseFilterType.CombineBook:
                    // 合并秘籍：暂显示所有（实际应检查是否可合并）
                    return true;

                case ChooseFilterType.BreakThroughBook:
                    // 突破用秘籍：已装备的
                    return skill.equiped;

                case ChooseFilterType.TeachNpc:
                case ChooseFilterType.TeachNpcNewSkill:
                    // 教NPC：暂显示所有
                    return true;

                case ChooseFilterType.StealSkill:
                    // 偷学武学：暂显示所有
                    return true;

                case ChooseFilterType.PlayerNoSkill:
                    // 玩家无此武学：暂显示所有（实际应排除玩家已学的）
                    return true;

                default:
                    LoggerManager.Debug($"PassSkillFilter: 未实现的筛选类型 {filterType}，默认显示所有武学");
                    return true;
            }
        }

        /// <summary>
        /// 添加仓库物品到选择列表（用于IncludeStorage筛选类型）
        /// </summary>
        private static void AddStorageItems(ChooseController cc, PlotController __instance, string conditionExpr)
        {
            try
            {
                // 获取势力仓库
                var externalStorage = GameDataController.ExternalStorage;
                if (externalStorage != null && externalStorage.allItem != null)
                {
                    foreach (var item in externalStorage.allItem)
                    {
                        if (item == null) continue;

                        // 条件表达式筛选
                        if (conditionExpr != null)
                        {
                            __instance.plotInteractItem = item;
                            try
                            {
                                if (!ConditionExpressionEvaluator.Evaluate(__instance, conditionExpr))
                                    continue;
                            }
                            catch (System.Exception e)
                            {
                                LoggerManager.Warning($"AddStorageItems: 评估仓库物品条件时出错: {e.Message}");
                                continue;
                            }
                        }

                        cc.CreateChooseItem(item, "仓库");
                    }
                }

                // 获取藏经阁
                WorldData worldData = CommonHandlers.GetWorldData();
                HeroData player = worldData != null ? worldData.Player() : null;
                if (player != null)
                {
                    var area = player.GetArea();
                    if (area != null)
                    {
                        var force = area.GetForce();
                        if (force != null && force.forceStorage != null && force.forceStorage.allItem != null)
                        {
                            foreach (var item in force.forceStorage.allItem)
                            {
                                if (item == null) continue;

                                // 条件表达式筛选
                                if (conditionExpr != null)
                                {
                                    __instance.plotInteractItem = item;
                                    try
                                    {
                                        if (!ConditionExpressionEvaluator.Evaluate(__instance, conditionExpr))
                                            continue;
                                    }
                                    catch (System.Exception e)
                                    {
                                        LoggerManager.Warning($"AddStorageItems: 评估藏经阁物品条件时出错: {e.Message}");
                                        continue;
                                    }
                                }

                                cc.CreateChooseItem(item, "藏经阁");
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                LoggerManager.Warning($"AddStorageItems: 添加仓库物品时出错: {e.Message}");
            }
        }
    }
}
