using Il2Cpp;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace TheExtensionOfLong
{
    /// <summary>
    /// AreaData 查询指令。
    /// 格式: [$AreaData:区域信息:区域ID/名称/关键字(可选)$]
    /// 区域ID为空时默认读取 targetArea/目标地区。
    /// </summary>
    [ConditionQuery("AreaData")]
    public static class QueryAreaData
    {
        private static readonly Dictionary<string, Func<PlotController, AreaData, string[], string>> CompositeMethods
            = new Dictionary<string, Func<PlotController, AreaData, string[], string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "GetForceID", CompositeGetForceID },
            { "GetForceName", CompositeGetForceName },
            { "GetBranchLeaderID", CompositeGetBranchLeaderID },
            { "GetBranchLeaderName", CompositeGetBranchLeaderName },
            { "GetInsideHeroID", CompositeGetInsideHeroID },
            { "GetInsideHeroName", CompositeGetInsideHeroName },
            { "HaveInsideHero", CompositeHaveInsideHero },
            { "GetAreaState", CompositeGetAreaState },
            { "GetAreaStatePercent", CompositeGetAreaStatePercent },
            { "GetMaxAreaState", CompositeGetMaxAreaState },
            { "GetChangeAreaState", CompositeGetChangeAreaState },
            { "GetResourceValueRate", CompositeGetResourceValueRate },
            { "GetChangeResource", CompositeGetChangeResource },
            { "FindBuildingID", CompositeFindBuildingID },
            { "FindBuildingName", CompositeFindBuildingName },
            { "GetCenterBuildingID", CompositeGetCenterBuildingID },
            { "GetTileBuildingID", CompositeGetTileBuildingID },
            { "Count", CompositeCount },
            { "Value", CompositeValue },
            { "Index", CompositeIndex },
        };

        public static string TryQuery(PlotController plotController, string[] parts)
        {
            if (parts.Length < 2)
            {
                LoggerManager.Warning("  AreaData查询: 参数不足，格式[AreaData:区域信息:区域ID/名称/关键字(可选)]");
                return "";
            }

            string fieldInfo = parts[1];
            string areaIdRaw = parts.Length > 2 ? parts[2] : null;

            AreaData area = CommonHandlers.ResolveAreaId(plotController, areaIdRaw);
            if (area == null)
            {
                LoggerManager.Warning($"  AreaData查询: 未找到区域 (id=\"{areaIdRaw ?? "targetArea"}\")");
                return "";
            }

            int eqIdx = fieldInfo.IndexOf('=');
            if (eqIdx >= 0)
            {
                string methodName = fieldInfo.Substring(0, eqIdx);
                string methodArg = fieldInfo.Substring(eqIdx + 1);
                return ResolveCompositeMethod(plotController, area, methodName, methodArg);
            }

            if (CompositeMethods.ContainsKey(fieldInfo))
                return ResolveCompositeMethod(plotController, area, fieldInfo, "");

            return ConditionQueryHandlers.ReadObjectFieldValue(area, "AreaData", fieldInfo);
        }

        private static string ResolveCompositeMethod(PlotController plotController, AreaData area, string methodName, string methodArg)
        {
            string[] args = methodArg.Split('-');

            if (CompositeMethods.TryGetValue(methodName, out var handler))
                return handler(plotController, area, args);

            LoggerManager.Warning($"  AreaData查询: 未知复合方法 \"{methodName}\"");
            return "";
        }

        private static string CompositeGetForceID(PlotController plotController, AreaData area, string[] args)
        {
            try
            {
                ForceData force = area.GetForce();
                return force != null ? force.forceID.ToString() : area.belongForceID.ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  AreaData查询 GetForceID 失败: {e.Message}");
                return "-1";
            }
        }

        private static string CompositeGetForceName(PlotController plotController, AreaData area, string[] args)
        {
            try
            {
                ForceData force = area.GetForce();
                return force?.forceName ?? "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  AreaData查询 GetForceName 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetBranchLeaderID(PlotController plotController, AreaData area, string[] args)
        {
            return area.branchLeaderID.ToString();
        }

        private static string CompositeGetBranchLeaderName(PlotController plotController, AreaData area, string[] args)
        {
            if (area.branchLeaderID < 0)
                return "";

            HeroData hero = CommonHandlers.GetWorldData()?.GetHero(area.branchLeaderID);
            return hero?.heroName ?? "";
        }

        private static string CompositeGetInsideHeroID(PlotController plotController, AreaData area, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "AreaData查询 GetInsideHeroID", "索引", out int index))
                return "-1";

            if (area.insideHeros == null || index < 0 || index >= area.insideHeros.Count)
                return "-1";

            return area.insideHeros[index].ToString();
        }

        private static string CompositeGetInsideHeroName(PlotController plotController, AreaData area, string[] args)
        {
            string heroID = CompositeGetInsideHeroID(plotController, area, args);
            if (!int.TryParse(heroID, out int id) || id < 0)
                return "";

            HeroData hero = CommonHandlers.GetWorldData()?.GetHero(id);
            return hero?.heroName ?? "";
        }

        private static string CompositeHaveInsideHero(PlotController plotController, AreaData area, string[] args)
        {
            if (!CommonHandlers.TryGetHeroIdArg(plotController, args, 0, "AreaData查询 HaveInsideHero", out int heroID))
                return "0";

            return area.insideHeros != null && area.insideHeros.Contains(heroID) ? "1" : "0";
        }

        private static string CompositeGetAreaState(PlotController plotController, AreaData area, string[] args)
        {
            if (!TryGetAreaStateArg(args, 0, "GetAreaState", out int areaStateType))
                return "";

            try { return area.GetAreaState(areaStateType).ToString("G"); }
            catch (Exception e)
            {
                LoggerManager.Error($"  AreaData查询 GetAreaState 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetAreaStatePercent(PlotController plotController, AreaData area, string[] args)
        {
            if (!TryGetAreaStateArg(args, 0, "GetAreaStatePercent", out int areaStateType))
                return "";

            try { return area.GetAreaStatePercent(areaStateType).ToString("G"); }
            catch (Exception e)
            {
                LoggerManager.Error($"  AreaData查询 GetAreaStatePercent 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetMaxAreaState(PlotController plotController, AreaData area, string[] args)
        {
            if (!TryGetAreaStateArg(args, 0, "GetMaxAreaState", out int areaStateType))
                return "";

            try { return area.GetMaxAreaState(areaStateType).ToString("G"); }
            catch (Exception e)
            {
                LoggerManager.Error($"  AreaData查询 GetMaxAreaState 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetChangeAreaState(PlotController plotController, AreaData area, string[] args)
        {
            if (!TryGetAreaStateArg(args, 0, "GetChangeAreaState", out int areaStateType))
                return "";

            try { return area.GetChangeAreaState(areaStateType).ToString("G"); }
            catch (Exception e)
            {
                LoggerManager.Error($"  AreaData查询 GetChangeAreaState 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetResourceValueRate(PlotController plotController, AreaData area, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "AreaData查询 GetResourceValueRate", "资源ID", out int resourceID))
                return "";

            string mode = args.Length > 1 ? args[1]?.Trim().ToLowerInvariant() : "base";
            try
            {
                if (mode == "temp" || mode == "临时")
                {
                    if (area.resourceValueRateTemp == null || resourceID < 0 || resourceID >= area.resourceValueRateTemp.Count)
                        return "";
                    return area.resourceValueRateTemp[resourceID].ToString("G");
                }

                if (area.resourceValueRateBase == null || resourceID < 0 || resourceID >= area.resourceValueRateBase.Count)
                    return "";
                return area.resourceValueRateBase[resourceID].ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  AreaData查询 GetResourceValueRate 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetChangeResource(PlotController plotController, AreaData area, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "AreaData查询 GetChangeResource", "资源ID", out int resourceID))
                return "";

            if (area.changeResource == null || resourceID < 0 || resourceID >= area.changeResource.Count)
                return "";

            return area.changeResource[resourceID].ToString("G");
        }

        private static string CompositeFindBuildingID(PlotController plotController, AreaData area, string[] args)
        {
            AreaBuildingData building = ResolveBuilding(area, args, "FindBuildingID");
            return building != null ? building.buildingID.ToString() : "-1";
        }

        private static string CompositeFindBuildingName(PlotController plotController, AreaData area, string[] args)
        {
            AreaBuildingData building = ResolveBuilding(area, args, "FindBuildingName");
            if (building == null)
                return "";

            try
            {
                AreaBuildingDataBase data = building.DataBase();
                return data?.name ?? building.buildingID.ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  AreaData查询 FindBuildingName 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetCenterBuildingID(PlotController plotController, AreaData area, string[] args)
        {
            try
            {
                AreaBuildingData building = area.GetCenterBuilding();
                return building != null ? building.buildingID.ToString() : "-1";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  AreaData查询 GetCenterBuildingID 失败: {e.Message}");
                return "-1";
            }
        }

        private static string CompositeGetTileBuildingID(PlotController plotController, AreaData area, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "AreaData查询 GetTileBuildingID", "列", out int column) ||
                !CommonHandlers.TryGetIntArg(args, 1, "AreaData查询 GetTileBuildingID", "行", out int row))
            {
                return "-1";
            }

            try
            {
                AreaTileData tile = area.GetTile(column, row);
                return tile?.building != null ? tile.building.buildingID.ToString() : "-1";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  AreaData查询 GetTileBuildingID 失败: {e.Message}");
                return "-1";
            }
        }

        private static string CompositeCount(PlotController plotController, AreaData area, string[] args)
        {
            if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                LoggerManager.Warning("  AreaData查询 Count: 参数不足，格式[Count=属性/方法名]");
                return "-1";
            }

            return ConditionQueryHandlers.GenericCount(area, "AreaData", args[0], BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase);
        }

        private static string CompositeValue(PlotController plotController, AreaData area, string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || !int.TryParse(args[1], out int index))
            {
                LoggerManager.Warning("  AreaData查询 Value: 参数不足，格式[Value=属性/方法名-索引]");
                return "";
            }

            return ConditionQueryHandlers.GenericValue(area, "AreaData", args[0], index, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase);
        }

        private static string CompositeIndex(PlotController plotController, AreaData area, string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]))
            {
                LoggerManager.Warning("  AreaData查询 Index: 参数不足，格式[Index=属性/方法名-查找值]");
                return "-1";
            }

            return ConditionQueryHandlers.GenericIndex(area, "AreaData", args[0], args[1], BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase);
        }

        private static bool TryGetAreaStateArg(string[] args, int index, string actionName, out int areaStateType)
        {
            areaStateType = -1;
            if (args == null || args.Length <= index || string.IsNullOrWhiteSpace(args[index]))
            {
                LoggerManager.Warning($"  AreaData查询 {actionName}: 缺少区域状态参数");
                return false;
            }

            if (TryParseAreaStateType(args[index], out areaStateType))
                return true;

            LoggerManager.Warning($"  AreaData查询 {actionName}: 未知区域状态 \"{args[index]}\"");
            return false;
        }

        private static bool TryParseAreaStateType(string raw, out int areaStateType)
        {
            areaStateType = -1;
            if (int.TryParse(raw, out int value) && value >= 0 && value <= 3)
            {
                areaStateType = value;
                return true;
            }

            string lower = raw.Trim().ToLowerInvariant();
            switch (lower)
            {
                case "safe":
                case "治安":
                    areaStateType = 0;
                    return true;
                case "support":
                case "民心":
                    areaStateType = 1;
                    return true;
                case "defence":
                case "defense":
                case "防御":
                    areaStateType = 2;
                    return true;
                case "people":
                case "population":
                case "人口":
                    areaStateType = 3;
                    return true;
                default:
                    return false;
            }
        }

        private static AreaBuildingData ResolveBuilding(AreaData area, string[] args, string actionName)
        {
            if (args == null || args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                LoggerManager.Warning($"  AreaData查询 {actionName}: 缺少建筑ID或名称参数");
                return null;
            }

            try
            {
                if (int.TryParse(args[0], out int buildingID))
                    return area.FindBuilding(buildingID);

                return area.FindBuilding(args[0]);
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  AreaData查询 {actionName} 失败: {e.Message}");
                return null;
            }
        }
    }
}
