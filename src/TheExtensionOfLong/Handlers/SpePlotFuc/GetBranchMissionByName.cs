using Il2Cpp;
using Il2CppInterop.Runtime;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 根据任务名称获取支线任务并交给GameController处理
    /// 在 BranchMissionDataBase 中查找 MissionData.name 匹配的任务，Clone 后调用 GetFullMission
    /// 格式: GetBranchMissionByName*任务名称
    /// 示例: GetBranchMissionByName*采薇任务
    /// </summary>
    [SpePlotFuc("GetBranchMissionByName")]
    public static class SpePlotFucGetBranchMissionByName
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 1 || string.IsNullOrWhiteSpace(fucParams[0]))
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*任务名称]");
                return;
            }

            string missionName = fucParams[0];

            MissionDataController missionDataController = MissionDataController._instance;
            if (missionDataController == null)
            {
                LoggerManager.Error($"{fucName}: MissionDataController实例不存在");
                return;
            }

            var branchMissionDataBase = missionDataController.BranchMissionDataBase;
            if (branchMissionDataBase == null)
            {
                LoggerManager.Error($"{fucName}: BranchMissionDataBase为空");
                return;
            }

            MissionData found = null;
            for (int i = 0; i < branchMissionDataBase.Count; i++)
            {
                MissionData md = branchMissionDataBase[i];
                if (md != null && md.name == missionName)
                {
                    found = md;
                    break;
                }
            }

            if (found == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到名称为\"{missionName}\"的支线任务");
                return;
            }

            GameController gameController = GameController.Instance;
            if (gameController == null)
            {
                LoggerManager.Error($"{fucName}: GameController实例不存在");
                return;
            }

            MissionData cloned = found.Clone().Cast<MissionData>();
            gameController.GetFullMission(cloned);
            LoggerManager.Debug($"{fucName}: 已获取支线任务 name={missionName}, speMissionID={cloned.speMissionID}");
        }
    }
}
