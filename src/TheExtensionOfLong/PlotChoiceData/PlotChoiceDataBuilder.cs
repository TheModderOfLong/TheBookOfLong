using System.Collections.Generic;
using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 将 JSON 模型转换为 Il2Cpp 的 SinglePlotChoiceData
    /// 注意：字符串字段中的 [$查询$] 和 [&算术&] 不会被在此阶段解析，
    /// 而是在实际执行选项时由其他 Patch 负责解析，以确保获取最新的游戏状态
    /// </summary>
    public static class PlotChoiceDataBuilder
    {
        /// <summary>
        /// 从 JSON 模型构建全新的 SinglePlotChoiceData
        /// 字符串字段原样赋值，不做查询指令解析
        /// </summary>
        public static SinglePlotChoiceData BuildChoice(PlotChoiceDataModel model)
        {
            if (model == null) return null;

            var choice = new SinglePlotChoiceData
            {
                choiceText = model.choiceText ?? "",
                callFuc = model.callFuc ?? "",
                callParam = model.callParam ?? "",
                inited = model.inited,
                inheritMissionRequirement = model.inheritMissionRequirement,
                describe = model.describe ?? "",
                destroyEvent = model.destroyEvent,
                autoChangeCostByDifficulty = model.autoChangeCostByDifficulty,
            };

            // requirements
            choice.requirements = BuildRequirements(model.requirements);

            // relations
            choice.relations = BuildRelations(model.relations);

            // costResource
            choice.costResource = BuildCostResources(model.costResource);

            // playerInteractionTimeNeed
            if (model.playerInteractionTimeNeed != null)
            {
                choice.playerInteractionTimeNeed =
                    (PlayerInteractionTimeType)model.playerInteractionTimeNeed.value;
            }

            return choice;
        }

        /// <summary>
        /// 将 JSON 模型的值覆盖到已有的 SinglePlotChoiceData（Overwrite 模式）
        /// 字符串字段原样赋值，不做查询指令解析
        /// </summary>
        public static void ApplyToChoice(SinglePlotChoiceData choice, PlotChoiceDataModel model)
        {
            if (choice == null || model == null) return;

            if (model.choiceText != null) choice.choiceText = model.choiceText;
            if (model.callFuc != null) choice.callFuc = model.callFuc;
            if (model.callParam != null) choice.callParam = model.callParam;
            choice.inited = model.inited;
            choice.inheritMissionRequirement = model.inheritMissionRequirement;
            if (model.describe != null) choice.describe = model.describe;
            choice.destroyEvent = model.destroyEvent;
            choice.autoChangeCostByDifficulty = model.autoChangeCostByDifficulty;

            // requirements：直接替换
            if (model.requirements != null)
            {
                choice.requirements = BuildRequirements(model.requirements);
            }

            // relations：直接替换
            if (model.relations != null)
            {
                choice.relations = BuildRelations(model.relations);
            }

            // costResource：直接替换
            if (model.costResource != null)
            {
                choice.costResource = BuildCostResources(model.costResource);
            }

            // playerInteractionTimeNeed
            if (model.playerInteractionTimeNeed != null)
            {
                choice.playerInteractionTimeNeed =
                    (PlayerInteractionTimeType)model.playerInteractionTimeNeed.value;
            }
        }

        private static Il2CppSystem.Collections.Generic.List<PlotChoiceRequirement> BuildRequirements(List<PlotChoiceRequirementModel> models)
        {
            var list = new Il2CppSystem.Collections.Generic.List<PlotChoiceRequirement>();
            if (models != null)
            {
                foreach (var req in models)
                {
                    var reqType = (ChoiceRequirementType)req.requireType.value;
                    list.Add(new PlotChoiceRequirement(reqType, req.requireNum));
                }
            }
            return list;
        }

        private static Il2CppSystem.Collections.Generic.List<RelationRequirementType> BuildRelations(List<int> values)
        {
            var list = new Il2CppSystem.Collections.Generic.List<RelationRequirementType>();
            if (values != null)
            {
                foreach (var relVal in values)
                {
                    list.Add((RelationRequirementType)relVal);
                }
            }
            return list;
        }

        private static Il2CppSystem.Collections.Generic.List<ResourceData> BuildCostResources(List<ResourceDataModel> models)
        {
            var list = new Il2CppSystem.Collections.Generic.List<ResourceData>();
            if (models != null)
            {
                foreach (var res in models)
                {
                    list.Add(new ResourceData(res.resourceType, res.resourceNum));
                }
            }
            return list;
        }

    }
}
