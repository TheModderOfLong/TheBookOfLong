using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Newtonsoft.Json;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 管理剧情定时器的创建、持久化、每日检查、触发执行以及传闻显示镜像同步。
    /// </summary>
    public static class TimerManager
    {
        private const string TimerStorageKey = "system_timers";
        private const string LastTimerStorageKey = "system_last_timer";
        private const int StorageVersion = 1;

        private static bool _cacheLoaded;
        private static List<TimerData> _timerCache = new List<TimerData>();
        private static int _nextDueAbsDay = int.MaxValue;
        private static PlotEventLogData _boundPlotEventLogData;
        private static TimerData _lastTriggeredTimer;
        private static int _lastCheckedAbsDay = -1;
        private static int _lastObservedAbsDay = int.MinValue;
        private static readonly Dictionary<string, EventData> _watcherEvents = new Dictionary<string, EventData>();
        private static bool _syncingWatchers;

        /// <summary>
        /// 单个定时器的持久化数据，记录目标日期、回调指令、参数以及可选的传闻监听配置。
        /// </summary>
        public class TimerData
        {
            public string Id;
            public int Type;
            public int CreatedAbsDay;
            public string OriginalTimeParam;
            public int TargetAbsDay;
            public string Callback;
            public string Param;
            public string CustomParam;
            public bool ForceUpdateOnSet;
            public bool TriggerImmediatelyOnSet;
            public TimerWatcherData Watcher;
        }

        /// <summary>
        /// 定时器传闻监听配置，用于将指定定时器映射为运行时的传闻 EventData。
        /// </summary>
        public class TimerWatcherData
        {
            public bool Show;
            public string Title;
            public string Describe;
            public float RareLevel;
        }

        /// <summary>
        /// 定时器存档包装结构，保留版本号以便后续兼容升级。
        /// </summary>
        private class TimerStorageData
        {
            public int Version = StorageVersion;
            public List<TimerData> Timers = new List<TimerData>();
        }

        /// <summary>
        /// 处理 SetTimer 指令，创建、更新、删除或立即触发指定剧情定时器。
        /// </summary>
        public static void HandleSetTimer(PlotController pc, string fucName, string[] args)
        {
            if (args == null || args.Length < 3)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*定时器ID#类型#时间参数#回调函数名(可选)#函数参数(可选)#自定义参数(可选)#是否强制更新(可选)#是否立即触发(可选)]");
                return;
            }

            string id = (args[0] ?? "").Trim();
            if (string.IsNullOrEmpty(id))
            {
                LoggerManager.Warning($"{fucName}: 定时器ID不能为空");
                return;
            }

            if (!int.TryParse(args[1], out int type) || (type != 0 && type != 1))
            {
                LoggerManager.Warning($"{fucName}: 定时器类型无效 {args[1]}，仅支持 0/1");
                return;
            }

            string timeParam = args[2] ?? "";
            string callback = args.Length > 3 ? args[3] ?? "" : "";
            string param = args.Length > 4 ? args[4] ?? "" : "";
            string customParam = args.Length > 5 ? args[5] ?? "" : "";
            bool forceUpdate = args.Length > 6 && ParseBoolLike(args[6], false);
            bool triggerImmediately = args.Length > 7 && ParseBoolLike(args[7], false);

            EnsureCacheLoaded();
            TimerData existed = _timerCache.FirstOrDefault(t => t != null && t.Id == id);
            TimerWatcherData oldWatcher = CloneWatcher(existed?.Watcher);

            if (IsRemoveRequest(type, timeParam))
            {
                if (existed != null && forceUpdate)
                {
                    _timerCache.Remove(existed);
                    SaveCacheToStorage();
                    SyncTimerWatchers();
                    LoggerManager.Debug($"{fucName}: 已删除定时器 {id}");
                }
                else
                {
                    LoggerManager.Debug($"{fucName}: 定时器 {id} 删除请求未生效，原因: 不存在或未强制更新");
                }
                return;
            }

            if (!TryBuildTargetAbsDay(type, timeParam, out int targetAbsDay))
            {
                LoggerManager.Warning($"{fucName}: 时间参数无效 {timeParam}");
                return;
            }

            if (existed != null && !forceUpdate)
            {
                LoggerManager.Debug($"{fucName}: 定时器 {id} 已存在，未强制更新，跳过");
                return;
            }

            if (existed != null)
            {
                _timerCache.Remove(existed);
            }

            TimerData newTimer = new TimerData
            {
                Id = id,
                Type = type,
                CreatedAbsDay = GetCurrentAbsDay(),
                OriginalTimeParam = timeParam,
                TargetAbsDay = targetAbsDay,
                Callback = callback,
                Param = param,
                CustomParam = customParam,
                ForceUpdateOnSet = forceUpdate,
                TriggerImmediatelyOnSet = triggerImmediately,
                Watcher = oldWatcher
            };

            if (triggerImmediately && targetAbsDay <= GetCurrentAbsDay())
            {
                SaveCacheToStorage();
                SyncTimerWatchers();
                ExecuteTimer(newTimer);
                LoggerManager.Debug($"{fucName}: 定时器 {id} 立即触发");
                return;
            }

            _timerCache.Add(newTimer);
            SaveCacheToStorage();
            SyncTimerWatchers();
            LoggerManager.Debug($"{fucName}: 已设置定时器 {id}，目标={FormatAbsDay(targetAbsDay)}");
        }

        /// <summary>
        /// 处理 RemoveTimer 指令，按 ID 移除指定定时器并同步清理其传闻镜像。
        /// </summary>
        public static void HandleRemoveTimer(PlotController pc, string fucName, string[] args)
        {
            if (args == null || args.Length < 1)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*定时器ID]");
                return;
            }

            string id = (args[0] ?? "").Trim();
            if (string.IsNullOrEmpty(id))
            {
                LoggerManager.Warning($"{fucName}: 定时器ID不能为空");
                return;
            }

            EnsureCacheLoaded();
            TimerData existed = _timerCache.FirstOrDefault(t => t != null && t.Id == id);
            if (existed == null)
            {
                LoggerManager.Debug($"{fucName}: 定时器 {id} 不存在，无需删除");
                return;
            }

            _timerCache.Remove(existed);
            SaveCacheToStorage();
            SyncTimerWatchers();
            LoggerManager.Debug($"{fucName}: 已删除定时器 {id}");
        }

        /// <summary>
        /// 处理 SetTimerWatcher 指令，为已存在的定时器开启、更新或关闭传闻显示监听。
        /// </summary>
        public static void HandleSetTimerWatcher(PlotController pc, string fucName, string[] args)
        {
            if (args == null || args.Length < 2)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*定时器ID#是否显示#标题(显示时必填)#内容(显示时必填)#稀有等级(可选)]");
                return;
            }

            string id = (args[0] ?? "").Trim();
            if (string.IsNullOrEmpty(id))
            {
                LoggerManager.Warning($"{fucName}: 定时器ID不能为空");
                return;
            }

            if (!TryParseBoolLike(args[1], out bool show))
            {
                LoggerManager.Warning($"{fucName}: 是否显示参数无效 {args[1]}，仅支持 true/false/1/0");
                return;
            }

            EnsureCacheLoaded();
            TimerData timer = _timerCache.FirstOrDefault(t => t != null && t.Id == id);
            if (timer == null)
            {
                LoggerManager.Warning($"{fucName}: 定时器 {id} 不存在，无法设置传闻监听");
                return;
            }

            if (!show)
            {
                timer.Watcher = null;
                SaveCacheToStorage();
                SyncTimerWatchers();
                LoggerManager.Debug($"{fucName}: 已关闭定时器 {id} 的传闻监听");
                return;
            }

            string title = args.Length > 2 ? (args[2] ?? "").Trim() : "";
            string describe = args.Length > 3 ? (args[3] ?? "").Trim() : "";
            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(describe))
            {
                LoggerManager.Warning($"{fucName}: 是否显示=1 时标题和内容必填");
                return;
            }

            float rareLevel = 0f;
            if (args.Length > 4 && !string.IsNullOrWhiteSpace(args[4]) && !float.TryParse(args[4], out rareLevel))
            {
                rareLevel = 0f;
                LoggerManager.Debug($"{fucName}: 稀有等级 {args[4]} 解析失败，使用默认值 0");
            }

            timer.Watcher = new TimerWatcherData
            {
                Show = true,
                Title = title,
                Describe = describe,
                RareLevel = rareLevel
            };

            SaveCacheToStorage();
            SyncTimerWatchers();
            LoggerManager.Debug($"{fucName}: 已设置定时器 {id} 的传闻监听，标题={title}，剩余={Math.Max(0, timer.TargetAbsDay - GetCurrentAbsDay())}天");
        }

        /// <summary>
        /// 在新游戏或读档完成后重新绑定世界数据，并从存档数据恢复定时器缓存和传闻镜像。
        /// </summary>
        public static void InitializeCacheAfterGameLoaded(WorldData worldData)
        {
            _timerCache.Clear();
            _cacheLoaded = false;
            _nextDueAbsDay = int.MaxValue;
            _boundPlotEventLogData = worldData?.PlotEventLog;
            _lastTriggeredTimer = null;
            _lastCheckedAbsDay = -1;

            EnsureCacheLoaded();
            SyncTimerWatchers();
        }

        /// <summary>
        /// 清空运行时定时器缓存和传闻镜像，用于退出或切换世界数据时避免旧状态残留。
        /// </summary>
        public static void ResetCache()
        {
            ClearTimerWatcherEvents();
            _timerCache.Clear();
            _cacheLoaded = false;
            _nextDueAbsDay = int.MaxValue;
            _boundPlotEventLogData = null;
            _lastTriggeredTimer = null;
            _lastCheckedAbsDay = -1;
            _lastObservedAbsDay = int.MinValue;
        }

        /// <summary>
        /// 确保定时器缓存已经从 PlotEventLog 中加载，且在世界数据切换时自动重载。
        /// </summary>
        public static void EnsureCacheLoaded()
        {
            PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
            if (_cacheLoaded && _boundPlotEventLogData == logData)
            {
                return;
            }

            _boundPlotEventLogData = logData;
            ReloadCacheFromStorage();
            _cacheLoaded = true;
            RecalculateNextDueAbsDay();
        }

        /// <summary>
        /// 每日推进后检查所有定时器，触发已到期项并保留未到期项。
        /// </summary>
        public static void CheckTimers()
        {
            try
            {
                EnsureCacheLoaded();
                int currentAbsDay = GetCurrentAbsDay();

                LoggerManager.Debug($"SetTimer: 每日检查开始，当前={FormatAbsDay(currentAbsDay)}({currentAbsDay})，定时器数量={_timerCache.Count}，下次到期={FormatNextDueForLog(currentAbsDay)}");

                if (_timerCache.Count == 0)
                {
                    SyncTimerWatchersInternal(currentAbsDay);
                    LoggerManager.Debug("SetTimer: 每日检查结束，当前没有定时器");
                    return;
                }

                if (_lastCheckedAbsDay == currentAbsDay)
                {
                    SyncTimerWatchersInternal(currentAbsDay);
                    LoggerManager.Debug($"SetTimer: 每日检查跳过，{FormatAbsDay(currentAbsDay)} 已检查过");
                    return;
                }

                _lastCheckedAbsDay = currentAbsDay;

                if (currentAbsDay < _nextDueAbsDay)
                {
                    SyncTimerWatchersInternal(currentAbsDay);
                    LoggerManager.Debug($"SetTimer: 每日检查结束，未到期；下次到期={FormatAbsDay(_nextDueAbsDay)}，剩余={_nextDueAbsDay - currentAbsDay}天，最近定时器={DescribeTimer(GetNextDueTimer(), currentAbsDay)}");
                    return;
                }

                List<TimerData> remain = new List<TimerData>();
                List<TimerData> due = new List<TimerData>();

                foreach (TimerData timer in _timerCache)
                {
                    if (timer == null) continue;
                    if (timer.TargetAbsDay <= currentAbsDay)
                        due.Add(timer);
                    else
                        remain.Add(timer);
                }

                if (due.Count == 0)
                {
                    RecalculateNextDueAbsDay();
                    SyncTimerWatchersInternal(currentAbsDay);
                    LoggerManager.Debug($"SetTimer: 每日检查结束，未找到到期定时器；重新计算下次到期={FormatNextDueForLog(currentAbsDay)}");
                    return;
                }

                LoggerManager.Debug($"SetTimer: 每日检查发现到期定时器 {due.Count} 个，保留 {remain.Count} 个；到期列表={DescribeTimerList(due, currentAbsDay)}");

                _timerCache = remain;
                SaveCacheToStorage();
                SyncTimerWatchersInternal(currentAbsDay);

                foreach (TimerData timer in due)
                {
                    LoggerManager.Debug($"SetTimer: 准备触发定时器 {DescribeTimer(timer, currentAbsDay)}");
                    ExecuteTimer(timer);
                }

                LoggerManager.Debug($"SetTimer: 每日检查结束，已触发 {due.Count} 个定时器，剩余 {_timerCache.Count} 个，下次到期={FormatNextDueForLog(currentAbsDay)}");
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"SetTimer: 每日检查异常: {ex}");
            }
        }

        /// <summary>
        /// 兜底检查世界日期是否发生变化，适用于非 ChangeDay 路径造成的日期推进。
        /// </summary>
        public static void CheckTimersOnDateChanged()
        {
            try
            {
                WorldData worldData = CommonHandlers.GetWorldData();
                if (worldData?.worldTime == null) return;

                int currentAbsDay = GetCurrentAbsDay();
                if (_lastObservedAbsDay == int.MinValue)
                {
                    _lastObservedAbsDay = currentAbsDay;
                    return;
                }

                if (_lastObservedAbsDay == currentAbsDay) return;

                _lastObservedAbsDay = currentAbsDay;
                CheckTimers();
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"SetTimer: 日期变化轮询异常: {ex}");
            }
        }

        /// <summary>
        /// 获取当前有效定时器数量。
        /// </summary>
        public static int GetTimerCount()
        {
            EnsureCacheLoaded();
            return _timerCache.Count;
        }

        /// <summary>
        /// 按定时器 ID 查询定时器数据。
        /// </summary>
        public static bool TryGetTimer(string id, out TimerData timer)
        {
            EnsureCacheLoaded();
            timer = null;
            if (string.IsNullOrEmpty(id)) return false;

            timer = _timerCache.FirstOrDefault(t => t != null && t.Id == id);
            return timer != null;
        }

        /// <summary>
        /// 将查询引用解析为定时器，支持 last、next、序号和定时器 ID。
        /// </summary>
        public static TimerData ResolveTimerRef(string timerRef)
        {
            EnsureCacheLoaded();
            string id = (timerRef ?? "").Trim();
            string lower = id.ToLowerInvariant();

            if (string.IsNullOrEmpty(id)
                || lower == "lasttimer"
                || lower == "last"
                || id == "最后触发定时器"
                || id == "当前触发定时器"
                || id == "上次触发定时器"
                )
            {
                return _lastTriggeredTimer;
            }

            if (lower == "nexttimer"
                || lower == "next"
                || id == "最近定时器"
                || id == "下次定时器")
            {
                return GetNextDueTimer();
            }

            TryGetTimer(id, out TimerData timer);
            return timer;
        }

        /// <summary>
        /// 按当前缓存顺序获取指定序号的定时器。
        /// </summary>
        public static TimerData GetTimerByIndex(int index)
        {
            EnsureCacheLoaded();
            if (index < 0 || index >= _timerCache.Count) return null;
            return _timerCache[index];
        }

        /// <summary>
        /// 获取目标日期最近的未到期定时器。
        /// </summary>
        public static TimerData GetNextDueTimer()
        {
            EnsureCacheLoaded();
            TimerData result = null;
            foreach (TimerData timer in _timerCache)
            {
                if (timer == null) continue;
                if (result == null || timer.TargetAbsDay < result.TargetAbsDay)
                    result = timer;
            }
            return result;
        }

        /// <summary>
        /// 获取当前世界时间对应的绝对天数。
        /// </summary>
        public static int GetCurrentAbsDay()
        {
            WorldData worldData = CommonHandlers.GetWorldData();
            return ToAbsDay(worldData?.worldTime);
        }

        public static TimeData FromAbsDay(int absDay)
        {
            int year = absDay / 360 + 1;
            int dayOfYear = absDay % 360;
            if (dayOfYear < 0)
            {
                year--;
                dayOfYear += 360;
            }
            int month = dayOfYear / 30 + 1;
            int day = dayOfYear % 30 + 1;
            return new TimeData(year, month, day);
        }

        /// <summary>
        /// 将绝对天数格式化为游戏内年月日文本。
        /// </summary>
        public static string FormatAbsDay(int absDay)
        {
            TimeData time = FromAbsDay(absDay);
            return FormatTime(time);
        }

        /// <summary>
        /// 查询定时器字段值，供剧情指令和表达式读取定时器状态。
        /// </summary>
        public static string QueryTimer(string fieldName, string timerRef)
        {
            string field = (fieldName ?? "").Trim();
            TimerData timer = ResolveTimerRef(timerRef);

            if (field.Equals("Exists", StringComparison.OrdinalIgnoreCase))
                return timer != null ? "1" : "0";

            if (timer == null)
            {
                if (IsNumericTimerField(field)) return "-1";
                return "";
            }

            int currentAbsDay = GetCurrentAbsDay();
            int rawLeftDays = timer.TargetAbsDay - currentAbsDay;

            switch (field.ToLowerInvariant())
            {
                case "id":
                    return timer.Id ?? "";
                case "type":
                    return timer.Type.ToString();
                case "createdabsday":
                    return timer.CreatedAbsDay.ToString();
                case "createdtime":
                    return FormatAbsDay(timer.CreatedAbsDay);
                case "originaltimeparam":
                    return timer.OriginalTimeParam ?? "";
                case "targetabsday":
                    return timer.TargetAbsDay.ToString();
                case "targettime":
                    return FormatAbsDay(timer.TargetAbsDay);
                case "callback":
                    return timer.Callback ?? "";
                case "param":
                    return timer.Param ?? "";
                case "customparam":
                    return timer.CustomParam ?? "";
                case "forceupdateonset":
                    return timer.ForceUpdateOnSet ? "1" : "0";
                case "triggerimmediatelyonset":
                    return timer.TriggerImmediatelyOnSet ? "1" : "0";
                case "leftdays":
                    return Math.Max(0, rawLeftDays).ToString();
                case "rawleftdays":
                    return rawLeftDays.ToString();
                case "isdue":
                    return timer.TargetAbsDay <= currentAbsDay ? "1" : "0";
                case "ispast":
                    return timer.TargetAbsDay < currentAbsDay ? "1" : "0";
                case "istoday":
                    return timer.TargetAbsDay == currentAbsDay ? "1" : "0";
                default:
                    LoggerManager.Warning($"Timer查询: 未知字段 {fieldName}");
                    return "";
            }
        }

        /// <summary>
        /// 将当前定时器 watcher 配置同步为世界传闻列表中的运行时 EventData。
        /// </summary>
        public static void SyncTimerWatchers()
        {
            EnsureCacheLoaded();
            SyncTimerWatchersInternal(GetCurrentAbsDay());
        }

        /// <summary>
        /// 从 PlotEventLog 持久化字段中重新读取定时器缓存。
        /// </summary>
        private static void ReloadCacheFromStorage()
        {
            _timerCache = DecodeTimers(ReadStorageString(TimerStorageKey));
            _lastTriggeredTimer = DecodeLastTimer(ReadStorageString(LastTimerStorageKey));
        }

        /// <summary>
        /// 将当前定时器缓存序列化并写回 PlotEventLog。
        /// </summary>
        private static void SaveCacheToStorage()
        {
            PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
            if (logData == null)
            {
                LoggerManager.Warning("SetTimer: PlotEventLogData实例不存在，无法保存定时器");
                return;
            }

            string raw = EncodeTimers(_timerCache);
            logData.Set(TimerStorageKey, raw);
            RecalculateNextDueAbsDay();
        }

        /// <summary>
        /// 保存最近一次触发的定时器快照，便于剧情指令查询 last。
        /// </summary>
        private static void SaveLastTriggeredTimerToStorage()
        {
            PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
            if (logData == null) return;

            string raw = _lastTriggeredTimer != null ? EncodeLastTimer(_lastTriggeredTimer) : "";
            logData.Set(LastTimerStorageKey, raw);
        }

        /// <summary>
        /// 从 PlotEventLog 的系统字符串存储中读取指定 key 的值。
        /// </summary>
        private static string ReadStorageString(string key)
        {
            PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
            if (logData == null || !logData.HaveKey(key)) return "";
            return logData.Get(key) ?? "";
        }

        /// <summary>
        /// 将定时器列表包装版本信息后序列化为 JSON 字符串。
        /// </summary>
        private static string EncodeTimers(List<TimerData> timers)
        {
            TimerStorageData storage = new TimerStorageData();
            storage.Timers = SanitizeTimers(timers);
            return JsonConvert.SerializeObject(storage, Formatting.None);
        }

        /// <summary>
        /// 从 JSON 字符串反序列化定时器列表，并兼容旧格式的裸数组存储。
        /// </summary>
        private static List<TimerData> DecodeTimers(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new List<TimerData>();

            try
            {
                TimerStorageData storage = JsonConvert.DeserializeObject<TimerStorageData>(raw);
                return SanitizeTimers(storage?.Timers);
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"SetTimer: system_timers JSON解析失败: {ex.Message}");
                return new List<TimerData>();
            }
        }

        /// <summary>
        /// 将最近触发的定时器快照序列化为 JSON 字符串。
        /// </summary>
        private static string EncodeLastTimer(TimerData timer)
        {
            return JsonConvert.SerializeObject(timer, Formatting.None);
        }

        /// <summary>
        /// 从 JSON 字符串恢复最近触发的定时器快照。
        /// </summary>
        private static TimerData DecodeLastTimer(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            try
            {
                TimerData timer = JsonConvert.DeserializeObject<TimerData>(raw);
                return IsValidTimer(timer) ? timer : null;
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"SetTimer: system_last_timer JSON解析失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 清理反序列化后的定时器列表，过滤无效项、去重并按目标日期排序。
        /// </summary>
        private static List<TimerData> SanitizeTimers(List<TimerData> timers)
        {
            List<TimerData> result = new List<TimerData>();
            if (timers == null) return result;

            foreach (TimerData timer in timers)
            {
                if (!IsValidTimer(timer))
                {
                    LoggerManager.Warning("SetTimer: 跳过无效定时器记录");
                    continue;
                }

                timer.Id = timer.Id.Trim();
                timer.OriginalTimeParam = timer.OriginalTimeParam ?? "";
                timer.Callback = timer.Callback ?? "";
                timer.Param = timer.Param ?? "";
                timer.CustomParam = timer.CustomParam ?? "";
                timer.Watcher = SanitizeWatcher(timer.Watcher);
                result.Add(timer);
            }

            return result;
        }

        /// <summary>
        /// 判断定时器数据是否具备继续参与检查和触发的基本条件。
        /// </summary>
        private static bool IsValidTimer(TimerData timer)
        {
            return timer != null
                && !string.IsNullOrWhiteSpace(timer.Id)
                && (timer.Type == 0 || timer.Type == 1);
        }

        /// <summary>
        /// 格式化最近到期定时器信息，用于每日检查日志。
        /// </summary>
        private static string FormatNextDueForLog(int currentAbsDay)
        {
            if (_nextDueAbsDay == int.MaxValue) return "<none>";
            return $"{FormatAbsDay(_nextDueAbsDay)}({_nextDueAbsDay}, 剩余{_nextDueAbsDay - currentAbsDay}天)";
        }

        /// <summary>
        /// 格式化多个定时器的简要信息，用于日志展示。
        /// </summary>
        private static string DescribeTimerList(List<TimerData> timers, int currentAbsDay)
        {
            if (timers == null || timers.Count == 0) return "<empty>";

            const int maxCount = 8;
            string result = string.Join("; ", timers
                .Where(t => t != null)
                .Take(maxCount)
                .Select(t => DescribeTimer(t, currentAbsDay)));

            if (timers.Count > maxCount)
            {
                result += $"; ...(+{timers.Count - maxCount})";
            }

            return result;
        }

        /// <summary>
        /// 格式化单个定时器的关键字段，用于日志展示。
        /// </summary>
        private static string DescribeTimer(TimerData timer, int currentAbsDay)
        {
            if (timer == null) return "<null>";

            int leftDays = timer.TargetAbsDay - currentAbsDay;
            string typeName = timer.Type == 0 ? "Abs" : "Delay";
            return $"id={timer.Id}, type={typeName}, target={FormatAbsDay(timer.TargetAbsDay)}({timer.TargetAbsDay}), left={leftDays}, callback={SafeLogText(timer.Callback)}, param={SafeLogText(timer.Param)}, custom={SafeLogText(timer.CustomParam)}";
        }

        /// <summary>
        /// 将空字符串转换为日志中更易识别的占位文本。
        /// </summary>
        private static string SafeLogText(string value)
        {
            return string.IsNullOrEmpty(value) ? "<empty>" : value;
        }

        /// <summary>
        /// 根据 watcher 配置创建、更新或移除运行时传闻 EventData，并通知传闻 UI 刷新。
        /// </summary>
        private static void SyncTimerWatchersInternal(int currentAbsDay)
        {
            if (_syncingWatchers) return;

            _syncingWatchers = true;
            try
            {
                WorldData worldData = CommonHandlers.GetWorldData();
                if (worldData?.WorldEventDatas == null)
                {
                    _watcherEvents.Clear();
                    return;
                }

                HashSet<string> desiredIds = new HashSet<string>();
                foreach (TimerData timer in _timerCache)
                {
                    if (timer?.Watcher != null && timer.Watcher.Show)
                    {
                        desiredIds.Add(timer.Id);
                    }
                }

                bool changed = false;
                foreach (string id in _watcherEvents.Keys.ToList())
                {
                    EventData watcherEvent = _watcherEvents[id];
                    if (!desiredIds.Contains(id) || watcherEvent == null)
                    {
                        if (watcherEvent != null && worldData.WorldEventDatas.Contains(watcherEvent))
                        {
                            worldData.WorldEventDatas.Remove(watcherEvent);
                            changed = true;
                        }

                        _watcherEvents.Remove(id);
                    }
                }

                foreach (TimerData timer in _timerCache)
                {
                    if (timer?.Watcher == null || !timer.Watcher.Show) continue;

                    if (!_watcherEvents.TryGetValue(timer.Id, out EventData watcherEvent) || watcherEvent == null)
                    {
                        watcherEvent = CreateWatcherEvent(timer, currentAbsDay);
                        _watcherEvents[timer.Id] = watcherEvent;
                    }
                    else if (ShouldRecreateWatcherEvent(watcherEvent, timer, currentAbsDay))
                    {
                        if (worldData.WorldEventDatas.Contains(watcherEvent))
                        {
                            worldData.WorldEventDatas.Remove(watcherEvent);
                        }

                        watcherEvent = CreateWatcherEvent(timer, currentAbsDay);
                        _watcherEvents[timer.Id] = watcherEvent;
                        changed = true;
                    }

                    UpdateWatcherEventLeftTime(watcherEvent, timer, currentAbsDay);

                    if (!worldData.WorldEventDatas.Contains(watcherEvent))
                    {
                        worldData.WorldEventDatas.Add(watcherEvent);
                        changed = true;
                    }
                }

                if (changed)
                {
                    MarkWorldEventTableDirty();
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"SetTimerWatcher: 同步传闻显示项失败: {ex}");
            }
            finally
            {
                _syncingWatchers = false;
            }
        }

        /// <summary>
        /// 创建 TimerWatcher 使用的运行时传闻 EventData，并按当前定时器状态写入初始显示数据。
        /// </summary>
        private static EventData CreateWatcherEvent(TimerData timer, int currentAbsDay)
        {
            TimerWatcherData watcher = timer.Watcher;
            EventData eventData = new EventData();
            eventData.eventName = watcher.Title ?? "";
            eventData.eventDescribe = watcher.Describe ?? "";
            eventData.areaID = new Il2CppSystem.Collections.Generic.List<int>();
            eventData.areaMapTileID = new Il2CppSystem.Collections.Generic.List<int>();
            eventData.resourcePointID = -1;
            eventData.nearAreaID = -1;
            eventData.plotTargetEvent = false;
            eventData.missionTargetEvent = false;
            eventData.autoDestroy = false;
            eventData.leftTime = Math.Max(0, timer.TargetAbsDay - currentAbsDay);
            eventData.difficulty = watcher.RareLevel;
            eventData.eventOutTimeCallFuc = "";
            eventData.plotData = null;
            eventData.notImportant = true;
            eventData.isAreaEvent = false;
            return eventData;
        }

        /// <summary>
        /// 简化版重建判断：只检查 SetTimerWatcher 会修改、且原版传闻 UI 只在初始化时读取的静态显示字段。
        /// </summary>
        private static bool ShouldRecreateWatcherEvent(EventData eventData, TimerData timer, int currentAbsDay)
        {
            TimerWatcherData watcher = timer?.Watcher;
            if (eventData == null || watcher == null) return true;

            return eventData.eventName != (watcher.Title ?? "") ||
                   eventData.eventDescribe != (watcher.Describe ?? "") ||
                   Math.Abs(eventData.difficulty - watcher.RareLevel) > 0.0001f;
        }

        /// <summary>
        /// 同步 watcher 传闻的剩余天数；不重建 EventData，避免每日推进时刷新整条传闻。
        /// </summary>
        private static void UpdateWatcherEventLeftTime(EventData eventData, TimerData timer, int currentAbsDay)
        {
            if (eventData == null || timer == null) return;
            eventData.leftTime = Math.Max(0, timer.TargetAbsDay - currentAbsDay);
        }

        /// <summary>
        /// 从世界传闻列表中移除所有由 TimerWatcher 创建的运行时传闻项。
        /// </summary>
        private static void ClearTimerWatcherEvents()
        {
            try
            {
                WorldData worldData = CommonHandlers.GetWorldData();
                if (worldData?.WorldEventDatas != null)
                {
                    foreach (EventData watcherEvent in _watcherEvents.Values.ToList())
                    {
                        if (watcherEvent != null && worldData.WorldEventDatas.Contains(watcherEvent))
                        {
                            worldData.WorldEventDatas.Remove(watcherEvent);
                        }
                    }

                    MarkWorldEventTableDirty();
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"SetTimerWatcher: 清理传闻显示项失败: {ex}");
            }
            finally
            {
                _watcherEvents.Clear();
            }
        }

        /// <summary>
        /// 标记传闻 UI 需要重建列表，使 watcher 标题、内容、难度和剩余天数变化能及时显示。
        /// </summary>
        private static void MarkWorldEventTableDirty()
        {
            try
            {
                if (MissionUIController._instance != null)
                {
                    MissionUIController._instance.worldEventTableDirty = true;
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 校验并规范化 watcher 配置，显示状态下要求标题和内容非空。
        /// </summary>
        private static TimerWatcherData SanitizeWatcher(TimerWatcherData watcher)
        {
            if (watcher == null || !watcher.Show) return null;
            watcher.Title = (watcher.Title ?? "").Trim();
            watcher.Describe = (watcher.Describe ?? "").Trim();
            if (string.IsNullOrEmpty(watcher.Title) || string.IsNullOrEmpty(watcher.Describe))
            {
                LoggerManager.Warning("SetTimerWatcher: 跳过无效 watcher 配置，显示时标题和内容不能为空");
                return null;
            }

            return watcher;
        }

        /// <summary>
        /// 克隆 watcher 配置，用于更新定时器时保留原有传闻监听设置。
        /// </summary>
        private static TimerWatcherData CloneWatcher(TimerWatcherData watcher)
        {
            watcher = SanitizeWatcher(watcher);
            if (watcher == null) return null;

            return new TimerWatcherData
            {
                Show = watcher.Show,
                Title = watcher.Title,
                Describe = watcher.Describe,
                RareLevel = watcher.RareLevel
            };
        }

        /// <summary>
        /// 解析并执行到期定时器的回调剧情函数。
        /// </summary>
        private static void ExecuteTimer(TimerData timer)
        {
            if (timer == null) return;

            try
            {
                int currentAbsDay = GetCurrentAbsDay();
                LoggerManager.Debug($"SetTimer: 触发流程开始，{DescribeTimer(timer, currentAbsDay)}");

                PlotController pc = PlotController._instance;
                if (pc == null)
                {
                    LoggerManager.Warning($"SetTimer: PlotController实例为空，无法触发定时器 {timer.Id}");
                    return;
                }

                _lastTriggeredTimer = timer;
                SaveLastTriggeredTimerToStorage();

                string callback = ResolveTriggerText(pc, timer.Callback);
                string param = ResolveTriggerText(pc, timer.Param);

                LoggerManager.Debug($"SetTimer: 定时器 {timer.Id} 解析回调，rawCallback={SafeLogText(timer.Callback)}, rawParam={SafeLogText(timer.Param)}, resolvedCallback={SafeLogText(callback)}, resolvedParam={SafeLogText(param)}");

                callback = PlotCommandHandler.StripParens(PlotCommandHandler.StripBraces(callback ?? ""));
                param = PlotCommandHandler.StripParens(PlotCommandHandler.StripBraces(param ?? ""));

                LoggerManager.Debug($"SetTimer: 定时器 {timer.Id} 清理括号后，callback={SafeLogText(callback)}, param={SafeLogText(param)}");

                if (string.IsNullOrWhiteSpace(callback))
                {
                    LoggerManager.Debug($"SetTimer: 定时器 {timer.Id} 为纯计时器，到期后不执行回调");
                    return;
                }

                if (string.IsNullOrEmpty(param))
                {
                    pc.gameObject.SendMessage(callback);
                    LoggerManager.Debug($"SetTimer: 定时器 {timer.Id} 触发 SendMessage(\"{callback}\")");
                }
                else
                {
                    pc.gameObject.SendMessage(callback, param);
                    LoggerManager.Debug($"SetTimer: 定时器 {timer.Id} 触发 SendMessage(\"{callback}\", \"{param}\")");
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"SetTimer: 定时器 {timer.Id} 触发异常: {ex}");
            }
        }

        /// <summary>
        /// 在定时器触发时解析回调函数名和参数中的剧情表达式。
        /// </summary>
        private static string ResolveTriggerText(PlotController pc, string text)
        {
            string result = text ?? "";
            for (int i = 0; i < 4; i++)
            {
                if (!ConditionQueryHandlers.ContainsParseableSyntax(result))
                    break;

                string resolved = ConditionQueryHandlers.ResolveAllCommands(pc, result);
                if (resolved == result)
                    break;

                result = resolved ?? "";
            }
            return result;
        }

        /// <summary>
        /// 根据定时器类型和时间参数计算目标绝对天数。
        /// </summary>
        private static bool TryBuildTargetAbsDay(int type, string timeParam, out int targetAbsDay)
        {
            targetAbsDay = 0;
            string raw = (timeParam ?? "").Trim();

            if (type == 1)
            {
                if (!int.TryParse(raw, out int days) || days < 0)
                    return false;

                targetAbsDay = GetCurrentAbsDay() + days;
                return true;
            }

            if (type == 0)
            {
                string[] parts = raw.Split('-');
                if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
                    return false;

                if (!int.TryParse(parts[0], out int year) || year < 0)
                    return false;

                int month = 1;
                int day = 1;

                if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) && !int.TryParse(parts[1], out month))
                    return false;
                if (parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]) && !int.TryParse(parts[2], out day))
                    return false;

                if (month < 1 || month > 12 || day < 1 || day > 30)
                    return false;

                targetAbsDay = ToAbsDay(new TimeData(year, month, day));
                return true;
            }

            return false;
        }

        /// <summary>
        /// 判断 SetTimer 参数是否表示兼容旧方案的删除请求。
        /// </summary>
        private static bool IsRemoveRequest(int type, string timeParam)
        {
            string raw = (timeParam ?? "").Trim();
            if (type == 1)
            {
                return int.TryParse(raw, out int days) && days < 0;
            }

            if (type == 0)
            {
                string[] parts = raw.Split('-');
                return parts.Length > 0 && int.TryParse(parts[0], out int year) && year < 0;
            }

            return false;
        }

        /// <summary>
        /// 将游戏 TimeData 转换为绝对天数。
        /// </summary>
        private static int ToAbsDay(TimeData time)
        {
            if (time == null) return 0;
            return (time.year - 1) * 360 + (time.month - 1) * 30 + (time.day - 1);
        }

        /// <summary>
        /// 将游戏 TimeData 格式化为年月日文本。
        /// </summary>
        private static string FormatTime(TimeData time)
        {
            if (time == null) return "";
            return $"{time.year}年{time.month}月{time.day}日";
        }

        /// <summary>
        /// 重新计算当前缓存中最近的定时器到期日。
        /// </summary>
        private static void RecalculateNextDueAbsDay()
        {
            _nextDueAbsDay = int.MaxValue;
            foreach (TimerData timer in _timerCache)
            {
                if (timer == null) continue;
                if (timer.TargetAbsDay < _nextDueAbsDay)
                    _nextDueAbsDay = timer.TargetAbsDay;
            }
        }

        /// <summary>
        /// 解析布尔风格参数，解析失败时返回指定默认值。
        /// </summary>
        private static bool ParseBoolLike(string raw, bool defaultValue)
        {
            string value = (raw ?? "").Trim();
            if (value.Length == 0) return defaultValue;
            if (value == "1") return true;
            if (value == "0") return false;
            if (value.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (value.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            return defaultValue;
        }

        /// <summary>
        /// 解析 true/false/1/0/yes/no/on/off 等布尔风格参数。
        /// </summary>
        private static bool TryParseBoolLike(string raw, out bool value)
        {
            string text = (raw ?? "").Trim();
            if (text == "1")
            {
                value = true;
                return true;
            }

            if (text == "0")
            {
                value = false;
                return true;
            }

            if (text.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }

            if (text.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }

            value = false;
            return false;
        }

        /// <summary>
        /// 判断查询字段是否应以数值方式返回。
        /// </summary>
        private static bool IsNumericTimerField(string field)
        {
            string lower = (field ?? "").Trim().ToLowerInvariant();
            return lower == "type"
                || lower == "createdabsday"
                || lower == "targetabsday"
                || lower == "leftdays"
                || lower == "rawleftdays";
        }
    }
}
