using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppSystem.Collections.Generic;
using System.Reflection;
using System;

namespace TheExtensionOfLong
{
    [SpePlotFuc("GenerateRandomItem")]
    public static class SpePlotFucGenerateRandomItem
    {
public struct RandomItemParams
        {
            public int type;           // 默认-1: 物品类型(0~6); -1=随机
            public float shopLv;       // 默认0:  商店等级(Item体系); 0=游戏默认
            public float bossLv;       // 默认0:  boss等级(品质加成); 0=游戏默认
            public bool noRandom;      // 默认false: 缩小随机范围
            public int subType;        // 默认-1:  子类型; -1=随机
            public float itemValue;    // 默认0:  物品价值(Value体系); >0切换Value体系
            public int littleType;     // 默认-1:  武器小类型(仅Value体系)
            public int weaponType;     // 默认-1:  武器类型(仅Value体系)
            public bool forceBonus;    // 默认true: 是否启用势力科技加成
            public bool show;          // 默认true: 是否显示弹窗(HeroGetRandomItem专用)
            public string targetHeroStr; // 默认null: 目标角色ID/名称(GenerateRandomItem专用)

            public static RandomItemParams Default => new RandomItemParams
            {
                type = -1,
                shopLv = 0f,
                bossLv = 0f,
                noRandom = false,
                subType = -1,
                itemValue = 0f,
                littleType = -1,
                weaponType = -1,
                forceBonus = true,
                show = true,
                targetHeroStr = null,
            };
        }

        /// <summary>
        /// 从参数数组解析 RandomItemParams
        /// startIdx: 从第几个参数开始解析命名参数
        /// legacyShowCompat: 是否兼容旧格式（第二个位置参数无=时按show解析）
        /// </summary>
        public static RandomItemParams ParseRandomItemParams(string[] fucParams, int startIdx, bool legacyShowCompat)
        {
            var p = RandomItemParams.Default;

            for (int i = startIdx; i < fucParams.Length; i++)
            {
                string param = fucParams[i];
                if (string.IsNullOrWhiteSpace(param)) continue;

                int eqIdx = param.IndexOf('=');
                if (eqIdx < 0)
                {
                    // 旧格式兼容：第一个命名参数位置无=时按 show 解析
                    if (legacyShowCompat && i == startIdx)
                    {
                        string showStr = param.ToLower().Trim();
                        p.show = !(showStr == "false" || showStr == "0");
                    }
                    continue;
                }

                string key = param.Substring(0, eqIdx).Trim().ToLower();
                string val = param.Substring(eqIdx + 1).Trim();

                switch (key)
                {
                    case "show":
                        p.show = !(val == "false" || val == "0");
                        break;
                    case "type":
                        int.TryParse(val, out p.type);
                        break;
                    case "shoplv":
                        float.TryParse(val, out p.shopLv);
                        break;
                    case "bosslv":
                        float.TryParse(val, out p.bossLv);
                        break;
                    case "norandom":
                        p.noRandom = (val == "true" || val == "1");
                        break;
                    case "subtype":
                        int.TryParse(val, out p.subType);
                        break;
                    case "itemvalue":
                        float.TryParse(val, out p.itemValue);
                        break;
                    case "littletype":
                        int.TryParse(val, out p.littleType);
                        break;
                    case "weapontype":
                        int.TryParse(val, out p.weaponType);
                        break;
                    case "forcebonus":
                        p.forceBonus = !(val == "false" || val == "0");
                        break;
                    case "targethero":
                        if (!string.IsNullOrWhiteSpace(val))
                            p.targetHeroStr = val;
                        break;
                }
            }

            return p;
        }

