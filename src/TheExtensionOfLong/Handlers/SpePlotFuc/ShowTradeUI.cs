using System;
using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 显示交易UI界面
    /// 格式: ShowTradeUI*targetType#targetItemListType#leftItemListType#rightItemListType#minItemLv=Int(可选)-maxItemLv=Int(可选)-useAreaItemPrice=boolean(可选)-noSell=boolean(可选)-speSellValueRate=float(可选)-speBuyValueRate=float(可选)
    ///
    /// targetType: TradeUIType枚举，支持int值(如0=Shop,1=Storage,2=ForceStorage,3=Give,4=GovernStorage)或枚举名(如Shop/Storage等)
    /// targetItemListType: ItemListType枚举，支持int值(如-1=None,0=EquipType,...,7=All)或枚举名(如EquipType/All等)
    /// leftItemListType/rightItemListType: 物品列表来源，格式为"类型:参数"
    ///   -1或空: 临时剧情商店(PlotController.tempPlotShop)，无参数
    ///   0: 角色背包(HeroData.itemListData)，参数为角色ID/名称
    ///   1: 角色仓库(HeroData.selfStorage)，参数为角色ID/名称
    /// 可选参数(以key=value格式，用-分隔):
    ///   minItemLv=Int         最小物品等级(默认0)
    ///   maxItemLv=Int         最大物品等级(默认99)
    ///   useAreaItemPrice=bool 是否使用区域物价(默认false)
    ///   noSell=bool           是否禁止出售(默认false)
    ///   speSellValueRate=float 特殊出售倍率(默认1.0)
    ///   speBuyValueRate=float  特殊购买倍率(默认1.0)
    ///
    /// 示例: ShowTradeUI*0#7#-1:0#0:player
    ///       ShowTradeUI*Shop#All#-1#0:player#minItemLv=1-maxItemLv=5-useAreaItemPrice=true
    ///       ShowTradeUI*3#-1#0:player#0:小白#noSell=true-speSellValueRate=1.5
    /// </summary>
    [SpePlotFuc("ShowTradeUI")]
    public static class SpePlotFucShowTradeUI
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 4)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*targetType#targetItemListType#leftItemListType#rightItemListType#可选参数]");
                return;
            }

            // ---- 1. 解析 targetType (TradeUIType) ----
            TradeUIType targetType;
            string targetTypeStr = fucParams[0].Trim();
            if (int.TryParse(targetTypeStr, out int targetTypeInt))
            {
                targetType = (TradeUIType)targetTypeInt;
            }
            else if (!Enum.TryParse<TradeUIType>(targetTypeStr, out targetType))
            {
                LoggerManager.Warning($"{fucName}: 无法识别的交易类型 \"{targetTypeStr}\"");
                return;
            }

            // ---- 2. 解析 targetItemListType (ItemListType) ----
            ItemListType targetItemListType;
            string itemListTypeStr = fucParams[1].Trim();
            if (int.TryParse(itemListTypeStr, out int itemListTypeInt))
            {
                targetItemListType = (ItemListType)itemListTypeInt;
            }
            else if (!Enum.TryParse<ItemListType>(itemListTypeStr, out targetItemListType))
            {
                LoggerManager.Warning($"{fucName}: 无法识别的物品列表类型 \"{itemListTypeStr}\"");
                return;
            }

            // ---- 3. 解析 leftItemListType (类型:参数) ----
            ItemListData leftItemList = CommonHandlers.ResolveItemListData(plotController, fucName, fucParams[2], "左侧");
            if (leftItemList == null)
            {
                LoggerManager.Warning($"{fucName}: 左侧物品列表解析失败");
                return;
            }

            // ---- 4. 解析 rightItemListType (类型:参数) ----
            ItemListData rightItemList = CommonHandlers.ResolveItemListData(plotController, fucName, fucParams[3], "右侧");
            if (rightItemList == null)
            {
                LoggerManager.Warning($"{fucName}: 右侧物品列表解析失败");
                return;
            }

            // ---- 5. 解析可选命名参数 ----
            int minItemLv = 0;
            int maxItemLv = 99;
            bool useAreaItemPrice = false;
            bool noSell = false;
            float speSellValueRate = 1f;
            float speBuyValueRate = 1f;

            if (fucParams.Length > 4 && !string.IsNullOrWhiteSpace(fucParams[4]))
            {
                string[] namedParams = fucParams[4].Split('-');
                for (int i = 0; i < namedParams.Length; i++)
                {
                    string param = namedParams[i].Trim();
                    if (string.IsNullOrWhiteSpace(param)) continue;

                    int eqIdx = param.IndexOf('=');
                    if (eqIdx < 0) continue;

                    string key = param.Substring(0, eqIdx).Trim().ToLower();
                    string val = param.Substring(eqIdx + 1).Trim();

                    switch (key)
                    {
                        case "minitemlv":
                            int.TryParse(val, out minItemLv);
                            break;
                        case "maxitemlv":
                            int.TryParse(val, out maxItemLv);
                            break;
                        case "useareaitemprice":
                            useAreaItemPrice = !(val == "false" || val == "0");
                            break;
                        case "nosell":
                            noSell = !(val == "false" || val == "0");
                            break;
                        case "spesellvaluerate":
                            float.TryParse(val, out speSellValueRate);
                            break;
                        case "spebuyvaluerate":
                            float.TryParse(val, out speBuyValueRate);
                            break;
                    }
                }
            }

            // ---- 6. 调用 TradeUIController.ShowTradeUI ----
            TradeUIController tradeUI = TradeUIController._instance;
            if (tradeUI == null)
            {
                LoggerManager.Error($"{fucName}: TradeUIController实例不存在");
                return;
            }

            tradeUI.ShowTradeUI(targetType, targetItemListType, leftItemList, rightItemList, minItemLv, maxItemLv, useAreaItemPrice, noSell, speSellValueRate, speBuyValueRate);
            LoggerManager.Debug($"{fucName}: 已显示交易界面 targetType={targetType}, targetItemListType={targetItemListType}, minItemLv={minItemLv}, maxItemLv={maxItemLv}, useAreaItemPrice={useAreaItemPrice}, noSell={noSell}, speSellValueRate={speSellValueRate}, speBuyValueRate={speBuyValueRate}");
        }
    }
}
