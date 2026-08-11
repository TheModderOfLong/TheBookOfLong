using System;
using System.Collections.Generic;
using System.Reflection;
using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 标记一个 SpePlotFuc 拓展指令处理器。
    /// Name 对应剧情表中使用的指令名称。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class SpePlotFucAttribute : Attribute
    {
        public SpePlotFucAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    /// <summary>
    /// SpePlotFuc 拓展指令处理器。
    /// 负责解析指令参数、自动发现处理器并分发到对应的指令类。
    /// </summary>
    public static class SpePlotFucHandlers
    {
        private delegate void SpePlotFucHandler(PlotController plotController, string fucName, string[] fucParams);

        private static readonly Dictionary<string, SpePlotFucHandler> Handlers = BuildHandlers();

        /// <summary>
        /// 尝试处理 SpePlotFuc 拓展指令。
        /// 返回值遵循 Harmony Prefix 约定：true 表示继续执行原方法，false 表示拦截原方法。
        /// </summary>
        public static bool TryHandle(PlotController plotController, ref string param)
        {
            if (param == null)
            {
                return true;
            }

            try
            {
                string resolved = ConditionQueryHandlers.ResolveAllCommands(plotController, param);
                if (resolved != param)
                {
                    LoggerManager.Debug($"SpePlotFuc: 参数解析完成: \"{param}\" -> \"{resolved}\"");
                    param = resolved;
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"SpePlotFuc: 参数解析异常 - {ex.Message}");
            }

            var parsed = PlotCommandHandler.ParseCommandParams(param, '*', '#');
            if (parsed == null)
            {
                return true;
            }

            string fucName = parsed.Value.fucName;
            string[] fucParams = parsed.Value.fucParams;

            if (CanHandle(fucName))
            {
                LoggerManager.Debug($"SpePlotFuc: {fucName}({string.Join(", ", fucParams)})");
                TryCall(plotController, fucName, fucParams);
                return false;
            }

            LoggerManager.Warning($"SpePlotFuc: 不存在的拓展指令 - {fucName}");
            return true;
        }

        /// <summary>
        /// 判断指定指令名是否已经注册了 SpePlotFuc 拓展处理器。
        /// </summary>
        public static bool CanHandle(string fucName)
        {
            return Handlers.ContainsKey(fucName);
        }

        /// <summary>
        /// 调用已经注册的 SpePlotFuc 拓展处理器。
        /// </summary>
        public static bool TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (!Handlers.TryGetValue(fucName, out SpePlotFucHandler handler))
            {
                return false;
            }

            handler(plotController, fucName, fucParams);
            return true;
        }

        /// <summary>
        /// 扫描当前程序集中的 SpePlotFucAttribute，并构建“指令名 -> 处理方法”的映射表。
        /// 每个处理器类需要提供静态 TryCall(PlotController, string, string[]) 方法。
        /// </summary>
        private static Dictionary<string, SpePlotFucHandler> BuildHandlers()
        {
            var handlers = new Dictionary<string, SpePlotFucHandler>();
            Type[] types = typeof(SpePlotFucHandlers).Assembly.GetTypes();

            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                object[] attrs = type.GetCustomAttributes(typeof(SpePlotFucAttribute), false);
                if (attrs == null || attrs.Length == 0)
                {
                    continue;
                }

                MethodInfo method = type.GetMethod(
                    "TryCall",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new[] { typeof(PlotController), typeof(string), typeof(string[]) },
                    null);

                if (method == null)
                {
                    LoggerManager.Error($"SpePlotFuc: {type.FullName} 标记了 SpePlotFucAttribute，但未提供静态 TryCall(PlotController, string, string[]) 方法");
                    continue;
                }

                SpePlotFucHandler handler = (SpePlotFucHandler)Delegate.CreateDelegate(typeof(SpePlotFucHandler), method);
                for (int j = 0; j < attrs.Length; j++)
                {
                    var attr = (SpePlotFucAttribute)attrs[j];
                    if (string.IsNullOrWhiteSpace(attr.Name))
                    {
                        LoggerManager.Warning($"SpePlotFuc: {type.FullName} 存在空指令名，已跳过");
                        continue;
                    }

                    if (handlers.ContainsKey(attr.Name))
                    {
                        LoggerManager.Warning($"SpePlotFuc: 指令 {attr.Name} 存在重复 SpePlotFuc 处理器，后注册项将覆盖前注册项");
                    }

                    handlers[attr.Name] = handler;
                }
            }

            LoggerManager.Debug($"SpePlotFuc: 已注册 SpePlotFuc 指令处理器 {handlers.Count} 个");
            return handlers;
        }
    }
}
