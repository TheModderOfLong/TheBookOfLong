using Il2Cpp;
using System;

namespace TheExtensionOfLong
{
    [SpePlotFuc("SetTargetMission")]
    public static class SpePlotFucSetTargetMission
    {
        /// <summary>
        /// 将指定任务设置为当前剧情任务上下文。
        /// 格式: SetTargetMission*任务来源#对象(可选)
        ///   任务来源: 空/nowMission, forceMission, speMissionID-1001, speMissionID=1001, speMissionID:1001, 任务id, 任务名
        ///   对象: 当前仅支持 nowMission；省略时同 nowMission。
        /// </summary>
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 1)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*任务来源#对象(可选)]");
                return;
            }

            string missionSource = fucParams[0];
            string targetSlot = fucParams.Length > 1 ? fucParams[1] : "";
            string lowerSlot = (targetSlot ?? "").Trim().ToLowerInvariant();

            if (!string.IsNullOrEmpty(lowerSlot) && lowerSlot != "nowmission" && lowerSlot != "当前任务")
            {
                LoggerManager.Warning($"{fucName}: 不支持的任务槽位 \"{targetSlot}\"，当前仅支持 nowMission");
                return;
            }

            MissionData mission = CommonHandlers.ResolveMissionSource(plotController, missionSource);
            if (mission == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到任务 \"{(string.IsNullOrEmpty(missionSource) ? "nowMission" : missionSource)}\"");
                return;
            }

            if (plotController == null)
            {
                LoggerManager.Warning($"{fucName}: PlotController为空，无法设置 nowMission");
                return;
            }

            plotController.nowMission = mission;
            LoggerManager.Debug($"{fucName}: 已将任务 {mission.name}(id={mission.id}, speMissionID={mission.speMissionID}) 设为 nowMission");
        }
    }
}
