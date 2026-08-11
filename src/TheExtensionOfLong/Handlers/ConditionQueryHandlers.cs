using Il2Cpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 标记一个条件查询指令处理器。
    /// Name 对应 [$查询名:参数$] 中的查询名。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class ConditionQueryAttribute : Attribute
    {
        public ConditionQueryAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }

        /// <summary>
        /// 是否允许在单次查询解析过程中缓存相同查询文本的结果。
        /// 默认为 true；随机、生成、存在副作用或依赖同轮副作用的查询应显式设为 false。
        /// </summary>
        public bool Cacheable { get; set; } = true;
    }

    /// <summary>
    /// 条件查询指令分发器。
    /// 负责自动发现 Handlers/ConditionQuery 下带 ConditionQueryAttribute 的处理器类。
    /// </summary>
    public static class ConditionQueryDispatcher
    {
        private delegate string ConditionQueryHandler(PlotController plotController, string[] parts);

        /// <summary>
        /// 查询名是否忽略大小写。
        /// 该配置用于启动期构建查询注册表，运行中不应修改。
        /// </summary>
        public const bool IgnoreQueryNameCase = true;

        private static readonly StringComparer QueryNameComparer = IgnoreQueryNameCase
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        private sealed class QueryHandlerInfo
        {
            public ConditionQueryHandler Handler { get; set; }

            public bool Cacheable { get; set; }
        }

        private static readonly Dictionary<string, QueryHandlerInfo> Handlers = BuildHandlers();

        /// <summary>
        /// 尝试执行已注册的条件查询指令。
        /// </summary>
        public static bool TryQuery(PlotController plotController, string[] parts, out string value)
        {
            value = "";
            if (parts == null || parts.Length == 0)
            {
                return false;
            }

            if (!Handlers.TryGetValue(parts[0], out QueryHandlerInfo handlerInfo))
            {
                return false;
            }

            value = handlerInfo.Handler(plotController, parts) ?? "";
            return true;
        }

        /// <summary>
        /// 判断指定查询名是否已经注册了外置查询处理器。
        /// </summary>
        public static bool CanHandle(string queryName)
        {
            return Handlers.ContainsKey(queryName);
        }

        /// <summary>
        /// 判断完整查询文本对应的处理器是否允许单次解析内缓存。
        /// 未注册查询按不可缓存处理，避免改变未知查询的行为。
        /// </summary>
        public static bool IsCacheable(string queryString)
        {
            if (string.IsNullOrEmpty(queryString))
            {
                return false;
            }

            int colonIndex = queryString.IndexOf(':');
            string queryName = colonIndex >= 0 ? queryString.Substring(0, colonIndex) : queryString;
            return Handlers.TryGetValue(queryName, out QueryHandlerInfo handlerInfo) && handlerInfo.Cacheable;
        }

        /// <summary>
        /// 扫描当前程序集中的 ConditionQueryAttribute，并构建"查询名 -> 处理方法"的映射表。
        /// 每个处理器类需要提供静态 TryQuery(PlotController, string[]) 方法。
        /// </summary>
        private static Dictionary<string, QueryHandlerInfo> BuildHandlers()
        {
            var handlers = new Dictionary<string, QueryHandlerInfo>(QueryNameComparer);
            Type[] types = typeof(ConditionQueryDispatcher).Assembly.GetTypes();

            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                object[] attrs = type.GetCustomAttributes(typeof(ConditionQueryAttribute), false);
                if (attrs == null || attrs.Length == 0)
                {
                    continue;
                }

                MethodInfo method = type.GetMethod(
                    "TryQuery",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new[] { typeof(PlotController), typeof(string[]) },
                    null);

                if (method == null)
                {
                    LoggerManager.Error($"ConditionQuery: {type.FullName} 标记了 ConditionQueryAttribute，但未提供静态 TryQuery(PlotController, string[]) 方法");
                    continue;
                }

                ConditionQueryHandler handler = (ConditionQueryHandler)Delegate.CreateDelegate(typeof(ConditionQueryHandler), method);
                for (int j = 0; j < attrs.Length; j++)
                {
                    var attr = (ConditionQueryAttribute)attrs[j];
                    if (string.IsNullOrWhiteSpace(attr.Name))
                    {
                        LoggerManager.Warning($"ConditionQuery: {type.FullName} 存在空查询名，已跳过");
                        continue;
                    }

                    if (handlers.ContainsKey(attr.Name))
                    {
                        LoggerManager.Warning($"ConditionQuery: 查询 {attr.Name} 存在重复处理器，后注册项将覆盖前注册项");
                    }

                    handlers[attr.Name] = new QueryHandlerInfo
                    {
                        Handler = handler,
                        Cacheable = attr.Cacheable
                    };
                }
            }

            LoggerManager.Debug($"ConditionQuery: 已注册外置查询处理器 {handlers.Count} 个");
            return handlers;
        }
    }

    /// <summary>
    /// 条件表达式的查询处理器集合
    /// 新增查询请在 Handlers/ConditionQuery 下添加带 ConditionQueryAttribute 的处理器类
    /// </summary>
    public static class ConditionQueryHandlers
    {
        /// <summary>
        /// 执行单个查询指令，返回字符串值
        /// 通过 ConditionQueryDispatcher 分发，新增查询类型无需修改此方法
        /// </summary>
        public static string ExecuteQuery(PlotController plotController, string queryString)
        {
            string[] parts = queryString.Split(':');
            if (parts.Length == 0) return "";

            if (ConditionQueryDispatcher.TryQuery(plotController, parts, out string value))
                return value;

            LoggerManager.Warning($"  未知查询类型: {parts[0]}");
            return "";
        }

        /// <summary>
        /// 在单次解析过程中执行查询；可缓存查询的相同完整查询文本只执行一次。
        /// </summary>
        internal static string ExecuteQueryWithCache(PlotController plotController, string queryString, ref Dictionary<string, string> queryCache)
        {
            if (!ConditionQueryDispatcher.IsCacheable(queryString))
            {
                return ExecuteQuery(plotController, queryString) ?? "";
            }

            if (queryCache == null)
            {
                queryCache = new Dictionary<string, string>(4, StringComparer.Ordinal);
            }

            if (queryCache.TryGetValue(queryString, out string cachedValue))
            {
                return cachedValue;
            }

            string value = ExecuteQuery(plotController, queryString) ?? "";
            queryCache[queryString] = value;
            return value;
        }

        /// <summary>
        /// 替换字符串中的 SUBARG 关键字为 PlotEventLogData 中存储的实际值。
        /// 供 ResolveAllCommands 和 ConditionExpressionEvaluator.Evaluate 共用，
        /// 确保条件表达式内的查询指令也能正确解析 SUBARG。
        /// </summary>
        public static string ReplaceSubArg(string input)
        {
            if (string.IsNullOrEmpty(input) || !input.Contains("SUBARG"))
                return input;

            PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
            if (logData == null || !logData.HaveKey("SUBARG"))
                return input;

            string subArgValue = logData.Get("SUBARG");
            if (string.IsNullOrEmpty(subArgValue))
                return input;

            return input.Replace("SUBARG", subArgValue);
        }

        /// <summary>
        /// 已知的指令语法标记，用于快速判断字符串是否包含可解析内容
        /// 扩展新语法时只需在此数组中添加新标记，并在 ResolveAllCommands 中增加对应的解析步骤
        /// </summary>
        private static readonly string[] SyntaxMarkers = { "[$", "[&" };

        /// <summary>
        /// 检查字符串是否包含任何可解析的指令语法
        /// </summary>
        public static bool ContainsParseableSyntax(string input)
        {
            if (string.IsNullOrEmpty(input)) return false;
            for (int i = 0; i < SyntaxMarkers.Length; i++)
            {
                if (input.Contains(SyntaxMarkers[i])) return true;
            }
            return false;
        }

        /// <summary>
        /// 解析字符串中所有指令语法，包括 [$查询$] 和 [&算术&]
        /// 解析顺序：保护 {{...}} → 解析 [$] → 解析 [&] → 还原 {{...}}
        /// 此方法是指令解析的统一入口，扩展新语法只需：
        ///   1. 在 SyntaxMarkers 中添加新标记
        ///   2. 在此方法中增加对应的解析步骤
        /// </summary>
        /// <param name="pc">PlotController 实例，为 null 时跳过 [$查询$] 解析（[&算术&] 仍可解析）</param>
        /// <param name="input">待解析的字符串</param>
        /// <returns>解析后的字符串；若无变化则返回原字符串引用</returns>
        public static string ResolveAllCommands(PlotController pc, string input)
        {
            Dictionary<string, string> queryCache = null;
            return ResolveAllCommands(pc, input, ref queryCache);
        }

        public static string ResolveAllCommands(PlotController pc, string input, ref Dictionary<string, string> queryCache)
        {
            if (string.IsNullOrEmpty(input) || !ContainsParseableSyntax(input))
                return input;

            // Step 0: 替换 SUBARG 关键字为实际值
            input = ReplaceSubArg(input);

            // Step 1: 保护 {{...}} 区域，替换为占位符
            var protectedAreas = new List<string>();
            string temp = Regex.Replace(input, @"\{\{(.+?)\}\}", m =>
            {
                protectedAreas.Add(m.Value);
                return $"__PROTECT_{protectedAreas.Count - 1}__";
            });

            // Step 2: 解析 [$...$]（先解析查询，为算术提供数值）
            if (temp.Contains("[$"))
            {
                if (pc == null)
                {
                    LoggerManager.Warning("指令解析: PlotController 为 null, 跳过 [$查询$] 解析");
                }
                else
                {
                    Dictionary<string, string> localQueryCache = queryCache;
                    temp = Regex.Replace(temp, @"\[\$(.+?)\$\]", m =>
                    {
                        string queryStr = m.Groups[1].Value;
                        try
                        {
                            string value = ExecuteQueryWithCache(pc, queryStr, ref localQueryCache);
                            return value ?? "";
                        }
                        catch (Exception ex)
                        {
                            LoggerManager.Error($"指令解析: [${queryStr}$] 异常: {ex.Message}");
                            return m.Value; // 保留原文
                        }
                    });
                    queryCache = localQueryCache;
                }
            }

            // Step 3: 解析 [&...&]（后解析算术，可使用查询结果；不需要 PlotController）
            if (temp.Contains("[&"))
            {
                temp = Regex.Replace(temp, @"\[&(.+?)&\]", m =>
                {
                    string arithExpr = m.Groups[1].Value;
                    try
                    {
                        double arithResult = ConditionExpressionEvaluator.ParseArithExpr(arithExpr);
                        if (double.IsNaN(arithResult))
                        {
                            LoggerManager.Warning($"指令解析: 算术 [&{arithExpr}&] 求值失败, 保留原文");
                            return m.Value;
                        }
                        // 整数结果不显示小数点
                        string resultStr = (arithResult == (long)arithResult)
                            ? ((long)arithResult).ToString()
                            : arithResult.ToString("G");
                        return resultStr;
                    }
                    catch (Exception ex)
                    {
                        LoggerManager.Error($"指令解析: [&{arithExpr}&] 异常: {ex.Message}");
                        return m.Value; // 保留原文
                    }
                });
            }

            // Step 4: 还原被保护的 {{...}} 区域
            for (int i = 0; i < protectedAreas.Count; i++)
            {
                temp = temp.Replace($"__PROTECT_{i}__", protectedAreas[i]);
            }

            return temp;
        }

        // ===== 查询处理器方法 =====

        /// <summary>
        /// 通用反射读取对象的属性或无参方法值
        /// 忽略大小写匹配，bool→"0"/"1"，数值→ToString("G")，枚举→整数字符串，其他→ToString()
        /// </summary>
        /// <param name="target">目标对象实例</param>
        /// <param name="typeName">类型名称，用于日志输出</param>
        /// <param name="fieldName">属性/字段/方法名</param>
        public static string ReadObjectFieldValue(object target, string typeName, string fieldName)
        {
            if (target == null) return "";

            var bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase;
            Type targetType = target.GetType();

            // 尝试属性
            PropertyInfo prop = targetType.GetProperty(fieldName, bindingFlags);
            if (prop != null)
            {
                try
                {
                    object val = prop.GetValue(target);
                    return ConvertFieldValue(val, prop.PropertyType);
                }
                catch (Exception e)
                {
                    LoggerManager.Error($"  {typeName}查询: 读取属性 {fieldName} 失败: {e.Message}");
                    return "";
                }
            }

            // 尝试字段
            FieldInfo field = targetType.GetField(fieldName, bindingFlags);
            if (field != null)
            {
                try
                {
                    object val = field.GetValue(target);
                    return ConvertFieldValue(val, field.FieldType);
                }
                catch (Exception e)
                {
                    LoggerManager.Error($"  {typeName}查询: 读取字段 {fieldName} 失败: {e.Message}");
                    return "";
                }
            }

            // 尝试无参方法
            MethodInfo method = targetType.GetMethod(fieldName, bindingFlags, null, Type.EmptyTypes, null);
            if (method != null)
            {
                try
                {
                    object val = method.Invoke(target, null);
                    return ConvertFieldValue(val, method.ReturnType);
                }
                catch (Exception e)
                {
                    LoggerManager.Error($"  {typeName}查询: 调用方法 {fieldName} 失败: {e.Message}");
                    return "";
                }
            }

            LoggerManager.Warning($"  {typeName}查询: 未找到属性/字段/方法 \"{fieldName}\"");
            return "";
        }

        /// <summary>
        /// 通用反射读取静态类的属性或无参方法值（用于 GlobalData 等纯静态类）
        /// 忽略大小写匹配，bool→"0"/"1"，数值→ToString("G")，枚举→整数字符串，其他→ToString()
        /// </summary>
        /// <param name="staticType">静态类 Type</param>
        /// <param name="typeName">类型名称，用于日志输出</param>
        /// <param name="fieldName">属性/字段/方法名</param>
        public static string ReadStaticFieldValue(Type staticType, string typeName, string fieldName)
        {
            var bindingFlags = BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase;

            // 尝试属性
            PropertyInfo prop = staticType.GetProperty(fieldName, bindingFlags);
            if (prop != null)
            {
                try
                {
                    object val = prop.GetValue(null);
                    return ConvertFieldValue(val, prop.PropertyType);
                }
                catch (Exception e)
                {
                    LoggerManager.Error($"  {typeName}查询: 读取属性 {fieldName} 失败: {e.Message}");
                    return "";
                }
            }

            // 尝试字段
            FieldInfo field = staticType.GetField(fieldName, bindingFlags);
            if (field != null)
            {
                try
                {
                    object val = field.GetValue(null);
                    return ConvertFieldValue(val, field.FieldType);
                }
                catch (Exception e)
                {
                    LoggerManager.Error($"  {typeName}查询: 读取字段 {fieldName} 失败: {e.Message}");
                    return "";
                }
            }

            // 尝试无参方法
            MethodInfo method = staticType.GetMethod(fieldName, bindingFlags, null, Type.EmptyTypes, null);
            if (method != null)
            {
                try
                {
                    object val = method.Invoke(null, null);
                    return ConvertFieldValue(val, method.ReturnType);
                }
                catch (Exception e)
                {
                    LoggerManager.Error($"  {typeName}查询: 调用方法 {fieldName} 失败: {e.Message}");
                    return "";
                }
            }

            LoggerManager.Warning($"  {typeName}查询: 未找到属性/字段/方法 \"{fieldName}\"");
            return "";
        }

        /// <summary>
        /// 将反射获取的值转换为字符串（通用）
        /// bool→"0"/"1"，数值→ToString("G")，枚举→整数字符串，其他→ToString()
        /// </summary>
        public static string ConvertFieldValue(object val, Type type)
        {
            if (val == null) return "";

            if (type == typeof(bool))
                return (bool)val ? "1" : "0";

            if (type == typeof(float))
                return ((float)val).ToString("G");

            if (type == typeof(double))
                return ((double)val).ToString("G");

            if (type == typeof(int))
                return ((int)val).ToString();

            if (type == typeof(long))
                return ((long)val).ToString();

            if (type == typeof(ulong))
                return ((ulong)val).ToString();

            // 枚举类型 → 返回整数值字符串（如 ItemType.Equip → "0"）
            if (type.IsEnum)
                return Convert.ChangeType(val, typeof(int)).ToString();

            return val.ToString();
        }

        /// <summary>
        /// 解析属性类型字符串为 BaseAttriType 枚举值（忽略大小写）
        /// 支持枚举名（如 Sword）和数字（如 10）
        /// </summary>
        public static bool TryParseAttriType(string attriName, out BaseAttriType attriType)
        {
            attriType = default(BaseAttriType);
            if (string.IsNullOrEmpty(attriName)) return false;

            // 尝试按枚举名解析（忽略大小写）
            if (Enum.TryParse(attriName, true, out BaseAttriType parsed))
            {
                attriType = parsed;
                return true;
            }

            // 尝试按数字解析
            if (int.TryParse(attriName, out int intVal))
            {
                if (Enum.IsDefined(typeof(BaseAttriType), intVal))
                {
                    attriType = (BaseAttriType)intVal;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 类型→子属性名 映射：当获取到的对象没有Count属性时，尝试取其子属性再计算Count
        /// 扩展方式：在此字典中添加 { typeof(目标类型), "子属性名" } 即可
        /// 示例: ItemListData 本身没有Count，但 ItemListData.allItem 有Count，所以映射为 "allItem"
        /// </summary>
        private static readonly Dictionary<Type, string> CountSubPropertyMap = new Dictionary<Type, string>
        {
            { typeof(ItemListData), "allItem" },
        };

        /// <summary>
        /// 获取对象列表属性的元素数量（通用实现）
        /// 格式: Count=属性/方法名
        ///   属性/方法名: 对象上返回列表(List/Il2Cpp List等有Count属性)的属性或无参方法名(忽略大小写)
        /// 当对象本身没有Count属性时，会根据 CountSubPropertyMap 尝试取子属性再计算Count
        /// 适用于 HeroData、ItemData、WorldData 等实例对象查询
        /// </summary>
        public static string GenericCount(object target, string typeName, string fieldName, BindingFlags bindingFlags)
        {
            if (target == null || string.IsNullOrWhiteSpace(fieldName))
            {
                LoggerManager.Warning($"  {typeName}查询 Count: 参数不足，格式[Count=属性/方法名]");
                return "";
            }

            Type targetType = target.GetType();
            object listObj = null;

            // 尝试属性
            PropertyInfo prop = targetType.GetProperty(fieldName, bindingFlags);
            if (prop != null)
            {
                try { listObj = prop.GetValue(target); }
                catch (Exception e)
                {
                    LoggerManager.Error($"  {typeName}查询 Count: 读取属性 {fieldName} 失败: {e.Message}");
                    return "-1";
                }
            }

            // 尝试字段
            if (listObj == null)
            {
                FieldInfo field = targetType.GetField(fieldName, bindingFlags);
                if (field != null)
                {
                    try { listObj = field.GetValue(target); }
                    catch (Exception e)
                    {
                        LoggerManager.Error($"  {typeName}查询 Count: 读取字段 {fieldName} 失败: {e.Message}");
                        return "-1";
                    }
                }
            }

            // 尝试无参方法
            if (listObj == null)
            {
                MethodInfo method = targetType.GetMethod(fieldName, bindingFlags, null, Type.EmptyTypes, null);
                if (method != null)
                {
                    try { listObj = method.Invoke(target, null); }
                    catch (Exception e)
                    {
                        LoggerManager.Error($"  {typeName}查询 Count: 调用方法 {fieldName} 失败: {e.Message}");
                        return "-1";
                    }
                }
            }

            if (listObj == null)
            {
                LoggerManager.Warning($"  {typeName}查询 Count: 未找到属性/字段/方法 \"{fieldName}\" 或其值为null");
                return "-1";
            }

            return ResolveCountFromObject(listObj, typeName, fieldName);
        }

        /// <summary>
        /// 从对象中获取Count值，支持自动通过 CountSubPropertyMap 解析子属性
        /// 当对象本身没有Count属性时，会查找 CountSubPropertyMap 中注册的子属性路径
        /// </summary>
        public static string ResolveCountFromObject(object listObj, string typeName, string fieldName)
        {
            // 通过反射获取 Count 属性（兼容 System.List 和 Il2Cpp List）
            var countProp = listObj.GetType().GetProperty("Count");
            if (countProp != null)
            {
                try { return ((int)countProp.GetValue(listObj)).ToString(); }
                catch (Exception e)
                {
                    LoggerManager.Error($"  {typeName}查询 Count: 读取 {fieldName}.Count 失败: {e.Message}");
                    return "-1";
                }
            }

            // 对象本身没有Count属性，尝试通过 CountSubPropertyMap 取子属性
            Type objType = listObj.GetType();
            if (CountSubPropertyMap.TryGetValue(objType, out string subPropName))
            {
                var subProp = objType.GetProperty(subPropName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (subProp != null)
                {
                    try
                    {
                        object subObj = subProp.GetValue(listObj);
                        if (subObj == null)
                        {
                            LoggerManager.Warning($"  {typeName}查询 Count: {fieldName}.{subPropName} 为null");
                            return "-1";
                        }
                        // 递归解析子对象的Count
                        return ResolveCountFromObject(subObj, typeName, $"{fieldName}.{subPropName}");
                    }
                    catch (Exception e)
                    {
                        LoggerManager.Error($"  {typeName}查询 Count: 读取 {fieldName}.{subPropName} 失败: {e.Message}");
                        return "-1";
                    }
                }

                // 尝试字段
                var subField = objType.GetField(subPropName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (subField != null)
                {
                    try
                    {
                        object subObj = subField.GetValue(listObj);
                        if (subObj == null)
                        {
                            LoggerManager.Warning($"  {typeName}查询 Count: {fieldName}.{subPropName} 为null");
                            return "-1";
                        }
                        return ResolveCountFromObject(subObj, typeName, $"{fieldName}.{subPropName}");
                    }
                    catch (Exception e)
                    {
                        LoggerManager.Error($"  {typeName}查询 Count: 读取 {fieldName}.{subPropName} 失败: {e.Message}");
                        return "-1";
                    }
                }

                LoggerManager.Warning($"  {typeName}查询 Count: {objType.Name} 在CountSubPropertyMap中注册了子属性\"{subPropName}\"，但未找到该属性/字段");
                return "-1";
            }

            LoggerManager.Warning($"  {typeName}查询 Count: \"{fieldName}\" 的返回类型 {objType.Name} 没有 Count 属性，也未在CountSubPropertyMap中注册子属性");
            return "-1";
        }

        /// <summary>
        /// 获取静态类列表属性的元素数量（用于 GlobalData 等静态类）
        /// 格式: Count=属性/方法名
        /// </summary>
        public static string GenericStaticCount(Type staticType, string typeName, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                LoggerManager.Warning($"  {typeName}查询 Count: 参数不足，格式[Count=属性/方法名]");
                return "";
            }

            var bindingFlags = BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase;
            object listObj = null;

            // 尝试静态属性
            PropertyInfo prop = staticType.GetProperty(fieldName, bindingFlags);
            if (prop != null)
            {
                try { listObj = prop.GetValue(null); }
                catch (Exception e)
                {
                    LoggerManager.Error($"  {typeName}查询 Count: 读取静态属性 {fieldName} 失败: {e.Message}");
                    return "";
                }
            }

            // 尝试静态字段
            if (listObj == null)
            {
                FieldInfo field = staticType.GetField(fieldName, bindingFlags);
                if (field != null)
                {
                    try { listObj = field.GetValue(null); }
                    catch (Exception e)
                    {
                        LoggerManager.Error($"  {typeName}查询 Count: 读取静态字段 {fieldName} 失败: {e.Message}");
                        return "";
                    }
                }
            }

            // 尝试静态无参方法
            if (listObj == null)
            {
                MethodInfo method = staticType.GetMethod(fieldName, bindingFlags, null, Type.EmptyTypes, null);
                if (method != null)
                {
                    try { listObj = method.Invoke(null, null); }
                    catch (Exception e)
                    {
                        LoggerManager.Error($"  {typeName}查询 Count: 调用静态方法 {fieldName} 失败: {e.Message}");
                        return "";
                    }
                }
            }

            if (listObj == null)
            {
                LoggerManager.Warning($"  {typeName}查询 Count: 未找到静态属性/字段/方法 \"{fieldName}\" 或其值为null");
                return "-1";
            }

            return ResolveCountFromObject(listObj, typeName, fieldName);
        }

        // ===== Value / Index 通用列表访问方法 =====

        /// <summary>
        /// 基础类型集合，这些类型的 ToString() 结果有意义
        /// </summary>
        private static readonly HashSet<Type> BasicTypes = new HashSet<Type>
        {
            typeof(int), typeof(long), typeof(short), typeof(byte), typeof(uint), typeof(ulong),
            typeof(float), typeof(double), typeof(decimal),
            typeof(string), typeof(bool)
        };

        /// <summary>
        /// 从对象列表属性中按索引取值（通用实现）
        /// 格式: Value=属性/方法名-索引
        ///   仅对基础类型(int/float/string/bool/enum)列表有效，复杂对象类型返回空串并 Warning
        /// </summary>
        public static string GenericValue(object target, string typeName, string fieldName, int index, BindingFlags bindingFlags)
        {
            if (target == null || string.IsNullOrWhiteSpace(fieldName))
            {
                LoggerManager.Warning($"  {typeName}查询 Value: 参数不足，格式[Value=属性/方法名-索引]");
                return "";
            }

            // 获取列表对象
            object listObj = ResolveListObject(target, typeName, fieldName, bindingFlags);
            if (listObj == null) return "";

            // 边界检查
            var countProp = listObj.GetType().GetProperty("Count");
            if (countProp == null)
            {
                LoggerManager.Warning($"  {typeName}查询 Value: \"{fieldName}\" 没有 Count 属性");
                return "";
            }
            int count = (int)countProp.GetValue(listObj);
            if (index < 0 || index >= count)
            {
                LoggerManager.Warning($"  {typeName}查询 Value: 索引 {index} 越界 (Count={count})");
                return "";
            }

            // 通过 Item[index] 索引器获取元素
            var itemProp = listObj.GetType().GetProperty("Item");
            if (itemProp == null)
            {
                LoggerManager.Warning($"  {typeName}查询 Value: \"{fieldName}\" 没有索引器 Item[int]");
                return "";
            }

            try
            {
                object element = itemProp.GetValue(listObj, new object[] { index });
                if (element == null)
                {
                    LoggerManager.Warning($"  {typeName}查询 Value: \"{fieldName}[{index}]\" 的值为null");
                    return "";
                }

                Type elemType = element.GetType();

                // 枚举类型 → 返回整数值
                if (elemType.IsEnum)
                    return Convert.ChangeType(element, typeof(int)).ToString();

                // 基础类型 → 返回 ToString()
                if (BasicTypes.Contains(elemType))
                    return element.ToString();

                // 复杂对象类型 → 返回空串并 Warning
                LoggerManager.Warning($"  {typeName}查询 Value: \"{fieldName}[{index}]\" 的元素类型为 {elemType.Name}，不是基础类型，无法返回有意义的值");
                return "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  {typeName}查询 Value: 读取 {fieldName}[{index}] 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 从静态类列表属性中按索引取值（用于 GlobalData 等静态类）
        /// 格式: Value=属性/方法名-索引
        /// </summary>
        public static string GenericStaticValue(Type staticType, string typeName, string fieldName, int index)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                LoggerManager.Warning($"  {typeName}查询 Value: 参数不足，格式[Value=属性/方法名-索引]");
                return "";
            }

            var bindingFlags = BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase;
            object listObj = ResolveStaticListObject(staticType, typeName, fieldName, bindingFlags);
            if (listObj == null) return "";

            // 边界检查
            var countProp = listObj.GetType().GetProperty("Count");
            if (countProp == null)
            {
                LoggerManager.Warning($"  {typeName}查询 Value: \"{fieldName}\" 没有 Count 属性");
                return "";
            }
            int count = (int)countProp.GetValue(listObj);
            if (index < 0 || index >= count)
            {
                LoggerManager.Warning($"  {typeName}查询 Value: 索引 {index} 越界 (Count={count})");
                return "";
            }

            var itemProp = listObj.GetType().GetProperty("Item");
            if (itemProp == null)
            {
                LoggerManager.Warning($"  {typeName}查询 Value: \"{fieldName}\" 没有索引器 Item[int]");
                return "";
            }

            try
            {
                object element = itemProp.GetValue(listObj, new object[] { index });
                if (element == null)
                {
                    LoggerManager.Warning($"  {typeName}查询 Value: \"{fieldName}[{index}]\" 的值为null");
                    return "";
                }

                Type elemType = element.GetType();

                if (elemType.IsEnum)
                    return Convert.ChangeType(element, typeof(int)).ToString();

                if (BasicTypes.Contains(elemType))
                    return element.ToString();

                LoggerManager.Warning($"  {typeName}查询 Value: \"{fieldName}[{index}]\" 的元素类型为 {elemType.Name}，不是基础类型，无法返回有意义的值");
                return "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  {typeName}查询 Value: 读取 {fieldName}[{index}] 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 从对象列表属性中按值查找索引（通用实现）
        /// 格式: Index=属性/方法名-查找值
        ///   仅对基础类型(int/float/string/bool/enum)列表有效
        ///   未找到返回 "-1"，float 类型使用容差比较
        /// </summary>
        public static string GenericIndex(object target, string typeName, string fieldName, string searchValue, BindingFlags bindingFlags)
        {
            if (target == null || string.IsNullOrWhiteSpace(fieldName))
            {
                LoggerManager.Warning($"  {typeName}查询 Index: 参数不足，格式[Index=属性/方法名-查找值]");
                return "";
            }

            // 获取列表对象
            object listObj = ResolveListObject(target, typeName, fieldName, bindingFlags);
            if (listObj == null) return "";

            return FindIndexInList(listObj, typeName, fieldName, searchValue);
        }

        /// <summary>
        /// 从静态类列表属性中按值查找索引（用于 GlobalData 等静态类）
        /// 格式: Index=属性/方法名-查找值
        /// </summary>
        public static string GenericStaticIndex(Type staticType, string typeName, string fieldName, string searchValue)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                LoggerManager.Warning($"  {typeName}查询 Index: 参数不足，格式[Index=属性/方法名-查找值]");
                return "";
            }

            var bindingFlags = BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase;
            object listObj = ResolveStaticListObject(staticType, typeName, fieldName, bindingFlags);
            if (listObj == null) return "";

            return FindIndexInList(listObj, typeName, fieldName, searchValue);
        }

        // ===== Value / Index 内部辅助方法 =====

        /// <summary>
        /// 解析实例对象的列表属性/字段/方法，返回列表对象
        /// </summary>
        private static object ResolveListObject(object target, string typeName, string fieldName, BindingFlags bindingFlags)
        {
            Type targetType = target.GetType();
            object listObj = null;

            PropertyInfo prop = targetType.GetProperty(fieldName, bindingFlags);
            if (prop != null)
            {
                try { listObj = prop.GetValue(target); }
                catch (Exception e)
                {
                    LoggerManager.Error($"  {typeName}查询: 读取属性 {fieldName} 失败: {e.Message}");
                    return null;
                }
            }

            if (listObj == null)
            {
                FieldInfo field = targetType.GetField(fieldName, bindingFlags);
                if (field != null)
                {
                    try { listObj = field.GetValue(target); }
                    catch (Exception e)
                    {
                        LoggerManager.Error($"  {typeName}查询: 读取字段 {fieldName} 失败: {e.Message}");
                        return null;
                    }
                }
            }

            if (listObj == null)
            {
                MethodInfo method = targetType.GetMethod(fieldName, bindingFlags, null, Type.EmptyTypes, null);
                if (method != null)
                {
                    try { listObj = method.Invoke(target, null); }
                    catch (Exception e)
                    {
                        LoggerManager.Error($"  {typeName}查询: 调用方法 {fieldName} 失败: {e.Message}");
                        return null;
                    }
                }
            }

            if (listObj == null)
            {
                LoggerManager.Warning($"  {typeName}查询: 未找到属性/字段/方法 \"{fieldName}\"");
            }
            return listObj;
        }

        /// <summary>
        /// 解析静态类的列表属性/字段/方法，返回列表对象
        /// </summary>
        private static object ResolveStaticListObject(Type staticType, string typeName, string fieldName, BindingFlags bindingFlags)
        {
            object listObj = null;

            PropertyInfo prop = staticType.GetProperty(fieldName, bindingFlags);
            if (prop != null)
            {
                try { listObj = prop.GetValue(null); }
                catch (Exception e)
                {
                    LoggerManager.Error($"  {typeName}查询: 读取静态属性 {fieldName} 失败: {e.Message}");
                    return null;
                }
            }

            if (listObj == null)
            {
                FieldInfo field = staticType.GetField(fieldName, bindingFlags);
                if (field != null)
                {
                    try { listObj = field.GetValue(null); }
                    catch (Exception e)
                    {
                        LoggerManager.Error($"  {typeName}查询: 读取静态字段 {fieldName} 失败: {e.Message}");
                        return null;
                    }
                }
            }

            if (listObj == null)
            {
                MethodInfo method = staticType.GetMethod(fieldName, bindingFlags, null, Type.EmptyTypes, null);
                if (method != null)
                {
                    try { listObj = method.Invoke(null, null); }
                    catch (Exception e)
                    {
                        LoggerManager.Error($"  {typeName}查询: 调用静态方法 {fieldName} 失败: {e.Message}");
                        return null;
                    }
                }
            }

            if (listObj == null)
            {
                LoggerManager.Warning($"  {typeName}查询: 未找到静态属性/字段/方法 \"{fieldName}\"");
            }
            return listObj;
        }

        /// <summary>
        /// 在列表中查找指定值的索引
        /// 支持 int/float/string/bool/enum 类型，float 使用容差比较
        /// 未找到返回 "-1"
        /// </summary>
        private static string FindIndexInList(object listObj, string typeName, string fieldName, string searchValue)
        {
            var countProp = listObj.GetType().GetProperty("Count");
            if (countProp == null)
            {
                LoggerManager.Warning($"  {typeName}查询 Index: \"{fieldName}\" 没有 Count 属性");
                return "-1";
            }
            var itemProp = listObj.GetType().GetProperty("Item");
            if (itemProp == null)
            {
                LoggerManager.Warning($"  {typeName}查询 Index: \"{fieldName}\" 没有索引器 Item[int]");
                return "-1";
            }

            int count = (int)countProp.GetValue(listObj);

            for (int i = 0; i < count; i++)
            {
                try
                {
                    object element = itemProp.GetValue(listObj, new object[] { i });
                    if (element == null) continue;

                    Type elemType = element.GetType();

                    // 枚举类型：将查找值解析为 int 再比较
                    if (elemType.IsEnum)
                    {
                        if (int.TryParse(searchValue, out int intVal) && Convert.ToInt32(element) == intVal)
                            return i.ToString();
                        continue;
                    }

                    // int / long / short / byte 等
                    if (element is int intElem)
                    {
                        if (int.TryParse(searchValue, out int intVal) && intElem == intVal)
                            return i.ToString();
                        continue;
                    }
                    if (element is long longElem)
                    {
                        if (long.TryParse(searchValue, out long longVal) && longElem == longVal)
                            return i.ToString();
                        continue;
                    }

                    // float：容差比较
                    if (element is float floatElem)
                    {
                        if (float.TryParse(searchValue, out float floatVal) && Math.Abs(floatElem - floatVal) < 0.0001f)
                            return i.ToString();
                        continue;
                    }

                    // double：容差比较
                    if (element is double doubleElem)
                    {
                        if (double.TryParse(searchValue, out double doubleVal) && Math.Abs(doubleElem - doubleVal) < 0.0001)
                            return i.ToString();
                        continue;
                    }

                    // string：直接比较
                    if (element is string strElem)
                    {
                        if (strElem == searchValue)
                            return i.ToString();
                        continue;
                    }

                    // bool：0/1 或 true/false
                    if (element is bool boolElem)
                    {
                        bool searchBool = searchValue.Equals("true", StringComparison.OrdinalIgnoreCase)
                                       || searchValue == "1";
                        if (boolElem == searchBool)
                            return i.ToString();
                        continue;
                    }

                    // 其他类型（复杂对象）跳过，继续查找
                    LoggerManager.Warning($"  {typeName}查询 Index: {fieldName}[{i}] 的元素类型为 {elemType.Name}，不是基础类型，跳过");
                    continue;
                }
                catch (Exception e)
                {
                    LoggerManager.Error($"  {typeName}查询 Index: 读取 {fieldName}[{i}] 失败: {e.Message}");
                    continue;
                }
            }

            LoggerManager.Debug($"  {typeName}查询 Index: 在 {fieldName} 中未找到值 \"{searchValue}\"");
            return "-1";
        }

        // ===== Value / Index 适配器方法 =====


    }

    /// <summary>
    /// 条件表达式解析与求值器
    /// 支持 [$查询$]、[&算术&]、[关系运算符]、[AND]、[OR]、()
    /// 求值流程: Step1 替换查询 → Step2 求值算术 → Step3 Tokenize → Step4 顶层逻辑拆分求值
    /// </summary>
    public static class ConditionExpressionEvaluator
    {
        /// <summary>
        /// 解析并求值条件表达式
        /// 支持 {{...}} 延迟求值标记：在 ResolveAllCommands 中被保护不预解析，
        /// 在此方法中剥离外层 {{}} 后正常求值，确保查询指令使用当前的上下文状态
        /// </summary>
        public static bool Evaluate(PlotController plotController, string expression, bool showDebugLog = true)
        {
            Dictionary<string, string> queryCache = null;
            return Evaluate(plotController, expression, ref queryCache, showDebugLog);
        }

        public static bool Evaluate(PlotController plotController, string expression, ref Dictionary<string, string> queryCache, bool showDebugLog = true)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                if (showDebugLog) LoggerManager.Debug("  Step0 空条件表达式，视为 true");
                return true;
            }

            // Step 0: 剥离 {{...}} 延迟求值标记
            // {{}} 在 ResolveAllCommands 中被保护（不预解析），在此处剥离后求值
            // 这样 ChooseHero 等指令的条件表达式可以使用遍历中的实时角色状态
            if (!string.IsNullOrEmpty(expression) && expression.StartsWith("{{"))
            {
                int closeIndex = expression.IndexOf("}}", 2);
                if (closeIndex >= 0)
                {
                    // 剥离 {{ }} ，保留内部表达式
                    expression = expression.Substring(2, closeIndex - 2);
                    if (showDebugLog) LoggerManager.Debug($"  Step0 剥离延迟标记后: {expression}");
                }
            }

            // Step 0.5: 替换 SUBARG 关键字为实际值
            // 条件表达式内的查询指令可能嵌入 SUBARG（如 [$GetStrVal:HeroFavor:SUBARG$]），
            // 必须在执行查询前替换为实际值，否则查询处理器会收到字面量 "SUBARG"
            expression = ConditionQueryHandlers.ReplaceSubArg(expression);

            // Step 1: 替换所有 [$查询$] 为字符串值（外层[]一并移除）
            Dictionary<string, string> localQueryCache = queryCache;
            string resolved = Regex.Replace(expression, @"\[\$(.+?)\$\]", m =>
            {
                string value = ConditionQueryHandlers.ExecuteQueryWithCache(plotController, m.Groups[1].Value, ref localQueryCache);
                if (showDebugLog) LoggerManager.Debug("  查询 [$" + m.Groups[1].Value + "$] → \"" + value + "\"");
                return value;
            });
            queryCache = localQueryCache;

            if (showDebugLog) LoggerManager.Debug($"  Step1 查询替换后: {resolved}");

            // Step 2: 求值所有 [&算术&]
            resolved = Regex.Replace(resolved, @"\[&(.+?)&\]", m =>
            {
                double arithResult = ParseArithExpr(m.Groups[1].Value);
                string arithStr = double.IsNaN(arithResult) ? "NaN" : arithResult.ToString("G");
                if (showDebugLog) LoggerManager.Debug($"  算术 [&{m.Groups[1].Value}&] → \"{arithStr}\"");
                return arithStr;
            });

            if (showDebugLog) LoggerManager.Debug($"  Step2 算术求值后: {resolved}");

            // Step 3: Tokenize
            string[] tokens = Tokenize(resolved);
            if (showDebugLog) LoggerManager.Debug($"  Step3 Tokenize: [{string.Join(", ", tokens)}]");

            // Step 4: 按当前括号层级拆分逻辑运算符，再解析比较表达式
            if (tokens.Length == 0)
            {
                if (showDebugLog) LoggerManager.Debug("  Step4 result: [False]，空表达式");
                return false;
            }

            ParseBoolResult parseResult = EvaluateSegment(tokens, 0, tokens.Length);
            if (!parseResult.Success)
            {
                LoggerManager.Warning($"条件表达式解析失败: {parseResult.Error}, 表达式={resolved}");
                return false;
            }

            bool result = parseResult.Value;
            if (showDebugLog) LoggerManager.Debug($"  Step4 result: [{result}]");

            return result;
        }

        /// <summary>
        /// 词法分析：将表达式拆分为 Token 序列
        /// Token 类型: 值 | [关系运算符] | [AND] | [OR] | ( | )
        /// </summary>
        private static string[] Tokenize(string expression)
        {
            List<string> tokens = new List<string>();
            int i = 0;
            while (i < expression.Length)
            {
                char c = expression[i];

                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                if (c == '(' || c == ')')
                {
                    tokens.Add(c.ToString());
                    i++;
                    continue;
                }

                if (c == '[')
                {
                    int end = expression.IndexOf(']', i);
                    if (end >= 0)
                    {
                        tokens.Add(expression.Substring(i, end - i + 1));
                        i = end + 1;
                        continue;
                    }
                }

                // 值：读取直到遇到 [ ( ) 或空白
                int start = i;
                while (i < expression.Length && expression[i] != '[' && expression[i] != '(' && expression[i] != ')' && !char.IsWhiteSpace(expression[i]))
                {
                    i++;
                }
                if (i > start)
                {
                    tokens.Add(expression.Substring(start, i - start));
                }
                else
                {
                    i++; // 跳过无法识别的字符
                }
            }
            return tokens.ToArray();
        }

        private struct ParseBoolResult
        {
            public bool Success;
            public bool Value;
            public string Error;

            public static ParseBoolResult Ok(bool value)
            {
                return new ParseBoolResult { Success = true, Value = value, Error = "" };
            }

            public static ParseBoolResult Fail(string error)
            {
                return new ParseBoolResult { Success = false, Value = false, Error = error };
            }
        }

        /// <summary>
        /// 按当前括号层级拆分 [OR] / [AND]，最后把不可再拆的片段作为比较表达式解析。
        /// </summary>
        private static ParseBoolResult EvaluateSegment(string[] tokens, int start, int end)
        {
            if (start >= end)
            {
                return ParseBoolResult.Fail("空条件片段");
            }

            string parenError;
            if (!ValidateParentheses(tokens, start, end, out parenError))
            {
                return ParseBoolResult.Fail(parenError);
            }

            while (CanStripOuterParentheses(tokens, start, end))
            {
                start++;
                end--;
                if (start >= end)
                {
                    return ParseBoolResult.Fail("空括号");
                }
            }

            List<int> orPositions = FindTopLevelOperatorPositions(tokens, start, end, "[OR]");
            if (orPositions.Count > 0)
            {
                return EvaluateSplitSegments(tokens, start, end, orPositions, true);
            }

            List<int> andPositions = FindTopLevelOperatorPositions(tokens, start, end, "[AND]");
            if (andPositions.Count > 0)
            {
                return EvaluateSplitSegments(tokens, start, end, andPositions, false);
            }

            return ParseComparison(tokens, start, end);
        }

        private static ParseBoolResult EvaluateSplitSegments(string[] tokens, int start, int end, List<int> operatorPositions, bool isOr)
        {
            bool result = isOr ? false : true;
            int segmentStart = start;

            for (int i = 0; i <= operatorPositions.Count; i++)
            {
                int segmentEnd = i < operatorPositions.Count ? operatorPositions[i] : end;
                if (segmentStart >= segmentEnd)
                {
                    return ParseBoolResult.Fail($"{(isOr ? "[OR]" : "[AND]")} 运算符两侧存在空条件片段");
                }

                ParseBoolResult segmentResult = EvaluateSegment(tokens, segmentStart, segmentEnd);
                if (!segmentResult.Success)
                {
                    return segmentResult;
                }

                result = isOr ? (result || segmentResult.Value) : (result && segmentResult.Value);
                segmentStart = segmentEnd + 1;
            }

            return ParseBoolResult.Ok(result);
        }

        private static bool ValidateParentheses(string[] tokens, int start, int end, out string error)
        {
            int depth = 0;
            for (int i = start; i < end; i++)
            {
                if (tokens[i] == "(")
                {
                    depth++;
                }
                else if (tokens[i] == ")")
                {
                    depth--;
                    if (depth < 0)
                    {
                        error = "多余的右括号";
                        return false;
                    }
                }
            }

            if (depth != 0)
            {
                error = "缺失右括号";
                return false;
            }

            error = "";
            return true;
        }

        private static bool CanStripOuterParentheses(string[] tokens, int start, int end)
        {
            if (end - start < 2 || tokens[start] != "(" || tokens[end - 1] != ")")
            {
                return false;
            }

            int depth = 0;
            for (int i = start; i < end; i++)
            {
                if (tokens[i] == "(")
                {
                    depth++;
                }
                else if (tokens[i] == ")")
                {
                    depth--;
                    if (depth == 0 && i < end - 1)
                    {
                        return false;
                    }
                }
            }

            return depth == 0;
        }

        private static List<int> FindTopLevelOperatorPositions(string[] tokens, int start, int end, string operatorToken)
        {
            List<int> positions = new List<int>();
            int depth = 0;

            for (int i = start; i < end; i++)
            {
                string token = tokens[i];
                if (token == "(")
                {
                    depth++;
                }
                else if (token == ")")
                {
                    depth--;
                }
                else if (depth == 0 && token.Equals(operatorToken, StringComparison.OrdinalIgnoreCase))
                {
                    positions.Add(i);
                }
            }

            return positions;
        }

        /// <summary>
        /// 解析比较片段。预解析造成的缺失操作数按空字符串处理，例如 [<>] 等价于 "" <> ""。
        /// </summary>
        private static ParseBoolResult ParseComparison(string[] tokens, int start, int end)
        {
            if (start >= end)
            {
                return ParseBoolResult.Fail("空比较表达式");
            }

            for (int i = start; i < end; i++)
            {
                if (tokens[i] == "(" || tokens[i] == ")")
                {
                    return ParseBoolResult.Fail("比较表达式中存在无法剥离的括号");
                }

                if (IsLogicalOperator(tokens[i]))
                {
                    return ParseBoolResult.Fail($"比较表达式中存在未拆分的逻辑运算符 {tokens[i]}");
                }
            }

            int opIndex = -1;
            for (int i = start; i < end; i++)
            {
                if (IsOperatorToken(tokens[i]))
                {
                    if (opIndex >= 0)
                    {
                        return ParseBoolResult.Fail("单个比较表达式中存在多个比较运算符");
                    }
                    opIndex = i;
                }
            }

            if (opIndex < 0)
            {
                if (end - start == 1)
                {
                    return ParseBoolResult.Ok(IsTruthy(tokens[start]));
                }
                return ParseBoolResult.Fail("单值表达式包含多个值 token");
            }

            string left;
            string right;
            string valueError;

            if (!TryReadComparisonValue(tokens, start, opIndex, out left, out valueError))
            {
                return ParseBoolResult.Fail(valueError);
            }

            if (!TryReadComparisonValue(tokens, opIndex + 1, end, out right, out valueError))
            {
                return ParseBoolResult.Fail(valueError);
            }

            return ParseBoolResult.Ok(CompareValues(left, tokens[opIndex], right));
        }

        private static bool TryReadComparisonValue(string[] tokens, int start, int end, out string value, out string error)
        {
            if (start >= end)
            {
                value = "";
                error = "";
                return true;
            }

            if (end - start == 1)
            {
                value = tokens[start];
                error = "";
                return true;
            }

            value = "";
            error = "比较操作数包含多个值 token";
            return false;
        }

        private static bool IsLogicalOperator(string token)
        {
            return token.Equals("[AND]", StringComparison.OrdinalIgnoreCase)
                || token.Equals("[OR]", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 比较两个值
        /// </summary>
        private static bool CompareValues(string left, string op, string right)
        {
            // 尝试数值比较
            if (double.TryParse(left, out double leftNum) && double.TryParse(right, out double rightNum))
            {
                switch (op)
                {
                    case "[>]": return leftNum > rightNum;
                    case "[<]": return leftNum < rightNum;
                    case "[=]": return Math.Abs(leftNum - rightNum) < 0.0001;
                    case "[>=]": return leftNum >= rightNum;
                    case "[<=]": return leftNum <= rightNum;
                    case "[<>]": return Math.Abs(leftNum - rightNum) >= 0.0001;
                    default: return false;
                }
            }

            // 字符串比较
            switch (op.ToUpperInvariant())
            {
                case "[=]": return left == right;
                case "[<>]": return left != right;
                case "[>]": return string.Compare(left, right, StringComparison.Ordinal) > 0;
                case "[<]": return string.Compare(left, right, StringComparison.Ordinal) < 0;
                case "[>=]": return false; // 字符串无法合理判断大小，安全起见返回false
                case "[<=]": return false; // 同上
                case "[IN]": return right.Contains(left);
                default: return false;
            }
        }

        /// <summary>
        /// 判断值是否为"真"（非0非空）
        /// </summary>
        private static bool IsTruthy(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            if (double.TryParse(value, out double num)) return num != 0;
            return true; // 非空非数字字符串视为true
        }

        /// <summary>
        /// 判断 token 是否为关系运算符（如 [&gt;], [&lt;], [=] 等）
        /// </summary>
        private static bool IsOperatorToken(string token)
        {
            return token == "[>]" || token == "[<]" || token == "[=]"
                || token == "[>=]" || token == "[<=]" || token == "[<>]"
                || token.Equals("[IN]", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 解析算术表达式入口
        /// 支持 + - * / % ^，^ 为右结合
        /// </summary>
        public static double ParseArithExpr(string expr)
        {
            int pos = 0;
            double result = ParseArithAddSub(expr, ref pos);
            return result;
        }

        /// <summary>
        /// 加减: add_sub := mul_div_mod ([+ -] mul_div_mod)*
        /// </summary>
        private static double ParseArithAddSub(string expr, ref int pos)
        {
            double result = ParseArithMulDivMod(expr, ref pos);
            while (pos < expr.Length)
            {
                SkipArithWhitespace(expr, ref pos);
                if (pos >= expr.Length) break;
                char op = expr[pos];
                if (op != '+' && op != '-') break;
                pos++;
                double right = ParseArithMulDivMod(expr, ref pos);
                if (op == '+') result += right;
                else result -= right;
            }
            return result;
        }

        /// <summary>
        /// 乘除模: mul_div_mod := power ([* / %] power)*
        /// </summary>
        private static double ParseArithMulDivMod(string expr, ref int pos)
        {
            double result = ParseArithPower(expr, ref pos);
            while (pos < expr.Length)
            {
                SkipArithWhitespace(expr, ref pos);
                if (pos >= expr.Length) break;
                char op = expr[pos];
                if (op != '*' && op != '/' && op != '%') break;
                pos++;
                double right = ParseArithPower(expr, ref pos);
                if (op == '*') result *= right;
                else if (op == '/') result = right != 0 ? result / right : double.NaN;
                else result = right != 0 ? result % right : double.NaN;
            }
            return result;
        }

        /// <summary>
        /// 幂: power := atom [^] power（右结合）
        /// </summary>
        private static double ParseArithPower(string expr, ref int pos)
        {
            double result = ParseArithAtom(expr, ref pos);
            SkipArithWhitespace(expr, ref pos);
            if (pos < expr.Length && expr[pos] == '^')
            {
                pos++;
                double right = ParseArithPower(expr, ref pos); // 右结合：递归调用自身
                result = Math.Pow(result, right);
            }
            return result;
        }

        /// <summary>
        /// 原子: 数字 | (算术表达式) | @函数(参数,参数,...)
        /// 支持的函数: @max, @min, @random
        /// </summary>
        private static double ParseArithAtom(string expr, ref int pos)
        {
            SkipArithWhitespace(expr, ref pos);
            if (pos >= expr.Length) return 0;

            // 函数调用: @funcName(args...)
            if (expr[pos] == '@')
            {
                return ParseArithFunc(expr, ref pos);
            }

            if (expr[pos] == '(')
            {
                pos++; // skip '('
                double result = ParseArithAddSub(expr, ref pos);
                SkipArithWhitespace(expr, ref pos);
                if (pos < expr.Length && expr[pos] == ')') pos++; // skip ')'
                return result;
            }

            // 读取数字
            int start = pos;
            if (pos < expr.Length && (expr[pos] == '-' || expr[pos] == '+'))
                pos++;
            while (pos < expr.Length && (char.IsDigit(expr[pos]) || expr[pos] == '.'))
                pos++;

            string numStr = expr.Substring(start, pos - start);
            if (double.TryParse(numStr, out double num))
                return num;

            LoggerManager.Error($"  算术解析错误: 无法解析数值 \"{numStr}\"");
            return double.NaN;
        }

        private static readonly System.Random _arithRandom = new System.Random();

        /// <summary>
        /// 解析函数调用: @funcName(arg1, arg2, ...)
        /// 支持的函数: @max, @min, @random
        /// </summary>
        private static double ParseArithFunc(string expr, ref int pos)
        {
            pos++; // skip '@'

            // 读取函数名
            int nameStart = pos;
            while (pos < expr.Length && (char.IsLetter(expr[pos]) || expr[pos] == '_'))
                pos++;
            string funcName = expr.Substring(nameStart, pos - nameStart);

            SkipArithWhitespace(expr, ref pos);

            // 期望 '('
            if (pos >= expr.Length || expr[pos] != '(')
            {
                LoggerManager.Error($"  算术解析错误: 函数 @{funcName} 后缺少 '('");
                return double.NaN;
            }
            pos++; // skip '('

            // 解析参数列表
            var args = new List<double>();
            SkipArithWhitespace(expr, ref pos);
            if (pos < expr.Length && expr[pos] != ')')
            {
                // 至少一个参数
                args.Add(ParseArithAddSub(expr, ref pos));
                SkipArithWhitespace(expr, ref pos);
                while (pos < expr.Length && expr[pos] == ',')
                {
                    pos++; // skip ','
                    args.Add(ParseArithAddSub(expr, ref pos));
                    SkipArithWhitespace(expr, ref pos);
                }
            }

            // 期望 ')'
            if (pos < expr.Length && expr[pos] == ')')
                pos++; // skip ')'
            else
                LoggerManager.Error($"  算术解析错误: 函数 @{funcName} 缺少闭合 ')'");

            // 执行函数
            if (args.Count == 0)
            {
                LoggerManager.Error($"  算术解析错误: 函数 @{funcName} 至少需要1个参数");
                return double.NaN;
            }

            switch (funcName.ToLowerInvariant())
            {
                case "max":
                    return args.Max();
                case "min":
                    return args.Min();
                case "random":
                    lock (_arithRandom)
                    {
                        return args[_arithRandom.Next(args.Count)];
                    }
                default:
                    LoggerManager.Error($"  算术解析错误: 未知函数 @{funcName}，支持的函数: @max, @min, @random");
                    return double.NaN;
            }
        }

        /// <summary>
        /// 跳过算术表达式中的空白
        /// </summary>
        private static void SkipArithWhitespace(string expr, ref int pos)
        {
            while (pos < expr.Length && char.IsWhiteSpace(expr[pos]))
                pos++;
        }
    }
}