        /// <summary>
        /// 根据 RandomItemParams 和目标角色生成随机物品
        /// targetHero: 用于势力加成和类型随机的角色引用，可为null
        /// </summary>
        public static ItemData GenerateRandomItemByParams(GameController gc, RandomItemParams p, HeroData targetHero, string fucName)
        {
            if (p.itemValue > 0f)
            {
                // ===== GenerateRandomItemValue 体系 =====
                if (p.type < 0)
                {
                    if (targetHero != null)
                        p.type = gc.GetHeroRandomGetItemType(targetHero, null);
                    else
                        p.type = UnityEngine.Random.Range(0, 7);
                }

                HeroData heroForCall = p.forceBonus ? targetHero : null;

                var itemData = gc.GenerateRandomItemValue(
                    p.itemValue, p.type, p.bossLv, p.subType, p.littleType, heroForCall, p.weaponType);

                LoggerManager.Debug($"{fucName}: [Value体系] itemValue={p.itemValue}, type={p.type}, " +
                    $"bossLv={p.bossLv}, subType={p.subType}, littleType={p.littleType}, " +
                    $"weaponType={p.weaponType}, forceBonus={p.forceBonus}, targetHero={p.targetHeroStr ?? "null"}");

                return itemData;
            }
            else
            {
                // ===== GenerateRandomItem 体系 =====
                HeroData heroForCall = p.forceBonus ? targetHero : null;

                ItemData itemData;
                if (p.type < 0)
                {
                    if (targetHero != null)
                    {
                        p.type = gc.GetHeroRandomGetItemType(targetHero, null);
                        itemData = gc.GenerateRandomItem(p.type, p.shopLv, p.bossLv, p.noRandom, p.subType, heroForCall, null);
                    }
                    else
                    {
                        // 无角色关联 → 使用最简重载
                        itemData = gc.GenerateRandomItem(p.shopLv, p.bossLv, p.noRandom, null);
                    }
                }
                else
                {
                    itemData = gc.GenerateRandomItem(p.type, p.shopLv, p.bossLv, p.noRandom, p.subType, heroForCall, null);
                }

                LoggerManager.Debug($"{fucName}: [Item体系] type={p.type}, " +
                    $"shopLv={p.shopLv}, bossLv={p.bossLv}, noRandom={p.noRandom}, subType={p.subType}, " +
                    $"forceBonus={p.forceBonus}, targetHero={p.targetHeroStr ?? "null"}");

                return itemData;
            }
        }

