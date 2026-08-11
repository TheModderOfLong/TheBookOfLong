using HarmonyLib;
using System.Collections.Generic;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 修补龙之书的 CsvPatchApplier.CreateBlankRow 方法，
    /// 使空白占位行填充必要列值，避免 GameDataController.LoadAllGameData() 因空行崩溃。
    ///
    /// 问题原因：TheBookOfLongTokenDelimitersPatch 添加了 # 等分隔符后，
    /// 龙之书的符号化ID分组发生变化，EnsureSequentialRowsForGroup 可能为缺失ID生成空白占位行。
    /// 原始 CreateBlankRow 只有Key列有值，其余全空，导致游戏加载数据时 ArgumentOutOfRangeException。
    ///
    /// 注意：CsvPatchApplier 是 internal 类，无法使用 [HarmonyPatch(typeof(...))]，
    ///       因此通过 ModMain.OnInitializeMelon 中使用 AccessTools 反射手动 Patch。
    /// </summary>
    public static class CsvPatchApplierCreateBlankRowPatch
    {
        /// <summary>
        /// PlotData.csv 的列定义（索引从0开始）：
        /// 0:剧情编号 1:角色左 2:角色右 3:高亮方 4:背景图片 5:背景音乐
        /// 6:播放音效 7:PlotShock 8:调用函数 9:选项 10:内容
        /// 空白占位行至少需要填充 角色左="无"、高亮方="无"、内容="（空）" 来避免游戏崩溃
        /// </summary>
        public static void CreateBlankRowPostfix(ref List<string> __result, int columnCount, int keyColumnIndex, int numericId)
        {
            if (__result == null || __result.Count == 0)
                return;

            // 判断是否为 PlotData.csv 的占位行（11列，Key列索引为0）
            if (columnCount == 11 && keyColumnIndex == 0)
            {
                // 角色左（列1）= "无"
                if (__result.Count > 1)
                    __result[1] = "无";
                // 高亮方（列3）= "无"
                if (__result.Count > 3)
                    __result[3] = "无";
                // 内容（列10）= "（占位）"
                if (__result.Count > 10)
                    __result[10] = "（占位）";
            }
        }
    }
}
