using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppSystem.Collections.Generic;
using System.Reflection;
using System;

namespace TheExtensionOfLong
{
    [SpePlotFuc("HeroGetRandomItem")]
    public static class SpePlotFucHeroGetRandomItem
    {
        /// <summary>
        /// 给指定角色生成并给予一个随机物品
        /// 
        /// 格式（向后兼容旧格式 + 新命名参数格式）:
        ///   HeroGetRandomItem*角色ID/角色名称/关键字[#命名参数1#命名参数2#...]
        /// 
        /// 命名参数（key=value，顺序不限，均可省略）:
        ///   show        = bool  (默认true)   是否显示获取弹窗
        ///   type        = int   (默认-1)     物品类型: 0=装备,1=药品,2=食物,3=书籍,4=宝物,5=材料,6=马匹; -1=按角色势力随机
        ///   shopLv      = float (默认0)      商店等级(GenerateRandomItem体系); 0=使用游戏默认值
        ///   bossLv      = float (默认0)      boss等级(品质加成); 0=使用游戏默认值
        ///   noRandom    = bool  (默认false)  是否缩小随机范围(true=更高品质下限)
        ///   subType     = int   (默认-1)     子类型(装备部位0~4/食物类别/马匹0=马1=马铠); -1=随机
        ///   itemValue   = float (默认0)      物品价值(GenerateRandomItemValue体系); >0时使用Value体系
        ///   littleType  = int   (默认-1)     武器小类型(仅Value体系); -1=随机
        ///   weaponType  = int   (默认-1)     武器类型(仅Value体系); -1=随机
        ///   forceBonus  = bool  (默认true)   是否启用势力科技加成
        /// 
        /// 旧格式兼容（仅前两个参数）:
        ///   HeroGetRandomItem*角色ID/名称[#是否展示]
        ///   第二个参数若不是 key=value 格式，按旧格式解析为 show
        /// 
        /// 示例:
        ///   HeroGetRandomItem*player                              (旧格式，完全兼容)
        ///   HeroGetRandomItem*player#false                        (旧格式，不显示弹窗)
        ///   HeroGetRandomItem*player#show=false                   (新格式，同上)
        ///   HeroGetRandomItem*小白#type=3#shopLv=5                (指定类型=书籍，商店等级=5)
        ///   HeroGetRandomItem*小白#itemValue=100#type=0#subType=0#weaponType=2  (Value体系：价值100的武器)
        ///   HeroGetRandomItem*player#bossLv=2#noRandom=true       (高boss等级+缩小随机)
        /// </summary>
        public static void TryCall(PlotController __instance, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 1 || string.IsNullOrWhiteSpace(fucParams[0]))
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*角色ID/角色名称/关键字[#命名参数...]]");
                return;
            }

            // 1. 解析角色（第一个参数始终是角色引用）
            HeroData hero = CommonHandlers.ResolveHeroId(__instance, fucParams[0]);
            if (hero == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到角色 \"{fucParams[0]}\"");
                return;
            }

            // 2. 解析参数（兼容旧格式 + 命名参数格式）
            SpePlotFucGenerateRandomItem.RandomItemParams p = SpePlotFucGenerateRandomItem.ParseRandomItemParams(fucParams, 1, legacyShowCompat: true);

            // 3. 生成随机物品
            GameController gameController = GameController.Instance;
            if (gameController == null)
            {
                LoggerManager.Error($"{fucName}: GameController实例不存在");
                return;
            }

            ItemData itemData = SpePlotFucGenerateRandomItem.GenerateRandomItemByParams(gameController, p, hero, fucName);
            if (itemData == null)
            {
                LoggerManager.Warning($"{fucName}: 随机物品生成失败");
                return;
            }

            // 4. 给予角色
            hero.GetItem(itemData, p.show);

            LoggerManager.Debug($"{fucName}: 已给 {hero.heroName}(ID={hero.heroID}) 物品 {itemData.Name(true)}, show={p.show}");
        }
    }
}
