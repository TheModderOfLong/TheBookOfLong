using Il2Cpp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheExtensionOfLong
{
    public static class StringListManager
    {
        private const string ListKeyPrefix = "StrList_";
        private const string SeparatorKeySuffix = "_SEP";
        private const string StorageSeparator = "\u2016";
        private const string DefaultDisplaySeparator = "-";

        private static readonly Random Random = new Random();

        private enum AddMode
        {
            Unique = 0,
            Head = 1,
            Tail = 2,
        }

        private enum RemoveMode
        {
            All = 0,
            Head = 1,
            Tail = 2,
        }

        private enum GetMode
        {
            All = 0,
            Head = 1,
            Tail = 2,
            Random = 3,
            At = 4,
        }

        public static void HandleAdd(PlotController plotController, string fucName, string[] args)
        {
            if (args.Length < 2)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*元素#列表ID#添加模式(可选)]");
                return;
            }

            string elementPart = args[0];
            string listId = args[1];
            if (!TryValidateListId(fucName, listId))
                return;

            AddMode mode = AddMode.Unique;
            if (args.Length > 2 && !TryParseEnumArg(args[2], AddMode.Unique, out mode))
                LoggerManager.Warning($"{fucName}: 未知添加模式 \"{args[2]}\"，使用默认 Unique");

            List<string> elements = ParseElementArgs(elementPart);
            if (elements.Count == 0)
            {
                LoggerManager.Warning($"{fucName}: 未提供有效元素");
                return;
            }

            List<string> items = GetList(listId);
            int beforeCount = items.Count;

            for (int i = 0; i < elements.Count; i++)
            {
                string element = elements[i];
                switch (mode)
                {
                    case AddMode.Head:
                        items.Insert(0, element);
                        break;
                    case AddMode.Tail:
                        items.Add(element);
                        break;
                    default:
                        if (!items.Contains(element))
                            items.Add(element);
                        break;
                }
            }

            SaveList(listId, items);
            LoggerManager.Debug($"{fucName}: 列表 \"{listId}\" 添加完成，模式={mode}，元素数 {beforeCount}->{items.Count}");
        }

        public static void HandleRemove(PlotController plotController, string fucName, string[] args)
        {
            if (args.Length < 2)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*元素#列表ID#移除模式(可选)]");
                return;
            }

            string elementPart = args[0];
            string listId = args[1];
            if (!TryValidateListId(fucName, listId))
                return;

            List<string> items = GetList(listId);
            if (items.Count == 0)
                return;

            RemoveMode mode = RemoveMode.All;
            if (args.Length > 2 && !TryParseEnumArg(args[2], RemoveMode.All, out mode))
                LoggerManager.Warning($"{fucName}: 未知移除模式 \"{args[2]}\"，使用默认 All");

            List<string> targets = ParseElementArgs(elementPart);
            if (targets.Count == 0)
            {
                LoggerManager.Warning($"{fucName}: 未提供有效元素");
                return;
            }

            int beforeCount = items.Count;
            for (int i = 0; i < targets.Count; i++)
            {
                string target = targets[i];
                switch (mode)
                {
                    case RemoveMode.Head:
                        RemoveFirst(items, target);
                        break;
                    case RemoveMode.Tail:
                        RemoveLast(items, target);
                        break;
                    default:
                        items.RemoveAll(item => item == target);
                        break;
                }
            }

            SaveList(listId, items);
            LoggerManager.Debug($"{fucName}: 列表 \"{listId}\" 移除完成，模式={mode}，元素数 {beforeCount}->{items.Count}");
        }

        public static void HandleClear(PlotController plotController, string fucName, string[] args)
        {
            if (args.Length < 1)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*列表ID]");
                return;
            }

            string listId = args[0];
            if (!TryValidateListId(fucName, listId))
                return;

            PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
            if (logData == null)
            {
                LoggerManager.Error($"{fucName}: PlotEventLogData实例不存在，无法清空列表");
                return;
            }

            logData.Set(StorageKey(listId), null);
            logData.Set(SeparatorKey(listId), null);
            LoggerManager.Debug($"{fucName}: 已清空列表 \"{listId}\"");
        }

        public static void HandleSetSeparator(PlotController plotController, string fucName, string[] args)
        {
            if (args.Length < 2)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*列表ID#新分隔符]");
                return;
            }

            string listId = args[0];
            if (!TryValidateListId(fucName, listId))
                return;

            if (!HasList(listId))
            {
                LoggerManager.Warning($"{fucName}: 列表 \"{listId}\" 不存在，无法更换分隔符");
                return;
            }

            string separator = args[1];
            if (string.IsNullOrEmpty(separator))
            {
                LoggerManager.Warning($"{fucName}: 分隔符不能为空");
                return;
            }

            string oldSeparator = GetDisplaySeparator(listId);
            if (oldSeparator == separator)
                return;

            SetDisplaySeparator(listId, separator);
            LoggerManager.Debug($"{fucName}: 列表 \"{listId}\" 展示分隔符 \"{oldSeparator}\" -> \"{separator}\"");
        }

        public static string Query(PlotController plotController, string[] parts)
        {
            if (parts == null || parts.Length < 3)
            {
                LoggerManager.Warning("  StrList查询: 参数不足，格式[$StrList:字段名=参数:列表ID$]");
                return "";
            }

            string listId = parts[parts.Length - 1];
            if (string.IsNullOrWhiteSpace(listId))
            {
                LoggerManager.Warning("  StrList查询: 列表ID不能为空");
                return "";
            }

            string fieldPart = parts[1] ?? "";
            string fieldName = fieldPart;
            string[] args = new string[0];
            int eqIndex = fieldPart.IndexOf('=');
            if (eqIndex >= 0)
            {
                fieldName = fieldPart.Substring(0, eqIndex);
                args = SplitListArgs(fieldPart.Substring(eqIndex + 1));
                for (int i = 0; i < args.Length; i++)
                    args[i] = PlotCommandHandler.StripParens(args[i]).Trim();
            }

            switch ((fieldName ?? "").Trim().ToLowerInvariant())
            {
                case "count":
                    return GetList(listId).Count.ToString();
                case "contains":
                    return QueryContains(listId, args);
                case "get":
                    return QueryGet(listId, args);
                case "index":
                    return QueryIndex(listId, args);
                case "sep":
                    return HasList(listId) ? GetDisplaySeparator(listId) : "";
                default:
                    LoggerManager.Warning($"  StrList查询: 未知字段 \"{fieldName}\"");
                    return "";
            }
        }

        private static string QueryContains(string listId, string[] args)
        {
            if (args.Length < 1 || string.IsNullOrEmpty(args[0]))
                return "0";

            return GetList(listId).Contains(args[0]) ? "1" : "0";
        }

        private static string QueryIndex(string listId, string[] args)
        {
            if (args.Length < 1 || string.IsNullOrEmpty(args[0]))
                return "-1";

            List<string> items = GetList(listId);
            int index = items.IndexOf(args[0]);
            return index >= 0 ? (index + 1).ToString() : "-1";
        }

        private static string QueryGet(string listId, string[] args)
        {
            if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                LoggerManager.Warning("  StrList Get: 缺少模式参数");
                return "";
            }

            GetMode mode = GetMode.All;
            if (!TryParseEnumArg(args[0], GetMode.All, out mode))
                LoggerManager.Warning($"  StrList Get: 未知模式 \"{args[0]}\"，使用默认 All");

            List<string> items = GetList(listId);
            if (items.Count == 0)
                return "";

            if (mode == GetMode.All)
                return JoinForDisplay(listId, items);

            int n = 1;
            if (args.Length > 1 && !int.TryParse(args[1], out n))
            {
                LoggerManager.Warning($"  StrList Get: N参数 \"{args[1]}\" 不是有效整数，使用默认1");
                n = 1;
            }

            if (n <= 0)
                return "";

            List<string> selected = new List<string>();
            switch (mode)
            {
                case GetMode.Head:
                    selected.AddRange(items.Take(n));
                    break;
                case GetMode.Tail:
                    selected.AddRange(items.Skip(Math.Max(0, items.Count - n)));
                    break;
                case GetMode.Random:
                    selected.AddRange(TakeRandom(items, n));
                    break;
                case GetMode.At:
                    if (n >= 1 && n <= items.Count)
                        selected.Add(items[n - 1]);
                    break;
            }

            return JoinForDisplay(listId, selected);
        }

        private static List<string> TakeRandom(List<string> items, int count)
        {
            int takeCount = Math.Min(count, items.Count);
            List<string> shuffled = new List<string>(items);
            for (int i = shuffled.Count - 1; i >= shuffled.Count - takeCount; i--)
            {
                int j = Random.Next(i + 1);
                string temp = shuffled[i];
                shuffled[i] = shuffled[j];
                shuffled[j] = temp;
            }

            return shuffled.Skip(shuffled.Count - takeCount).ToList();
        }

        private static string JoinForDisplay(string listId, List<string> items)
        {
            if (items == null || items.Count == 0)
                return "";

            return string.Join(GetDisplaySeparator(listId), items.ToArray());
        }

        private static List<string> GetList(string listId)
        {
            string raw = GetRawValue(listId);
            if (string.IsNullOrEmpty(raw))
                return new List<string>();

            return raw.Split(new[] { StorageSeparator }, StringSplitOptions.None).ToList();
        }

        private static void SaveList(string listId, List<string> items)
        {
            string encoded = items == null || items.Count == 0
                ? ""
                : string.Join(StorageSeparator, items.ToArray());
            SetRawValue(listId, encoded);
        }

        private static string GetRawValue(string listId)
        {
            PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
            if (logData == null)
                return "";

            string key = StorageKey(listId);
            return logData.HaveKey(key) ? (logData.Get(key) ?? "") : "";
        }

        private static void SetRawValue(string listId, string value)
        {
            PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
            if (logData == null)
                return;

            logData.Set(StorageKey(listId), value);
        }

        private static string GetDisplaySeparator(string listId)
        {
            PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
            if (logData == null)
                return DefaultDisplaySeparator;

            string key = SeparatorKey(listId);
            if (!logData.HaveKey(key))
                return DefaultDisplaySeparator;

            string separator = logData.Get(key);
            return string.IsNullOrEmpty(separator) ? DefaultDisplaySeparator : separator;
        }

        private static void SetDisplaySeparator(string listId, string separator)
        {
            PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
            if (logData == null)
                return;

            string key = SeparatorKey(listId);
            logData.Set(key, separator == DefaultDisplaySeparator ? null : separator);
        }

        private static bool HasList(string listId)
        {
            PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
            return logData != null && logData.HaveKey(StorageKey(listId));
        }

        private static bool TryValidateListId(string fucName, string listId)
        {
            if (!string.IsNullOrWhiteSpace(listId))
                return true;

            LoggerManager.Warning($"{fucName}: 列表ID不能为空");
            return false;
        }

        private static List<string> ParseElementArgs(string input)
        {
            string[] rawParts = SplitListArgs(input);
            List<string> result = new List<string>();
            for (int i = 0; i < rawParts.Length; i++)
            {
                string element = PlotCommandHandler.StripParens(rawParts[i]).Trim();
                if (!string.IsNullOrEmpty(element))
                    result.Add(element);
            }

            return result;
        }

        private static string[] SplitListArgs(string input)
        {
            return string.IsNullOrEmpty(input)
                ? new string[0]
                : PlotCommandHandler.SplitRespectingBraces(input, '-');
        }

        private static bool TryParseEnumArg<TEnum>(string input, TEnum defaultValue, out TEnum value) where TEnum : struct
        {
            value = defaultValue;
            if (string.IsNullOrWhiteSpace(input))
                return true;

            if (Enum.TryParse(input.Trim(), true, out value) && Enum.IsDefined(typeof(TEnum), value))
                return true;

            string numberText = input.Trim().Replace("负", "-");
            if (int.TryParse(numberText, out int intValue) && Enum.IsDefined(typeof(TEnum), intValue))
            {
                value = (TEnum)Enum.ToObject(typeof(TEnum), intValue);
                return true;
            }

            value = defaultValue;
            return false;
        }

        private static void RemoveFirst(List<string> items, string target)
        {
            int index = items.IndexOf(target);
            if (index >= 0)
                items.RemoveAt(index);
        }

        private static void RemoveLast(List<string> items, string target)
        {
            int index = items.LastIndexOf(target);
            if (index >= 0)
                items.RemoveAt(index);
        }

        private static string StorageKey(string listId)
        {
            return ListKeyPrefix + listId.Trim();
        }

        private static string SeparatorKey(string listId)
        {
            return ListKeyPrefix + listId.Trim() + SeparatorKeySuffix;
        }
    }
}