        /// <summary>
        /// 按参数生成随机物品，并将物品设为指定对象（以便后续通过 [$ItemData:xxx:对象$] 查询获取）
        /// 
        /// 格式:
        ///   GenerateRandomItem*[对象(可选)][#命名参数1#命名参数2#...]
        /// 
        /// 对象（第一个参数，可选，默认plotInteractItem）:
        ///   chosenItem / chooseItem / 选中物品    → 设为选择器选中物品（需chooseResult已存在）
        ///   plotInteractItem / 剧情交互物品       → 设为剧情交互物品（默认）
        /// 
        /// 命名参数（key=value，顺序不限，均可省略，与HeroGetRandomItem一致）:
        ///   type        = int   (默认-1)     物品类型: 0=装备,1=药品,2=食物,3=书籍,4=宝物,5=材料,6=马匹; -1=按随机类型
        ///   shopLv      = float (默认0)      商店等级(GenerateRandomItem体系); 0=使用游戏默认值
        ///   bossLv      = float (默认0)      boss等级(品质加成); 0=使用游戏默认值
        ///   noRandom    = bool  (默认false)  是否缩小随机范围(true=更高品质下限)
        ///   subType     = int   (默认-1)     子类型(装备部位0~4/食物类别/马匹0=马1=马铠); -1=随机
        ///   itemValue   = float (默认0)      物品价值(GenerateRandomItemValue体系); >0时使用Value体系
        ///   littleType  = int   (默认-1)     武器小类型(仅Value体系); -1=随机
        ///   weaponType  = int   (默认-1)     武器类型(仅Value体系); -1=随机
        ///   forceBonus  = bool  (默认true)   是否启用势力科技加成
        ///   targetHero  = string(默认空)     目标角色ID/名称，用于势力加成和类型随机; 不指定时无角色关联
        /// 
        /// 示例:
        ///   GenerateRandomItem                                            → 生成随机物品，设为剧情交互物品
        ///   GenerateRandomItem*plotInteractItem                           → 同上
        ///   GenerateRandomItem*chosenItem                                 → 生成随机物品，设为选中物品
        ///   GenerateRandomItem*plotInteractItem#type=3#shopLv=5           → 生成书籍，设为剧情交互物品
        ///   GenerateRandomItem*选中物品#itemValue=100#type=0              → Value体系生成装备，设为选中物品
        ///   GenerateRandomItem#type=0#subType=0#targetHero=player         → 生成武器（关联玩家势力加成）
        /// </summary>
        public static void TryCall(PlotController __instance, string fucName, string[] fucParams)
        {
            // 1. 解析对象类型（第一个参数，如果不含=则视为对象名）
            string targetObj = "plotInteractItem"; // 默认
            int paramStartIdx = 0;

            if (fucParams.Length > 0 && !string.IsNullOrWhiteSpace(fucParams[0]))
            {
                string firstParam = fucParams[0].Trim();
                // 如果第一个参数不含'='，视为对象名
                if (!firstParam.Contains("="))
                {
                    targetObj = firstParam.ToLower();
                    paramStartIdx = 1;
                }
            }

            // 解析对象类型
            const int ObjPlotInteractItem = 0;
            const int ObjChosenItem = 1;
            // 扩展时在此添加常量

            int objType = ObjPlotInteractItem;
            switch (targetObj.ToLower())
            {
                case "plotinteractitem":
                case "剧情交互物品":
                    objType = ObjPlotInteractItem;
                    break;
                case "chosenitem":
                case "chooseitem":
                case "选中物品":
                    objType = ObjChosenItem;
                    break;
                // 扩展时在此添加case
                default:
                    LoggerManager.Warning($"{fucName}: 未知对象类型 \"{targetObj}\"，支持: chosenItem/chooseItem/选中物品, plotInteractItem/剧情交互物品，默认使用plotInteractItem");
                    break;
            }

            // 2. 解析命名参数
            RandomItemParams p = ParseRandomItemParams(fucParams, paramStartIdx, legacyShowCompat: false);

            // 3. 解析目标角色（可选，用于势力加成和类型随机）
            HeroData targetHero = null;
            if (!string.IsNullOrWhiteSpace(p.targetHeroStr))
            {
                targetHero = CommonHandlers.ResolveHeroId(__instance, p.targetHeroStr);
                if (targetHero == null)
                {
                    LoggerManager.Warning($"{fucName}: 未找到目标角色 \"{p.targetHeroStr}\"，将不关联角色");
                }
            }

            // 4. 生成随机物品
            GameController gameController = GameController.Instance;
            if (gameController == null)
            {
                LoggerManager.Error($"{fucName}: GameController实例不存在");
                return;
            }

            ItemData itemData = GenerateRandomItemByParams(gameController, p, targetHero, fucName);
            if (itemData == null)
            {
                LoggerManager.Warning($"{fucName}: 随机物品生成失败");
                return;
            }

            // 5. 将物品设为指定对象
            string objName;
            if (objType == ObjChosenItem)
            {
                objName = "chosenItem";
                ChooseController chooseController = ChooseController._instance;
                if (chooseController == null || chooseController.chooseResult == null)
                {
                    LoggerManager.Warning($"{fucName}: 选择器未打开或无选中对象，无法设为chosenItem，改为设为plotInteractItem");
                    __instance.plotInteractItem = itemData;
                    objName = "plotInteractItem(fallback)";
                }
                else
                {
                    ItemIconController itemIconCtrl = chooseController.chooseResult.GetComponent<ItemIconController>();
                    if (itemIconCtrl == null)
                    {
                        LoggerManager.Warning($"{fucName}: chooseResult无ItemIconController，无法设为chosenItem，改为设为plotInteractItem");
                        __instance.plotInteractItem = itemData;
                        objName = "plotInteractItem(fallback)";
                    }
                    else
                    {
                        itemIconCtrl.itemData = itemData;
                    }
                }
            }
            else
            {
                objName = "plotInteractItem";
                __instance.plotInteractItem = itemData;
            }

            LoggerManager.Debug($"{fucName}: 已生成物品 {itemData.Name(true)} 并设为 {objName}");
        }
    }
}
