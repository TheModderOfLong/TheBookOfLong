using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheResourceOfLong
{
    /// <summary>
    /// 日志级别，数值越大输出越详细
    /// </summary>
    public enum LogLevel
    {
        None = 0,     // 静默，不输出任何日志
        Error = 1,    // 仅错误
        Warning = 2,  // 错误 + 警告
        Info = 3,     // 错误 + 警告 + 一般信息
        Debug = 4     // 全部，含调试详情
    }

    /// <summary>
    /// 分级日志管理器，封装 MelonLogger 并提供级别过滤
    /// 发布时修改 CurrentLogLevel 为 Warning 或 Error 以减少输出；
    /// 运行时按 F2（由 XGMod.OnUpdate 驱动）循环切换级别。
    /// </summary>
    public static class LoggerManager
    {
        private static readonly MelonLogger.Instance Log = Melon<ModMain>.Logger;

        /// <summary>
        /// 当前日志级别，默认 Info。发布时可改为 Warning 以减少输出。
        /// </summary>
        public static LogLevel CurrentLogLevel = LogLevel.Debug;

        /// <summary>调试级别日志（反射查询细节、条件求值中间步骤等）</summary>
        public static void Debug(string msg)
        {
            if (CurrentLogLevel >= LogLevel.Debug) Log.Msg(msg);
        }

        /// <summary>信息级别日志（指令触发、条件分支结果、变量设置成功等）</summary>
        public static void Info(string msg)
        {
            // Log.Msg($"LogLevel={LogLevel.Info}, CurrentLogLevel={CurrentLogLevel}");
            if (CurrentLogLevel >= LogLevel.Info) Log.Msg(msg);
        }

        /// <summary>警告级别日志（参数不足/格式错误、实例为空等可预期异常）</summary>
        public static void Warning(string msg)
        {
            if (CurrentLogLevel >= LogLevel.Warning) Log.Warning(msg);
        }

        /// <summary>错误级别日志（严重错误、反射调用失败等）</summary>
        public static void Error(string msg)
        {
            if (CurrentLogLevel >= LogLevel.Error) Log.Error(msg);
        }

        /// <summary>
        /// 循环切换日志级别: Debug → Info → Warning → Error → None → Debug → ...
        /// </summary>
        public static void CycleLogLevel()
        {
            switch (CurrentLogLevel)
            {
                case LogLevel.Debug: CurrentLogLevel = LogLevel.Info; break;
                case LogLevel.Info: CurrentLogLevel = LogLevel.Warning; break;
                case LogLevel.Warning: CurrentLogLevel = LogLevel.Error; break;
                case LogLevel.Error: CurrentLogLevel = LogLevel.None; break;
                case LogLevel.None: CurrentLogLevel = LogLevel.Debug; break;
                default: CurrentLogLevel = LogLevel.Info; break;
            }
        }
    }
}
