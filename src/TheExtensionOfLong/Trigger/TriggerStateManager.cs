using System;
using System.Collections.Generic;
using Il2Cpp;
using Newtonsoft.Json;

namespace TheExtensionOfLong
{
    public static class TriggerStateManager
    {
        private const string StorageKey = "system_triggers";
        private const int StorageVersion = 1;

        public static void ApplyPersistedStates()
        {
            TriggerStorageData storage = ReadStorage();
            if (storage?.States == null || storage.States.Count == 0)
                return;

            int applied = 0;
            for (int i = 0; i < storage.States.Count; i++)
            {
                TriggerState state = storage.States[i];
                if (state == null || string.IsNullOrWhiteSpace(state.Id))
                    continue;

                if (TriggerRegistry.ApplyEnabledState(state.Id, state.Enabled, rebuildIndexes: false))
                    applied++;
            }

            if (applied > 0)
                TriggerRegistry.RebuildIndexes();

            if (applied > 0)
                LoggerManager.Debug("TriggerStateManager: 已应用存档触发器启用状态 " + applied + " 条");
        }

        public static bool SetEnabled(string id, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                LoggerManager.Warning("SetTriggerEnabled: 触发器编号不能为空");
                return false;
            }

            id = id.Trim();
            TriggerRule rule;
            bool ruleExists = TriggerRegistry.TryGetRule(id, out rule);
            bool defaultEnabled = ruleExists ? rule.DefaultEnabled : enabled;

            TriggerStorageData storage = ReadStorageOrCreate();
            if (storage.States == null)
                storage.States = new List<TriggerState>();

            TriggerState state = FindState(storage.States, id);
            bool needsOverride = !ruleExists || enabled != defaultEnabled;

            if (needsOverride)
            {
                if (state == null)
                {
                    state = new TriggerState();
                    state.Id = id;
                    storage.States.Add(state);
                }

                state.Enabled = enabled;
            }
            else if (state != null)
            {
                storage.States.Remove(state);
            }

            SaveStorage(storage);

            if (ruleExists)
            {
                TriggerRegistry.ApplyEnabledState(id, enabled);
                LoggerManager.Debug("SetTriggerEnabled: 已" + (enabled ? "启用" : "禁用") + "触发器 " + id);
            }
            else
            {
                LoggerManager.Warning("SetTriggerEnabled: 当前未找到触发器 " + id + "，已仅写入存档状态");
            }

            return true;
        }

        private static TriggerState FindState(List<TriggerState> states, string id)
        {
            if (states == null || string.IsNullOrWhiteSpace(id))
                return null;

            for (int i = 0; i < states.Count; i++)
            {
                TriggerState state = states[i];
                if (state != null && string.Equals(state.Id, id, StringComparison.OrdinalIgnoreCase))
                    return state;
            }

            return null;
        }

        private static TriggerStorageData ReadStorage()
        {
            PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
            if (logData == null || !logData.HaveKey(StorageKey))
                return null;

            string raw = logData.Get(StorageKey);
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            try
            {
                TriggerStorageData storage = JsonConvert.DeserializeObject<TriggerStorageData>(raw);
                if (storage == null)
                    return null;

                if (storage.States == null)
                    storage.States = new List<TriggerState>();

                return storage;
            }
            catch (Exception ex)
            {
                LoggerManager.Error("TriggerStateManager: system_triggers JSON解析失败: " + ex.Message);
                return null;
            }
        }

        private static TriggerStorageData ReadStorageOrCreate()
        {
            TriggerStorageData storage = ReadStorage();
            if (storage != null)
                return storage;

            storage = new TriggerStorageData();
            storage.Version = StorageVersion;
            storage.States = new List<TriggerState>();
            return storage;
        }

        private static void SaveStorage(TriggerStorageData storage)
        {
            PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
            if (logData == null)
            {
                LoggerManager.Warning("TriggerStateManager: PlotEventLogData实例不存在，无法保存触发器状态");
                return;
            }

            if (storage == null)
                storage = new TriggerStorageData();

            storage.Version = StorageVersion;
            if (storage.States == null)
                storage.States = new List<TriggerState>();

            string raw = JsonConvert.SerializeObject(storage, Formatting.None);
            logData.Set(StorageKey, raw);
        }
    }
}
