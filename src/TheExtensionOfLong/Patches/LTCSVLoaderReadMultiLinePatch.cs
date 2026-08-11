using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 修复 SpeHeroFaceData 第一列在原版中被当作 SpeHeroDataBase 列表序号使用的问题。
    /// 允许配置表继续使用真实角色ID，运行时在 CSV 进入 LTCSVLoader 前转换为原版需要的序号。
    /// </summary>
    [HarmonyPatch(typeof(LTCSVLoader), "ReadMultiLine", new Type[] { typeof(string) })]
    public static class LTCSVLoaderReadMultiLinePatch
    {
        private static readonly object Sync = new object();
        private static Dictionary<int, int> SpeHeroIdToListIndex = new Dictionary<int, int>();

        [HarmonyPrefix]
        public static void Prefix(ref string str)
        {
            if (string.IsNullOrEmpty(str))
                return;

            try
            {
                List<string[]> rows = CsvTextUtility.Parse(str);
                if (rows.Count == 0)
                    return;

                string[] header = rows[0];
                if (IsSpeHeroDataHeader(header))
                {
                    CacheSpeHeroListIndex(rows);
                    return;
                }

                if (IsSpeHeroFaceDataHeader(header))
                    RewriteSpeHeroFaceIds(rows, ref str);
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("LTCSVLoaderReadMultiLinePatch: 修正特殊角色捏脸表失败，保留原始CSV文本。异常=" + ex.Message);
            }
        }

        private static bool IsSpeHeroDataHeader(string[] header)
        {
            return HasHeaderPrefix(header, new string[]
            {
                "id", "名字", "性别", "门派", "武学流派", "等级"
            });
        }

        private static bool IsSpeHeroFaceDataHeader(string[] header)
        {
            return HasHeaderPrefix(header, new string[]
            {
                "id", "名字", "脸", "眼", "眉", "发", "嘴", "鼻", "发后", "男胡", "杂"
            });
        }

        private static bool HasHeaderPrefix(string[] header, string[] expected)
        {
            if (header == null || header.Length < expected.Length)
                return false;

            for (int i = 0; i < expected.Length; i++)
            {
                string value = header[i] == null ? string.Empty : header[i].Trim();
                if (!string.Equals(value, expected[i], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        private static void CacheSpeHeroListIndex(List<string[]> rows)
        {
            Dictionary<int, int> map = new Dictionary<int, int>();

            for (int i = 1; i < rows.Count; i++)
            {
                string[] row = rows[i];
                if (row == null || row.Length == 0)
                    continue;

                int heroId;
                if (!int.TryParse((row[0] ?? string.Empty).Trim(), out heroId))
                    continue;

                map[heroId] = i;
            }

            lock (Sync)
            {
                SpeHeroIdToListIndex = map;
            }

            LoggerManager.Debug("LTCSVLoaderReadMultiLinePatch: 已缓存特殊角色ID到列表序号映射。数量=" + map.Count);
        }

        private static void RewriteSpeHeroFaceIds(List<string[]> rows, ref string csvText)
        {
            Dictionary<int, int> map;
            lock (Sync)
            {
                map = new Dictionary<int, int>(SpeHeroIdToListIndex);
            }

            if (map.Count == 0)
            {
                LoggerManager.Debug("LTCSVLoaderReadMultiLinePatch: 尚未缓存SpeHeroData映射，跳过SpeHeroFaceData修正。");
                return;
            }

            int rewrittenCount = 0;
            for (int i = 1; i < rows.Count; i++)
            {
                string[] row = rows[i];
                if (row == null || row.Length == 0)
                    continue;

                int faceId;
                if (!int.TryParse((row[0] ?? string.Empty).Trim(), out faceId))
                    continue;

                if (faceId <= 0)
                    continue;

                int listIndex;
                if (!map.TryGetValue(faceId, out listIndex))
                    continue;

                if (listIndex == faceId)
                    continue;

                row[0] = listIndex.ToString();
                rewrittenCount++;
            }

            if (rewrittenCount <= 0)
                return;

            csvText = CsvTextUtility.Serialize(rows);
            LoggerManager.Debug("LTCSVLoaderReadMultiLinePatch: 已修正SpeHeroFaceData角色ID为列表序号。数量=" + rewrittenCount);
        }
    }
}
