using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 混合路径：WorldEventController 补丁管理器
    /// 
    /// 策略：
    /// - 补丁加载：复用龙之书的 TargetDefinitionsByFileName + ModProjectRegistry 文件发现机制
    /// - 补丁应用：独立实现，不修改 ApplyLoadedPatchFiles，在 GameController.Start 的独立 Postfix 中执行
    /// - 数据导出：独立实现导出逻辑
    /// - 去重：自行维护签名机制
    /// 
    /// 注意：龙之书的所有 ComplexXxx 类型都是 internal，无法在编译期直接引用。
    /// 因此本文件全部通过反射访问龙之书的 internal 类型和成员。
    /// </summary>
    public static class WorldEventPatchManager
    {
        // WorldEventController 对应的 ControllerKind 枚举值
        // 龙之书 ComplexControllerKind: MissionData=0, WorldPlotEvent=1, WorldEvent=2(自定义)
        public const int WorldEventKindValue = 2;

        // ComplexPatchTargetKind: ArrayByName=0, ObjectReplace=1
        private const int PatchTargetKindArrayByName = 0;

        // 已加载的 WorldEvent 补丁文件列表（存储为 object，因为 ComplexJsonPatchFile 是 internal）
        private static readonly List<object> _loadedPatchFiles = new List<object>();

        // 去重签名
        private static string _lastAppliedSignature = string.Empty;

        // 是否已初始化
        private static bool _isInitialized = false;

        // 导出相关
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        // 反射缓存：类型
        private static Type _patchManagerType;
        private static Type _dumpManagerType;
        private static Type _patchFileType;
        private static Type _targetDefType;
        private static Type _executorType;
        private static Type _accessorType;
        private static Type _applyResultType;
        private static Type _controllerKindType;
        private static Type _targetKindType;

        // 反射缓存：字段
        private static FieldInfo _targetDefsField;
        private static FieldInfo _loadedPatchFilesField;
        private static FieldInfo _latestRootField;

        // 反射缓存：方法
        private static MethodInfo _applyPatchMethod;
        private static MethodInfo _tryGetMemberValueMethod;
        private static MethodInfo _getObjectIdentityMethod;
        private static MethodInfo _isDumpCompletedMethod;
        private static FieldInfo _exportCycleIdField;

        // 协程运行标记，防止重入
        private static bool _coroutineRunning = false;

        /// <summary>
        /// 初始化：缓存反射元数据
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                CacheReflectionMetadata();
                _isInitialized = true;

                LoggerManager.Info($"WorldEventPatchManager: 初始化完成，已加载 {_loadedPatchFiles.Count} 个 WorldEvent 补丁文件");
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"WorldEventPatchManager: 初始化失败 - {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 缓存反射所需的类型、字段、方法
        /// </summary>
        private static void CacheReflectionMetadata()
        {
            // 缓存类型
            _patchManagerType = AccessTools.TypeByName("TheBookOfLong.GameComplexDataPatchManager");
            _dumpManagerType = AccessTools.TypeByName("TheBookOfLong.GameComplexDataDumpManager");
            _patchFileType = AccessTools.TypeByName("TheBookOfLong.ComplexJsonPatchFile");
            _targetDefType = AccessTools.TypeByName("TheBookOfLong.ComplexPatchTargetDefinition");
            _executorType = AccessTools.TypeByName("TheBookOfLong.ComplexPatchExecutor");
            _accessorType = AccessTools.TypeByName("TheBookOfLong.ComplexTypeAccessor");
            _applyResultType = AccessTools.TypeByName("TheBookOfLong.ComplexPatchApplyResult");
            _controllerKindType = AccessTools.TypeByName("TheBookOfLong.ComplexControllerKind");
            _targetKindType = AccessTools.TypeByName("TheBookOfLong.ComplexPatchTargetKind");

            // 缓存字段
            _targetDefsField = AccessTools.Field(_patchManagerType, "TargetDefinitionsByFileName");
            _loadedPatchFilesField = AccessTools.Field(_patchManagerType, "LoadedPatchFiles");
            _latestRootField = AccessTools.Field(_dumpManagerType, "_latestRoot");

            // 缓存方法
            _applyPatchMethod = AccessTools.Method(_executorType, "ApplyPatch");
            _tryGetMemberValueMethod = AccessTools.Method(_accessorType, "TryGetMemberValue");
            _getObjectIdentityMethod = AccessTools.Method(_accessorType, "GetObjectIdentity");
            _isDumpCompletedMethod = AccessTools.Method(_dumpManagerType, "IsExportCompleted");
            _exportCycleIdField = AccessTools.Field(_dumpManagerType, "_exportCycleId");
        }

        #region 协程：先导出原始数据再应用补丁

        /// <summary>
        /// 协程：等待龙之书的 dump 完成后，先导出原始 WorldEvent 数据再应用补丁。
        /// 
        /// 龙之书的策略是先 dump 再 patch，确保导出的是原始数据。
        /// 本模组需遵循相同策略：通过反射轮询龙之书的 IsExportCompleted，
        /// 等 dump 完成后先 ExportData()（原始数据），再 ApplyPatches()（打补丁）。
        /// 
        /// 如果龙之书未安装或 dump 失败，超时后回退到直接 export + patch。
        /// </summary>
        public static IEnumerator ExportThenPatchCoroutine()
        {
            if (_coroutineRunning)
            {
                LoggerManager.Debug("WorldEventPatchManager: 协程已在运行，跳过重复启动");
                yield break;
            }
            _coroutineRunning = true;

            try
            {
                // 1. 尝试等待龙之书的 dump 完成
                bool dumpCompleted = false;
                int dumpCycleId = 0;

                if (_isDumpCompletedMethod != null && _exportCycleIdField != null)
                {
                    dumpCycleId = GetCurrentDumpCycleId();
                    if (dumpCycleId > 0)
                    {
                        int maxWait = 300; // 最多等 300 帧（约5秒@60fps）
                        while (maxWait-- > 0)
                        {
                            if (IsDumpCompleted(dumpCycleId))
                            {
                                dumpCompleted = true;
                                break;
                            }
                            yield return null;
                        }

                        if (!dumpCompleted)
                        {
                            LoggerManager.Warning("WorldEventPatchManager: 等待龙之书 dump 超时，回退到直接导出");
                        }
                    }
                }

                if (!dumpCompleted)
                {
                    LoggerManager.Debug("WorldEventPatchManager: 未检测到龙之书 dump 周期，直接执行导出与补丁");
                }

                // 2. dump 完成后（或超时回退），先导出原始数据
                ExportData();

                // 3. 再应用补丁
                ApplyPatches();
            }
            finally
            {
                _coroutineRunning = false;
            }
        }

        /// <summary>
        /// 反射获取龙之书当前的 dumpCycleId
        /// </summary>
        private static int GetCurrentDumpCycleId()
        {
            try
            {
                if (_exportCycleIdField == null) return 0;
                return (int)_exportCycleIdField.GetValue(null);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 反射检查龙之书的 dump 是否已完成
        /// </summary>
        private static bool IsDumpCompleted(int dumpCycleId)
        {
            try
            {
                if (_isDumpCompletedMethod == null) return false;
                return (bool)_isDumpCompletedMethod.Invoke(null, new object[] { dumpCycleId });
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region 补丁提取

        /// <summary>
        /// 从龙之书的 LoadedPatchFiles 中提取 ControllerKind == 2 的补丁
        /// 并从原列表中移除，防止龙之书的 ApplyLoadedPatchFiles 错误路由到 MissionDataController
        /// 
        /// 由 LoadPatchFilesPostfixPatch 在龙之书 LoadPatchFiles 执行后调用
        /// </summary>
        public static void ExtractAndRemoveWorldEventPatches()
        {
            try
            {
                // 确保反射元数据已缓存
                if (_loadedPatchFilesField == null)
                    CacheReflectionMetadata();

                var loadedFiles = _loadedPatchFilesField.GetValue(null) as IList;
                if (loadedFiles == null) return;

                // 收集需要移除的补丁索引
                var toRemove = new List<int>();

                for (int i = 0; i < loadedFiles.Count; i++)
                {
                    var patchFile = loadedFiles[i];
                    if (patchFile == null) continue;

                    // 获取 patchFile.Target
                    var target = AccessTools.Property(_patchFileType, "Target").GetValue(patchFile);
                    if (target == null) continue;

                    // 获取 target.ControllerKind
                    var controllerKind = AccessTools.Property(_targetDefType, "ControllerKind").GetValue(target);

                    // 判断 ControllerKind 是否等于 WorldEventKind (2)
                    if (IsControllerKindEqual(controllerKind, WorldEventKindValue))
                    {
                        _loadedPatchFiles.Add(patchFile);
                        toRemove.Add(i);

                        string modName = (string)AccessTools.Property(_patchFileType, "ModName").GetValue(patchFile);
                        string relativePath = (string)AccessTools.Property(_patchFileType, "RelativePath").GetValue(patchFile);
                        LoggerManager.Info($"  WorldEvent 补丁: [{modName}] {relativePath}");
                    }
                }

                // 从龙之书的 LoadedPatchFiles 中移除 WorldEvent 补丁（倒序移除以避免索引偏移）
                for (int i = toRemove.Count - 1; i >= 0; i--)
                {
                    loadedFiles.RemoveAt(toRemove[i]);
                }

                if (toRemove.Count > 0)
                {
                    LoggerManager.Info($"  已从龙之书 LoadedPatchFiles 中提取并移除 {toRemove.Count} 个 WorldEvent 补丁");
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"WorldEventPatchManager: 提取 WorldEvent 补丁失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 判断两个 ComplexControllerKind 枚举值是否相等
        /// </summary>
        private static bool IsControllerKindEqual(object kindValue, int targetValue)
        {
            if (kindValue == null) return false;
            // 枚举底层是 int
            return Convert.ToInt32(kindValue) == targetValue;
        }

        #endregion

        #region TargetDefinition 注册

        /// <summary>
        /// 注册 TargetDefinition 到龙之书，使其能识别 WorldEventController_*.json 文件
        /// 由 PatchManagerStaticCtorPatch 调用
        /// </summary>
        public static void RegisterTargetDefinition()
        {
            try
            {
                if (_targetDefsField == null)
                    CacheReflectionMetadata();

                var dict = _targetDefsField.GetValue(null) as IDictionary;
                if (dict == null)
                {
                    LoggerManager.Warning("WorldEventPatchManager: TargetDefinitionsByFileName 为 null，无法注册");
                    return;
                }

                string key = "WorldEventController_worldEventDataBase.json";
                if (dict.Contains(key))
                    return;

                // 构造 ComplexControllerKind 枚举值 2
                object controllerKind = Enum.ToObject(_controllerKindType, WorldEventKindValue);
                // 构造 ComplexPatchTargetKind.ArrayByName (0)
                object patchTargetKind = Enum.ToObject(_targetKindType, PatchTargetKindArrayByName);

                // 创建 ComplexPatchTargetDefinition 实例（构造函数是 internal）
                var targetDef = Activator.CreateInstance(
                    _targetDefType,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null,
                    new object[] { controllerKind, "worldEventDataBase", patchTargetKind },
                    null);

                dict[key] = targetDef;

                LoggerManager.Info($"WorldEventPatchManager: 已注册 TargetDefinition '{key}'");
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"WorldEventPatchManager: 注册 TargetDefinition 失败 - {ex.Message}");
            }
        }

        #endregion

        #region 补丁应用

        /// <summary>
        /// 应用所有已加载的 WorldEvent 补丁
        /// 在 GameController.Start 的 Postfix 中调用
        /// </summary>
        public static void ApplyPatches()
        {
            try
            {
                // 1. 检查 WorldEventController 是否就绪
                var wec = WorldEventController.Instance;
                if (wec == null)
                {
                    LoggerManager.Debug("WorldEventPatchManager: WorldEventController.Instance 为 null，跳过补丁应用");
                    return;
                }

                // 2. 检查数据是否就绪
                object memberValue;
                if (!TryGetMemberValue(wec, "worldEventDataBase", out memberValue) || memberValue == null)
                {
                    LoggerManager.Debug("WorldEventPatchManager: worldEventDataBase 未初始化，跳过补丁应用");
                    return;
                }

                // 3. 初始化（如果尚未初始化）
                if (!_isInitialized) Initialize();

                // 4. 去重检查
                string currentSignature = BuildTargetSignature(wec);
                if (string.Equals(currentSignature, _lastAppliedSignature, StringComparison.Ordinal))
                {
                    LoggerManager.Debug("WorldEventPatchManager: 目标签名未变，跳过重复应用");
                    return;
                }

                // 5. 无补丁则跳过
                if (_loadedPatchFiles.Count == 0)
                {
                    LoggerManager.Debug("WorldEventPatchManager: 无 WorldEvent 补丁需应用");
                    return;
                }

                // 6. 应用补丁
                int totalAdded = 0, totalModified = 0;
                foreach (var patchFile in _loadedPatchFiles)
                {
                    try
                    {
                        // ComplexPatchExecutor.ApplyPatch(wec, patchFile)
                        var result = _applyPatchMethod.Invoke(null, new object[] { wec, patchFile });
                        if (result == null) continue;

                        // result.PatchTargetKind
                        var kindValue = AccessTools.Property(_applyResultType, "PatchTargetKind").GetValue(result);
                        int kindInt = Convert.ToInt32(kindValue);

                        string modName = (string)AccessTools.Property(_applyResultType, "ModName").GetValue(result);
                        string relativePath = (string)AccessTools.Property(_applyResultType, "RelativePath").GetValue(result);

                        if (kindInt == PatchTargetKindArrayByName)
                        {
                            int added = (int)AccessTools.Property(_applyResultType, "AddedCount").GetValue(result);
                            int modified = (int)AccessTools.Property(_applyResultType, "ModifiedCount").GetValue(result);
                            totalAdded += added;
                            totalModified += modified;
                            LoggerManager.Info($"  WorldEvent 补丁 [{modName}] {relativePath}: added {added}, modified {modified}");
                        }
                        else
                        {
                            int replaced = (int)AccessTools.Property(_applyResultType, "ReplacedCount").GetValue(result);
                            LoggerManager.Info($"  WorldEvent 补丁 [{modName}] {relativePath}: replaced {replaced}");
                        }
                    }
                    catch (Exception ex)
                    {
                        string modName = "?";
                        string relativePath = "?";
                        try
                        {
                            modName = (string)AccessTools.Property(_patchFileType, "ModName").GetValue(patchFile);
                            relativePath = (string)AccessTools.Property(_patchFileType, "RelativePath").GetValue(patchFile);
                        }
                        catch { }
                        // 展开 TargetInvocationException 以显示真实错误
                        var innerEx = ex is System.Reflection.TargetInvocationException tie ? tie.InnerException : ex;
                        LoggerManager.Error($"  WorldEvent 补丁应用失败 [{modName}] {relativePath}: {innerEx?.Message ?? ex.Message}\n{innerEx?.StackTrace ?? ex.StackTrace}");
                    }
                }

                // 7. 更新签名
                _lastAppliedSignature = currentSignature;

                LoggerManager.Info($"WorldEventPatchManager: 补丁应用完成，共 added={totalAdded}, modified={totalModified}");
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"WorldEventPatchManager: 应用补丁失败 - {ex.Message}\n{ex.StackTrace}");
            }
        }

        #endregion

        #region 数据导出

        /// <summary>
        /// 导出 WorldEventController 数据到 JSON
        /// </summary>
        public static void ExportData()
        {
            try
            {
                var wec = WorldEventController.Instance;
                if (wec == null)
                {
                    LoggerManager.Debug("WorldEventPatchManager: WorldEventController.Instance 为 null，跳过导出");
                    return;
                }

                object memberValue;
                if (!TryGetMemberValue(wec, "worldEventDataBase", out memberValue) || memberValue == null)
                {
                    LoggerManager.Debug("WorldEventPatchManager: worldEventDataBase 未初始化，跳过导出");
                    return;
                }

                // 获取龙之书的导出目录
                string latestRoot = GetLatestRoot();
                if (string.IsNullOrEmpty(latestRoot)) return;

                string complexDataRoot = Path.Combine(latestRoot, "ComplexData");
                if (!Directory.Exists(complexDataRoot))
                {
                    Directory.CreateDirectory(complexDataRoot);
                }

                string fileName = "WorldEventController_worldEventDataBase.json";
                string filePath = Path.Combine(complexDataRoot, fileName);

                object serializable = ToSerializableValue(memberValue, 0, new HashSet<object>(ReferenceComparer.Instance));
                string json = JsonConvert.SerializeObject(serializable, Formatting.Indented);
                File.WriteAllText(filePath, json, Utf8NoBom);

                LoggerManager.Info($"WorldEventPatchManager: 已导出 WorldEventController 数据到 {filePath}");
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"WorldEventPatchManager: 导出数据失败 - {ex.Message}");
            }
        }

        #endregion

        #region 反射辅助方法

        /// <summary>
        /// 反射调用 ComplexTypeAccessor.TryGetMemberValue
        /// </summary>
        private static bool TryGetMemberValue(object target, string memberName, out object value)
        {
            value = null;
            try
            {
                if (_tryGetMemberValueMethod == null)
                    CacheReflectionMetadata();

                // TryGetMemberValue 的 out 参数需要特殊处理
                var parameters = new object[] { target, memberName, null };
                var result = (bool)_tryGetMemberValueMethod.Invoke(null, parameters);
                if (result)
                {
                    value = parameters[2];
                }
                return result;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 反射调用 ComplexTypeAccessor.GetObjectIdentity
        /// </summary>
        private static long GetObjectIdentity(object value)
        {
            try
            {
                if (_getObjectIdentityMethod == null)
                    CacheReflectionMetadata();

                return (long)_getObjectIdentityMethod.Invoke(null, new object[] { value });
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 构建目标签名用于去重
        /// </summary>
        private static string BuildTargetSignature(WorldEventController wec)
        {
            long wecId = GetObjectIdentity(wec);
            object memberValue;
            long memberId = 0;
            if (TryGetMemberValue(wec, "worldEventDataBase", out memberValue) && memberValue != null)
            {
                memberId = GetObjectIdentity(memberValue);
            }
            return $"WorldEventController={wecId};worldEventDataBase={memberId};";
        }

        /// <summary>
        /// 获取龙之书的导出根目录
        /// </summary>
        private static string GetLatestRoot()
        {
            try
            {
                if (_latestRootField == null)
                    CacheReflectionMetadata();

                string latestRoot = (string)_latestRootField.GetValue(null);
                if (string.IsNullOrEmpty(latestRoot))
                {
                    LoggerManager.Debug("WorldEventPatchManager: 龙之书导出目录尚未初始化，跳过导出");
                    return null;
                }
                return latestRoot;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region 导出序列化

        private static object ToSerializableValue(object value, int depth, HashSet<object> visited)
        {
            if (value == null) return null;
            if (depth > 32) return "<MaxDepthReached>";

            if (value is string str) return str;

            if (value is UnityEngine.Object unityObj)
            {
                var dict = new Dictionary<string, object>(StringComparer.Ordinal);
                dict["name"] = unityObj.name;
                dict["type"] = value.GetType().FullName;
                return dict;
            }

            Type type = value.GetType();

            object simpleValue;
            if (TryConvertSimpleValue(value, type, out simpleValue))
                return simpleValue;

            bool isReferenceType = !type.IsValueType;
            if (isReferenceType && !visited.Add(value))
                return "<CycleDetected>";

            try
            {
                List<object> listValue;
                if (TryConvertEnumerable(value, depth, visited, out listValue))
                    return listValue;

                var objDict = new Dictionary<string, object>(StringComparer.Ordinal);
                bool hasMembers = false;
                foreach (var member in GetSerializableMembers(type))
                {
                    hasMembers = true;
                    object memberValue;
                    try { memberValue = member.Getter(value); }
                    catch (Exception ex) { memberValue = "<ReadFailed: " + ex.GetType().Name + ">"; }
                    objDict[member.Name] = ToSerializableValue(memberValue, depth + 1, visited);
                }
                return hasMembers ? (object)objDict : value.ToString();
            }
            finally
            {
                if (isReferenceType) visited.Remove(value);
            }
        }

        private static bool TryConvertSimpleValue(object value, Type type, out object simpleValue)
        {
            if (type.IsEnum)
            {
                Type underlying = Enum.GetUnderlyingType(type);
                object numVal = (underlying == typeof(byte) || underlying == typeof(ushort) ||
                                 underlying == typeof(uint) || underlying == typeof(ulong))
                    ? (object)Convert.ToUInt64(value) : Convert.ToInt64(value);
                var dict = new Dictionary<string, object>(StringComparer.Ordinal);
                dict["name"] = value.ToString();
                dict["value"] = numVal;
                simpleValue = dict;
                return true;
            }

            if (type == typeof(bool) || type == typeof(byte) || type == typeof(sbyte) ||
                type == typeof(short) || type == typeof(ushort) || type == typeof(int) ||
                type == typeof(uint) || type == typeof(long) || type == typeof(ulong) ||
                type == typeof(float) || type == typeof(double) || type == typeof(decimal) ||
                type == typeof(char))
            {
                simpleValue = value;
                return true;
            }

            if (type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
                type == typeof(TimeSpan) || type == typeof(Guid))
            {
                simpleValue = value.ToString();
                return true;
            }

            if (type == typeof(IntPtr) || type == typeof(UIntPtr))
            {
                simpleValue = value.ToString();
                return true;
            }

            string fullName = type.FullName;
            if (fullName == "Il2CppSystem.String" || fullName == "Il2CppSystem.Char")
            {
                simpleValue = value.ToString();
                return true;
            }

            simpleValue = null;
            return false;
        }

        private static bool TryConvertEnumerable(object value, int depth, HashSet<object> visited, out List<object> listValue)
        {
            var enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                listValue = new List<object>();
                foreach (object item in enumerable)
                    listValue.Add(ToSerializableValue(item, depth + 1, visited));
                return true;
            }

            var countProp = value.GetType().GetProperty("Count",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var itemProp = value.GetType().GetProperty("Item",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, null, new Type[] { typeof(int) }, null);

            if (countProp == null || itemProp == null || !countProp.CanRead || !itemProp.CanRead)
            {
                listValue = null;
                return false;
            }

            object countVal = countProp.GetValue(value);
            int count = (countVal == null) ? 0 : Convert.ToInt32(countVal);
            listValue = new List<object>(count);
            for (int i = 0; i < count; i++)
            {
                object item = itemProp.GetValue(value, new object[] { i });
                listValue.Add(ToSerializableValue(item, depth + 1, visited));
            }
            return true;
        }

        private static IEnumerable<SerializableMember> GetSerializableMembers(Type type)
        {
            Type current = type;
            while (current != null && current != typeof(object))
            {
                string ns = current.Namespace;
                if (!string.IsNullOrEmpty(ns) && ns.StartsWith("Il2CppInterop.Runtime", StringComparison.Ordinal))
                    break;

                foreach (var prop in current.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public))
                {
                    if (prop.CanRead && prop.CanWrite && prop.GetIndexParameters().Length == 0)
                        yield return new SerializableMember(prop.Name, p => prop.GetValue(p));
                }
                foreach (var field in current.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public))
                {
                    if (!field.IsInitOnly)
                        yield return new SerializableMember(field.Name, f => field.GetValue(f));
                }
                current = current.BaseType;
            }
        }

        private sealed class SerializableMember
        {
            public string Name { get; }
            public Func<object, object> Getter { get; }
            public SerializableMember(string name, Func<object, object> getter)
            {
                Name = name;
                Getter = getter;
            }
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object x, object y) => x == y;
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }

        #endregion
    }
}
