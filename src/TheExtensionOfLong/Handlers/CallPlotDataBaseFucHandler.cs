using System;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 处理 CallPlotDataBaseFuc 指令：以函数子程序方式调用指定数据库剧情。
    /// 该处理器只执行 plotCallFuc 和 clickCallFuc，不进入剧情播放/UI 生命周期。
    /// </summary>
    public static class CallPlotDataBaseFucHandler
    {
        private const int MaxCallDepth = 16;
        private static int _callDepth;

        /// <summary>
        /// 执行 CallPlotDataBaseFuc*剧情ID 指令。
        /// </summary>
        public static void Handle(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 1 || string.IsNullOrWhiteSpace(fucParams[0]))
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*剧情ID]");
                return;
            }

            if (_callDepth >= MaxCallDepth)
            {
                LoggerManager.Error($"{fucName}: 递归调用深度超过限制({MaxCallDepth})，已中止，剧情ID={fucParams[0]}");
                return;
            }

            if (!CommonHandlers.TryResolvePlotDataBaseId(fucParams[0], out int plotID))
            {
                LoggerManager.Warning($"{fucName}: 无法解析剧情ID [{fucParams[0]}]");
                return;
            }

            GameDataController dataController = GameDataController._instance;
            Dictionary<int, PlotData> plotDataBase = dataController?.PlotDataBase;
            if (!CommonHandlers.TryGetPlotDataByPlotId(plotDataBase, plotID, out PlotData plotData))
            {
                LoggerManager.Warning($"{fucName}: 剧情ID不存在 [{plotID}]");
                return;
            }

            _callDepth++;
            try
            {
                PlotData startNowPlot = plotController.nowPlot;
                SinglePlotData startNowSinglePlot = plotController.nowSinglePlot;

                LoggerManager.Debug($"{fucName}: 开始执行剧情函数 plotID={plotID}, plotName={plotData.plotName}, depth={_callDepth}");

                PreparePlotContext(plotController, plotData);
                ExecuteCallback(plotController, plotData.plotCallFuc, false, $"{fucName}.plotCallFuc[{plotID}]");
                if (HasPlotContextChanged(plotController, startNowPlot, startNowSinglePlot))
                {
                    LoggerManager.Debug($"{fucName}: plotCallFuc 触发剧情现场切换，停止执行后续 SinglePlotData，plotID={plotID}");
                    return;
                }

                List<SinglePlotData> singlePlots = plotData.plotDatas;
                if (singlePlots == null)
                {
                    return;
                }

                for (int i = 0; i < singlePlots.Count; i++)
                {
                    SinglePlotData single = singlePlots[i];
                    if (single == null)
                    {
                        continue;
                    }

                    PrepareSinglePlotContext(plotController, single);
                    ExecuteCallback(plotController, single.clickCallFuc, true, $"{fucName}.clickCallFuc[{plotID}:{i}]");

                    if (HasPlotContextChanged(plotController, startNowPlot, startNowSinglePlot))
                    {
                        LoggerManager.Debug($"{fucName}: clickCallFuc[{i}] 触发剧情现场切换，停止执行后续 SinglePlotData，plotID={plotID}");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"{fucName}: 执行剧情函数集合失败 plotID={plotID}, error={ex}");
            }
            finally
            {
                _callDepth--;
            }
        }

        /// <summary>
        /// 准备 PlotData 级业务上下文，仅同步本体 ShowPlot 中的非 UI 上下文字段。
        /// </summary>
        private static void PreparePlotContext(PlotController plotController, PlotData plotData)
        {
            GameController gameController = GameController._instance;
            if (gameController == null)
            {
                return;
            }

            if (plotData.targetHeroID != 0 && gameController.worldData != null)
            {
                plotController.targetInteractHero = gameController.worldData.GetHero(plotData.targetHeroID);
            }

            if (plotData.plotRandomHero != null && plotData.plotRandomHero.Count > 0)
            {
                plotController.plotInteractHeroList = gameController.GetRandomHero(plotData.plotRandomHero, plotData.differentForce);
            }
        }

        /// <summary>
        /// 准备 SinglePlotData 级业务上下文，仅同步本体 ShowSinglePlot 中的角色上下文字段。
        /// </summary>
        private static void PrepareSinglePlotContext(PlotController plotController, SinglePlotData single)
        {
            plotController.sourceInteractHero = plotController.GetHeroData(single.plotSource, single.sourceName, plotController.sourceInteractHero);
            plotController.targetInteractHero = plotController.GetHeroData(single.plotTarget, single.targetName, plotController.targetInteractHero);
        }

        /// <summary>
        /// 执行 plotCallFuc 或 clickCallFuc 回调；clickCallFuc 按当前 ChangeNextPlotPatch 语义支持 "|" 多回调。
        /// </summary>
        private static void ExecuteCallback(PlotController plotController, string callbackText, bool allowMultiCallback, string sourceLabel)
        {
            if (string.IsNullOrWhiteSpace(callbackText))
            {
                return;
            }

            string[] callbacks = allowMultiCallback
                ? PlotCommandHandler.SplitClickCallFucCallbacks(callbackText)
                : new[] { callbackText };

            for (int i = 0; i < callbacks.Length; i++)
            {
                string raw = callbacks[i];
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                try
                {
                    PlotCommandHandler.ExecuteSendMessageCallback(plotController, raw, $"{sourceLabel}:{i}");
                }
                catch (Exception ex)
                {
                    LoggerManager.Error($"CallPlotDataBaseFuc: 执行回调失败 source={sourceLabel}, callback={raw}, error={ex}");
                }
            }
        }

        /// <summary>
        /// 判断回调是否显式切换了剧情现场，用于阻止继续执行后续 SinglePlotData。
        /// </summary>
        private static bool HasPlotContextChanged(PlotController plotController, PlotData startNowPlot, SinglePlotData startNowSinglePlot)
        {
            return plotController.nowPlot != startNowPlot || plotController.nowSinglePlot != startNowSinglePlot;
        }
    }
}
