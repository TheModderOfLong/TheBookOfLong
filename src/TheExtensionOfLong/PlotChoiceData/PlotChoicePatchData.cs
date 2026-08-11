using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 单个选项补丁定义（对应JSON数组中的一个元素）
    /// </summary>
    public class PlotChoicePatchData
    {
        /// <summary>需要补丁的 PlotController 方法名</summary>
        public string patchFunction;

        /// <summary>条件表达式（使用 ConditionExpressionEvaluator 语法）</summary>
        public string conditionGroup;

        /// <summary>插入位置索引（null=最后一个）</summary>
        public int? insertPos;

        /// <summary>插入类型：0=Overwrite, 1=Before, 2=After</summary>
        [JsonConverter(typeof(InsertTypeJsonConverter))]
        public InsertType insertType;

        /// <summary>覆盖目标选项的描述文本（patchFunction+overwriteChoiceText确定唯一性）</summary>
        public string overwriteChoiceText;

        /// <summary>优先级（数字越大越高）</summary>
        public int priority;

        /// <summary>选项数据（null=删除该选项）</summary>
        [JsonProperty("ChoiceData")]
        public PlotChoiceDataModel ChoiceData;
    }

    /// <summary>
    /// 插入类型枚举
    /// </summary>
    public enum InsertType
    {
        Overwrite = 0,
        Before = 1,
        After = 2
    }

    /// <summary>
    /// InsertType 的 JSON 反序列化转换器
    /// 支持 { "name": "Before", "value": 1 } 对象格式和整数格式
    /// </summary>
    public class InsertTypeJsonConverter : JsonConverter<InsertType>
    {
        public override InsertType ReadJson(JsonReader reader, Type objectType, InsertType existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Integer)
            {
                return (InsertType)(long)reader.Value;
            }

            if (reader.TokenType == JsonToken.String)
            {
                string str = (string)reader.Value;
                if (Enum.TryParse(str, true, out InsertType result))
                    return result;
                if (int.TryParse(str, out int val))
                    return (InsertType)val;
            }

            if (reader.TokenType == JsonToken.StartObject)
            {
                var obj = serializer.Deserialize<InsertTypeObject>(reader);
                if (obj != null)
                {
                    if (obj.value != 0 || !string.IsNullOrEmpty(obj.name))
                        return (InsertType)obj.value;
                }
            }

            return InsertType.Before; // 默认
        }

        public override void WriteJson(JsonWriter writer, InsertType value, JsonSerializer serializer)
        {
            writer.WriteValue((int)value);
        }

        private class InsertTypeObject
        {
            public string name;
            public int value;
        }
    }

    /// <summary>
    /// 对应 SinglePlotChoiceData 的可序列化模型
    /// </summary>
    public class PlotChoiceDataModel
    {
        public string choiceText;
        public string callFuc;
        public string callParam;
        public bool inited;
        public bool inheritMissionRequirement;

        [JsonConverter(typeof(RequirementsJsonConverter))]
        public List<PlotChoiceRequirementModel> requirements;

        [JsonConverter(typeof(RelationsJsonConverter))]
        public List<int> relations;

        public bool autoChangeCostByDifficulty;

        public List<ResourceDataModel> costResource;
        public string describe;
        public bool destroyEvent;

        [JsonConverter(typeof(PlayerInteractionTimeTypeJsonConverter))]
        public PlayerInteractionTimeTypeModel playerInteractionTimeNeed;
    }

    /// <summary>
    /// 选项条件门槛模型
    /// </summary>
    public class PlotChoiceRequirementModel
    {
        /// <summary>
        /// 条件类型，支持两种 JSON 格式：
        /// 整数格式：{ "requireType": 25, ... }
        /// 对象格式：{ "requireType": { "name": "FavorDegree", "value": 0 }, ... }
        /// </summary>
        [JsonConverter(typeof(ChoiceRequirementTypeJsonConverter))]
        public ChoiceRequirementTypeModel requireType;

        public float requireNum;
        public bool autoChangeReuqireByDifficulty;
    }

    /// <summary>
    /// ChoiceRequirementType 模型
    /// </summary>
    public class ChoiceRequirementTypeModel
    {
        public string name;
        public int value;
    }

    /// <summary>
    /// 资源数据模型
    /// </summary>
    public class ResourceDataModel
    {
        public int resourceType;
        public float resourceNum;
    }

    /// <summary>
    /// PlayerInteractionTimeType 模型
    /// </summary>
    public class PlayerInteractionTimeTypeModel
    {
        public string name;
        public int value;
    }

    #region JSON 转换器

    /// <summary>
    /// requirements 字段的转换器：支持数组和null
    /// </summary>
    public class RequirementsJsonConverter : JsonConverter<List<PlotChoiceRequirementModel>>
    {
        public override List<PlotChoiceRequirementModel> ReadJson(JsonReader reader, Type objectType, List<PlotChoiceRequirementModel> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return new List<PlotChoiceRequirementModel>();
            return serializer.Deserialize<List<PlotChoiceRequirementModel>>(reader) ?? new List<PlotChoiceRequirementModel>();
        }

        public override void WriteJson(JsonWriter writer, List<PlotChoiceRequirementModel> value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }

    /// <summary>
    /// relations 字段的转换器：支持数组和null
    /// </summary>
    public class RelationsJsonConverter : JsonConverter<List<int>>
    {
        public override List<int> ReadJson(JsonReader reader, Type objectType, List<int> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return new List<int>();
            return serializer.Deserialize<List<int>>(reader) ?? new List<int>();
        }

        public override void WriteJson(JsonWriter writer, List<int> value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }

    /// <summary>
    /// ChoiceRequirementType 转换器：支持整数格式和对象格式
    /// 整数格式：{ "requireType": 25, ... }
    /// 对象格式：{ "requireType": { "name": "FavorDegree", "value": 0 }, ... }
    /// </summary>
    public class ChoiceRequirementTypeJsonConverter : JsonConverter<ChoiceRequirementTypeModel>
    {
        public override ChoiceRequirementTypeModel ReadJson(JsonReader reader, Type objectType, ChoiceRequirementTypeModel existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Integer)
            {
                return new ChoiceRequirementTypeModel { name = "", value = (int)(long)reader.Value };
            }

            if (reader.TokenType == JsonToken.StartObject)
            {
                return serializer.Deserialize<ChoiceRequirementTypeModel>(reader) ?? new ChoiceRequirementTypeModel();
            }

            return new ChoiceRequirementTypeModel();
        }

        public override void WriteJson(JsonWriter writer, ChoiceRequirementTypeModel value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }

    /// <summary>
    /// PlayerInteractionTimeType 转换器：支持整数格式和对象格式
    /// 整数格式：{ "playerInteractionTimeNeed": 1 }
    /// 对象格式：{ "playerInteractionTimeNeed": { "name": "ChatTime", "value": 1 } }
    /// </summary>
    public class PlayerInteractionTimeTypeJsonConverter : JsonConverter<PlayerInteractionTimeTypeModel>
    {
        public override PlayerInteractionTimeTypeModel ReadJson(JsonReader reader, Type objectType, PlayerInteractionTimeTypeModel existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            if (reader.TokenType == JsonToken.Integer)
            {
                return new PlayerInteractionTimeTypeModel { name = "", value = (int)(long)reader.Value };
            }

            if (reader.TokenType == JsonToken.StartObject)
            {
                return serializer.Deserialize<PlayerInteractionTimeTypeModel>(reader);
            }

            return null;
        }

        public override void WriteJson(JsonWriter writer, PlayerInteractionTimeTypeModel value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }

    #endregion
}
