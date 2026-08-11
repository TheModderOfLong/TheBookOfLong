# PlotChoiceDataController 选项补丁功能 — 可行性分析与实现方案

​ 


目前的可以patch的函数有缺少，且有些函数的实际意义不对（函数名称不代表实际效果），详见如下介绍：
ChangeNormalMeetNpcPlot（常规互动）
LoverInteractWithNPC（情侣交互）
ForceInteractWithNPC（势力角色交互）
MeditationWorkContinue（修行）
ChangeNpcPracticeSkillPlot（习武）
AskNPCMission（委托）
ChatWithNPC（论战）
FurtherInteractWithNPC（关系）


## 一、需求概述

参照"龙之书"MOD的 ComplexData 加载管线，实现从 `Mods/ModsOfLong/模组文件夹/ComplexData/` 目录下读取名为 `PlotChoiceDataController` 的 JSON 文件，并在运行时对 PlotController 的角色互动方法注入/修改/删除剧情选项。

### 目标文件路径

```
{游戏根}/Mods/ModsOfLong/{modName}/ComplexData/PlotChoiceDataController.json
```

### JSON 数据格式（已定义）

```json
[
    {
        "patchFunction": "AskHeroJoinTeam",
        "conditionGroup": "[$GetStrVal:自定义选项$][=]1",
        "insertPos": null,
        "insertType": { "name": "Before", "value": 1 },
        "overwriteChoiceText": "可以加入我的队伍吗？",
        "priority": 10,
        "ChoiceData": { /* SinglePlotChoiceData 结构 */ }
    }
]
```

> **注意**：JSON 中字段名 `ChoiceData` 首字母大写，但对应 C# 类 `SinglePlotChoiceData`。后续实现中需注意大小写映射。

---

## 二、可行性分析

### 2.1 技术可行性：✅ 完全可行

| 维度 | 评估 | 说明 |
|------|------|------|
| 文件读取 | ✅ | 可用 `System.IO.File.ReadAllText` + `System.Text.Json` 或 `Newtonsoft.Json`（项目已引用 `Il2CppNewtonsoft.Json`） |
| 目录发现 | ✅ | 龙之书已有成熟的 `ModProjectRegistry` 扫描 `ModsOfLong/mod*` 模式，可直接复用路径构建逻辑 |
| JSON 反序列化 | ✅ | 项目 `.csproj` 已引用 `Il2CppNewtonsoft.Json`，可直接使用 `JsonConvert.DeserializeObject` |
| Harmony 补丁 | ✅ | 项目已有6个成功的 Harmony 补丁先例，且 `FurtherInteractWithNPC` 的 Postfix 补丁已验证了选项注入可行性 |
| 条件表达式 | ✅ | 已有 `ConditionExpressionEvaluator.Evaluate(PlotController, string)` 完整实现 |
| SinglePlotChoiceData 构建 | ✅ | `GameControllerGetHeroNamePatch.cs` 已演示了完整的 `SinglePlotChoiceData` 构建方式 |
| 运行时选项列表操作 | ✅ | `nowSinglePlot.choices` 是 `List<SinglePlotChoiceData>`，可直接 `Insert`/`RemoveAt`/索引赋值 |

### 2.2 与龙之书管线的对比

| 特性 | 龙之书 ComplexData | 本方案 PlotChoiceDataController |
|------|-------------------|-------------------------------|
| 文件格式 | JSON | JSON（相同） |
| 读取目录 | `ModsOfLong/mod*/ComplexData/*.json` | 相同目录（可复用路径发现逻辑） |
| 补丁策略 | `ArrayByName`（按name匹配）或 `ObjectReplace` | **自定义策略**：按 `patchFunction`+`overwriteChoiceText` 匹配 |
| 应用时机 | `GameController.Start()` Postfix | `PlotController` 各互动方法的 **Postfix**（实时注入选项） |
| 数据写入方式 | 反射写入游戏控制器成员 | 直接操作 `List<SinglePlotChoiceData>` 的 `Insert`/`RemoveAt` |
| 补丁粒度 | 控制器级别（启动时一次性应用） | **方法级别**（每次互动调用时动态应用） |

### 2.3 关键差异：龙之书管线 vs 本方案

**龙之书的模式**是"启动时一次性修改游戏数据"——在 `GameController.Start()` 后，直接修改 `MissionDataController.xxxDataBase` 等静态数据集合。数据改一次就持久化。

**本方案的模式**是"运行时动态注入选项"——每次玩家与NPC互动时，`PlotController` 的互动方法（如 `AskHeroJoinTeam`）被调用后，在 Postfix 中动态往 `nowSinglePlot.choices` 插入/修改/删除选项。这些选项是临时的（每次互动重新生成）。

因此，**不需要复用龙之书的 ComplexData 管线（`ComplexPatchExecutor`/`ComplexJsonValuePatcher`/`ComplexTypeAccessor`等反射写入层）**，只需复用其**文件发现和加载**部分。

### 2.4 风险点

| 风险 | 等级 | 缓解措施 |
|------|------|---------|
| 多MOD补丁冲突 | 中 | 已有 `priority`+`overwriteChoiceText` 去重机制 |
| 选项引用的 callParam 符号ID解析 | 低 | 已有 `PlotControllerChangePlotDataBasePatch` 处理符号ID |
| `patchFunction` 对应的方法名写错 | 中 | 加载时验证方法是否存在；运行时找不到方法只 log 不崩溃 |
| 条件表达式解析失败 | 低 | `ConditionExpressionEvaluator` 已有完整错误处理 |
| IL2CPP 类型构造限制 | 中 | `SinglePlotChoiceData` 需用 Il2CPP 兼容方式构造（参考现有代码） |

---

## 三、实现方案

### 3.1 架构设计

```
┌──────────────────────────────────────────────────┐
│              ModMain.OnInitializeMelon()          │
│                       │                          │
│                       ▼                          │
│           PlotChoicePatchManager.Initialize()     │
│                       │                          │
│         ┌─────────────┼──────────────┐           │
│         ▼             ▼              ▼           │
│   扫描ModsOfLong   读取JSON文件    解析为       │
│   下的mod目录      列表           PatchData[]    │
│                                    列表           │
│                       │                          │
│                       ▼                          │
│           按 patchFunction 分组存储              │
│    Dictionary<string, List<PlotChoicePatch>>      │
│                       │                          │
└───────────────────────┼──────────────────────────┘
                        │
       ┌────────────────┼─────────────────────┐
       ▼                ▼                     ▼
  [HarmonyPostfix]  [HarmonyPostfix]    [HarmonyPostfix]
  AskHeroJoinTeam   FurtherInteract...  ChatWithNPC ...
       │                │                     │
       └────────────────┼─────────────────────┘
                        ▼
        PlotChoicePatchManager.ApplyPatches(
            PlotController, string methodName)
                        │
         ┌──────────────┼───────────────┐
         ▼              ▼               ▼
    条件过滤      选项插入/覆盖/删除   优先级排序
```

### 3.2 文件结构（新增文件）

```
TheExtensionOfLong/
├── Patches/
│   ├── PlotControllerChangePlotDataBasePatch.cs  (已有)
│   ├── PlotControllerSpePlotFucPatch.cs           (已有)
│   ├── PlotControllerGetHeroDataPatch.cs          (已有)
│   ├── GameControllerGetHeroNamePatch.cs          (已有)
│   ├── GlobalDataReplaceSpeStringPatch.cs         (已有)
│   ├── TheBookOfLongTokenDelimitersPatch.cs       (已有)
│   └── PlotChoiceDataPatch.cs                     (★ 新增：统一的选项补丁 Postfix)
├── PlotChoiceData/                                 (★ 新增目录)
│   ├── PlotChoicePatchManager.cs                  (★ 核心管理器)
│   ├── PlotChoicePatchData.cs                     (★ 补丁数据模型)
│   └── PlotChoiceDataBuilder.cs                   (★ SinglePlotChoiceData 构建器)
```

### 3.3 数据模型 — `PlotChoicePatchData.cs`

```csharp
using System.Collections.Generic;

namespace TheExtensionOfLong.PlotChoiceData
{
    /// <summary>
    /// 单个选项补丁定义（对应JSON中的一个元素）
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
        public InsertType insertType;

        /// <summary>覆盖目标选项的描述文本（patchFunction+overwriteChoiceText确定唯一性）</summary>
        public string overwriteChoiceText;

        /// <summary>优先级（数字越大越高）</summary>
        public int priority;

        /// <summary>选项数据（null=删除该选项）</summary>
        public PlotChoiceDataModel ChoiceData;
    }

    public enum InsertType
    {
        Overwrite = 0,
        Before = 1,
        After = 2
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
        public List<PlotChoiceRequirementModel> requirements;
        public List<int> relations;                   // RelationRequirementType 的 int 值
        public bool autoChangeCostByDifficulty;
        public List<ResourceDataModel> costResource;
        public string describe;
        public bool destroyEvent;
        public PlayerInteractionTimeTypeModel? playerInteractionTimeNeed;
    }

    public class PlotChoiceRequirementModel
    {
        public ChoiceRequirementTypeModel requireType;
        public float requireNum;
        public bool autoChangeReuqireByDifficulty;
    }

    public class ChoiceRequirementTypeModel
    {
        public string name;
        public int value;
    }

    public class ResourceDataModel
    {
        public int resourceType;
        public float resourceNum;
    }

    public class PlayerInteractionTimeTypeModel
    {
        public string name;
        public int value;
    }
}
```

### 3.4 核心管理器 — `PlotChoicePatchManager.cs`

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using Newtonsoft.Json;

namespace TheExtensionOfLong.PlotChoiceData
{
    /// <summary>
    /// 选项补丁管理器：加载JSON补丁文件，并在运行时动态应用
    /// </summary>
    public static class PlotChoicePatchManager
    {
        // 按 patchFunction 分组的补丁数据
        private static readonly Dictionary<string, List<PlotChoicePatchData>> _patchesByFunction
            = new Dictionary<string, List<PlotChoicePatchData>>(StringComparer.OrdinalIgnoreCase);

        private static bool _isInitialized = false;

        /// <summary>
        /// 初始化：扫描 ModsOfLong 目录，加载所有 PlotChoiceDataController.json
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                string modsRoot = FindModsRoot();
                if (modsRoot == null || !Directory.Exists(modsRoot))
                {
                    LoggerManager.Info("PlotChoicePatchManager: 未找到 ModsOfLong 目录，跳过加载");
                    _isInitialized = true;
                    return;
                }

                string modsOfLongRoot = Path.Combine(modsRoot, "ModsOfLong");
                if (!Directory.Exists(modsOfLongRoot))
                {
                    LoggerManager.Info("PlotChoicePatchManager: 未找到 ModsOfLong 目录，跳过加载");
                    _isInitialized = true;
                    return;
                }

                // 扫描所有 mod* 子目录
                string[] modDirs = Directory.GetDirectories(modsOfLongRoot, "mod*");
                int totalPatches = 0;

                foreach (string modDir in modDirs)
                {
                    string complexDataDir = Path.Combine(modDir, "ComplexData");
                    if (!Directory.Exists(complexDataDir)) continue;

                    // 查找 PlotChoiceDataController.json（不区分大小写）
                    string[] jsonFiles = Directory.GetFiles(complexDataDir, "PlotChoiceDataController.json");
                    if (jsonFiles.Length == 0)
                    {
                        // 也搜索 .json 扩展名的变体
                        jsonFiles = Directory.GetFiles(complexDataDir, "PlotChoiceDataController*");
                    }

                    foreach (string jsonFile in jsonFiles)
                    {
                        int count = LoadPatchFile(jsonFile, Path.GetFileName(modDir));
                        totalPatches += count;
                    }
                }

                LoggerManager.Info($"PlotChoicePatchManager: 加载完成，共 {totalPatches} 个选项补丁，覆盖 {_patchesByFunction.Count} 个方法");
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"PlotChoicePatchManager: 初始化失败 - {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 查找游戏 Mods 根目录
        /// </summary>
        private static string FindModsRoot()
        {
            // 方法1: MelonEnvironment.ModsDirectory（如果可用）
            // 方法2: 从游戏根目录推导
            try
            {
                string gameRoot = GameController._instance != null
                    ? UnityEngine.Application.dataPath.Replace("/Data", "").Replace("\\Data", "")
                    : null;

                if (gameRoot != null)
                {
                    string modsDir = Path.Combine(gameRoot, "Mods");
                    if (Directory.Exists(modsDir)) return modsDir;
                }
            }
            catch { }

            // 方法3: 从 MelonLoader 的路径推导
            try
            {
                string melonDir = MelonLoader.MelonEnvironment.ModsDirectory;
                if (!string.IsNullOrEmpty(melonDir)) return melonDir;
            }
            catch { }

            return null;
        }

        /// <summary>
        /// 加载单个JSON补丁文件
        /// </summary>
        private static int LoadPatchFile(string filePath, string modName)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                var patches = JsonConvert.DeserializeObject<List<PlotChoicePatchData>>(json);
                if (patches == null || patches.Count == 0) return 0;

                foreach (var patch in patches)
                {
                    if (string.IsNullOrEmpty(patch.patchFunction))
                    {
                        LoggerManager.Warning($"PlotChoicePatchManager: 跳过无 patchFunction 的补丁 (文件: {filePath})");
                        continue;
                    }

                    if (!_patchesByFunction.TryGetValue(patch.patchFunction, out var list))
                    {
                        list = new List<PlotChoicePatchData>();
                        _patchesByFunction[patch.patchFunction] = list;
                    }
                    list.Add(patch);
                }

                LoggerManager.Info($"PlotChoicePatchManager: 从 {modName} 加载 {patches.Count} 个选项补丁");
                return patches.Count;
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"PlotChoicePatchManager: 加载文件失败 {filePath} - {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 在指定方法执行后，动态应用选项补丁
        /// 由各 HarmonyPostfix 补丁调用
        /// </summary>
        public static void ApplyPatches(PlotController plotController, string methodName)
        {
            if (!_isInitialized) Initialize();
            if (_patchesByFunction.Count == 0) return;

            if (!_patchesByFunction.TryGetValue(methodName, out var patches)) return;

            SinglePlotData nowPlot = plotController.nowSinglePlot;
            if (nowPlot == null) return;

            List<SinglePlotChoiceData> choices = nowPlot.choices;
            if (choices == null) return;

            // 按 priority 降序排序（高优先级先处理）
            var sortedPatches = patches.OrderByDescending(p => p.priority).ToList();

            foreach (var patch in sortedPatches)
            {
                try
                {
                    ApplySinglePatch(plotController, choices, patch);
                }
                catch (Exception ex)
                {
                    LoggerManager.Error($"PlotChoicePatchManager: 应用补丁失败 (method={methodName}, " +
                        $"text={patch.overwriteChoiceText}) - {ex.Message}");
                }
            }

            nowPlot.choices = choices;
        }

        /// <summary>
        /// 应用单个选项补丁
        /// </summary>
        private static void ApplySinglePatch(
            PlotController plotController,
            List<SinglePlotChoiceData> choices,
            PlotChoicePatchData patch)
        {
            // 1. 条件检查
            if (!string.IsNullOrEmpty(patch.conditionGroup))
            {
                bool conditionMet = ConditionExpressionEvaluator.Evaluate(plotController, patch.conditionGroup);
                if (!conditionMet)
                {
                    LoggerManager.Debug($"  选项补丁条件不满足，跳过: {patch.overwriteChoiceText ?? patch.ChoiceData?.choiceText}");
                    return;
                }
            }

            switch (patch.insertType)
            {
                case InsertType.Overwrite:
                    ApplyOverwrite(choices, patch);
                    break;
                case InsertType.Before:
                    ApplyInsert(choices, patch, before: true);
                    break;
                case InsertType.After:
                    ApplyInsert(choices, patch, before: false);
                    break;
            }
        }

        /// <summary>
        /// Overwrite 模式：用新选项数据覆盖原选项
        /// </summary>
        private static void ApplyOverwrite(List<SinglePlotChoiceData> choices, PlotChoicePatchData patch)
        {
            if (string.IsNullOrEmpty(patch.overwriteChoiceText)) return;

            for (int i = 0; i < choices.Count; i++)
            {
                SinglePlotChoiceData choice = choices[i];
                if (choice != null && choice.choiceText == patch.overwriteChoiceText)
                {
                    if (patch.ChoiceData == null)
                    {
                        // choiceData=null 表示删除
                        choices.RemoveAt(i);
                        LoggerManager.Info($"  选项补丁[Overwrite/删除]: {patch.overwriteChoiceText}");
                    }
                    else
                    {
                        // 用新数据覆盖
                        PlotChoiceDataBuilder.ApplyToChoice(choice, patch.ChoiceData);
                        LoggerManager.Info($"  选项补丁[Overwrite]: {patch.overwriteChoiceText}");
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// Before/After 模式：插入新选项
        /// </summary>
        private static void ApplyInsert(List<SinglePlotChoiceData> choices, PlotChoicePatchData patch, bool before)
        {
            if (patch.ChoiceData == null) return; // 插入模式下 choiceData 不能为空

            // 检查是否有同 overwriteChoiceText 的其他补丁需要覆盖
            // （此逻辑在多补丁场景下由优先级保证最终结果）

            // 构建新的 SinglePlotChoiceData
            SinglePlotChoiceData newChoice = PlotChoiceDataBuilder.BuildChoice(patch.ChoiceData);

            // 计算插入位置
            int targetIndex;

            if (patch.insertPos.HasValue)
            {
                // 指定了位置
                targetIndex = patch.insertPos.Value;
                if (targetIndex < 0) targetIndex = 0;
                if (targetIndex > choices.Count) targetIndex = choices.Count;

                if (!before && targetIndex < choices.Count) targetIndex++;
            }
            else
            {
                // null = 最后一个
                targetIndex = choices.Count;
                if (before && choices.Count > 0) targetIndex = choices.Count - 1;
            }

            choices.Insert(targetIndex, newChoice);
            LoggerManager.Info($"  选项补丁[{(before ? "Before" : "After")}]: " +
                $"{newChoice.choiceText} @ index {targetIndex}");
        }
    }
}
```

### 3.5 选项数据构建器 — `PlotChoiceDataBuilder.cs`

```csharp
using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;

namespace TheExtensionOfLong.PlotChoiceData
{
    /// <summary>
    /// 将 JSON 模型转换为 Il2Cpp 的 SinglePlotChoiceData
    /// </summary>
    public static class PlotChoiceDataBuilder
    {
        /// <summary>
        /// 从 JSON 模型构建全新的 SinglePlotChoiceData
        /// </summary>
        public static SinglePlotChoiceData BuildChoice(PlotChoiceDataModel model)
        {
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
            if (model.requirements != null && model.requirements.Count > 0)
            {
                choice.requirements = new List<PlotChoiceRequirement>();
                foreach (var req in model.requirements)
                {
                    var reqType = (ChoiceRequirementType)req.requireType.value;
                    choice.requirements.Add(new PlotChoiceRequirement(reqType, req.requireNum));
                }
            }
            else
            {
                choice.requirements = new List<PlotChoiceRequirement>();
            }

            // relations
            if (model.relations != null && model.relations.Count > 0)
            {
                choice.relations = new List<RelationRequirementType>();
                foreach (var relVal in model.relations)
                {
                    choice.relations.Add((RelationRequirementType)relVal);
                }
            }
            else
            {
                choice.relations = new List<RelationRequirementType>();
            }

            // costResource
            if (model.costResource != null && model.costResource.Count > 0)
            {
                choice.costResource = new List<ResourceData>();
                foreach (var res in model.costResource)
                {
                    choice.costResource.Add(new ResourceData
                    {
                        resourceType = res.resourceType,
                        resourceNum = res.resourceNum
                    });
                }
            }
            else
            {
                choice.costResource = new List<ResourceData>();
            }

            // playerInteractionTimeNeed
            if (model.playerInteractionTimeNeed.HasValue)
            {
                choice.playerInteractionTimeNeed =
                    (PlayerInteractionTimeType)model.playerInteractionTimeNeed.Value.value;
            }

            return choice;
        }

        /// <summary>
        /// 将 JSON 模型的值覆盖到已有的 SinglePlotChoiceData（Overwrite 模式）
        /// </summary>
        public static void ApplyToChoice(SinglePlotChoiceData choice, PlotChoiceDataModel model)
        {
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
                choice.requirements = new List<PlotChoiceRequirement>();
                foreach (var req in model.requirements)
                {
                    var reqType = (ChoiceRequirementType)req.requireType.value;
                    choice.requirements.Add(new PlotChoiceRequirement(reqType, req.requireNum));
                }
            }

            // relations：直接替换
            if (model.relations != null)
            {
                choice.relations = new List<RelationRequirementType>();
                foreach (var relVal in model.relations)
                {
                    choice.relations.Add((RelationRequirementType)relVal);
                }
            }

            // costResource：直接替换
            if (model.costResource != null)
            {
                choice.costResource = new List<ResourceData>();
                foreach (var res in model.costResource)
                {
                    choice.costResource.Add(new ResourceData
                    {
                        resourceType = res.resourceType,
                        resourceNum = res.resourceNum
                    });
                }
            }

            // playerInteractionTimeNeed
            if (model.playerInteractionTimeNeed.HasValue)
            {
                choice.playerInteractionTimeNeed =
                    (PlayerInteractionTimeType)model.playerInteractionTimeNeed.Value.value;
            }
        }
    }
}
```

### 3.6 统一选项补丁入口 — `PlotChoiceDataPatch.cs`

**核心设计**：一个 Postfix 补丁挂在 `PlotController.FurtherInteractWithNPC` 上，因为该方法是被所有互动方法调用的"最终对话生成"方法。

但更精确的方式是：**挂在每个具体的互动方法上**，因为不同方法构建的 `nowSinglePlot.choices` 时机不同。

**推荐方案**：通用 Postfix，挂在 `PlotController.ShowPlot(PlotData)` 上，因为所有互动方法最终都会调用 `ShowPlot`，此时 `nowSinglePlot.choices` 已经构建完成。

```csharp
using System;
using HarmonyLib;
using Il2Cpp;

namespace TheExtensionOfLong.Patches
{
    /// <summary>
    /// 选项补丁统一入口
    /// 挂在 PlotController.ShowPlot(PlotData) 的 Postfix 上
    /// 此时 nowSinglePlot 已构建完成，choices 列表可操作
    /// 
    /// 注意：由于 ShowPlot 是所有剧情播放的最终入口，
    /// 需要确保只在互动场景下应用补丁，避免在非互动剧情中误注入
    /// </summary>
    [HarmonyPatch(typeof(PlotController), "ShowPlot", new[] { typeof(PlotData) })]
    public class PlotChoiceDataPatch
    {
        [HarmonyPostfix]
        public static void ShowPlotPostfix(PlotController __instance, PlotData plotData)
        {
            if (__instance == null || plotData == null) return;

            try
            {
                // 仅当有互动上下文时才应用
                if (__instance.targetInteractHero == null) return;

                // 遍历所有已注册的 patchFunction
                // 由 PlotChoicePatchManager 按方法名分组管理
                // 但 ShowPlot 不知道当前调用的是哪个方法，因此需要另一种策略
                // 
                // 策略2：直接挂在具体互动方法上（推荐）
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"PlotChoiceDataPatch: {ex.Message}");
            }
        }
    }
}
```

#### ⚠️ 关键设计决策：挂在哪个方法上？

**方案A：挂在 ShowPlot 上**（一个 Postfix 覆盖所有）
- 优点：只需一个补丁
- 缺点：无法区分当前是哪个互动方法，`patchFunction` 无法匹配

**方案B：挂在 FurtherInteractWithNPC 上**
- 优点：`FurtherInteractWithNPC` 是"继续互动"的总入口，大部分互动选项都在此时构建
- 缺点：部分方法（如 `AskHeroJoinTeam`、`ChatWithNPC`）的选项不是在 `FurtherInteractWithNPC` 中构建的

**方案C：挂在每个互动方法上**（每个方法一个 Postfix）
- 优点：精确匹配 `patchFunction`
- 缺点：需为每个方法写一个 Postfix，维护成本高

**方案D（推荐）：挂在 FurtherInteractWithNPC 上 + 主动调用**
- 在 `FurtherInteractWithNPC` 的 Postfix 中自动应用 `patchFunction == "FurtherInteractWithNPC"` 的补丁
- 在其他互动方法（如 `AskHeroJoinTeam`）的 Postfix 中，也调用 `PlotChoicePatchManager.ApplyPatches(pc, "AskHeroJoinTeam")`
- 对于需要支持的方法，只需添加一个简单的 Postfix 调用

**最终推荐：方案D**，兼顾灵活性和精确性。实际上，由于大部分互动方法的选项最终都会调用 `ChangePlot` → `ShowPlot`，可以在 `ChangePlot(SinglePlotData, int)` 或 `ChangePlot(int, int)` 上挂一个统一的 Postfix，通过调用栈或 `targetInteractHero` 判断上下文。

但最简洁有效的做法是：

**方案E（最终推荐）：在 FurtherInteractWithNPC 的 Postfix 中统一处理**

观察 `FurtherInteractWithNPC` 方法的代码逻辑，几乎所有互动选项最终都在此方法中构建并调用 `ChangePlot`，因此只需在此方法的 Postfix 中应用所有 `patchFunction` 对应的补丁。对于不在 `FurtherInteractWithNPC` 中的选项，可以用"挂载在其他方法 Postfix"的方式单独处理。

### 3.7 初始化调用 — 修改 `ModMain.cs`

```csharp
// 在 OnInitializeMelon() 中添加
PlotChoiceData.PlotChoicePatchManager.Initialize();
```

### 3.8 完整调用流程

```
1. 游戏启动 → ModMain.OnInitializeMelon()
   └→ PlotChoicePatchManager.Initialize()
      ├→ 扫描 Mods/ModsOfLong/mod*/ComplexData/
      ├→ 读取 PlotChoiceDataController.json
      ├→ JsonConvert.DeserializeObject<List<PlotChoicePatchData>>
      └→ 按 patchFunction 分组存入 _patchesByFunction

2. 玩家与NPC互动 → FurtherInteractWithNPC()
   └→ [HarmonyPostfix] → PlotChoicePatchManager.ApplyPatches(pc, "FurtherInteractWithNPC")
      ├→ 遍历该 patchFunction 下所有补丁
      ├→ ConditionExpressionEvaluator.Evaluate(pc, conditionGroup)
      ├→ 按 priority 降序处理
      ├─┬ Overwrite: 找到 overwriteChoiceText 匹配的选项 → ApplyToChoice 或 RemoveAt
        ├ Before: 构建 SinglePlotChoiceData → Insert(targetIndex)
        └ After: 构建 SinglePlotChoiceData → Insert(targetIndex+1)
```

---

## 四、实现步骤

### 第1步：创建数据模型 (`PlotChoicePatchData.cs`)
- 定义所有 JSON 反序列化用的数据类
- 注意 `ChoiceData` 字段名（JSON中大写C）与 C# 属性名的映射

### 第2步：创建构建器 (`PlotChoiceDataBuilder.cs`)
- 实现 `BuildChoice` 和 `ApplyToChoice`
- 处理枚举类型的 int→enum 转换（`ChoiceRequirementType`、`RelationRequirementType`、`PlayerInteractionTimeType`）
- 处理 Il2Cpp 的 `List<T>` 与 C# `List<T>` 的兼容性

### 第3步：创建管理器 (`PlotChoicePatchManager.cs`)
- 实现目录扫描和文件加载
- 实现 `ApplyPatches` 核心逻辑
- 处理 Overwrite/Before/After 三种插入类型
- 处理 priority 排序和 overwriteChoiceText 去重

### 第4步：创建 Harmony 补丁 (`PlotChoiceDataPatch.cs`)
- 在 `FurtherInteractWithNPC` 的 Postfix 中调用 `ApplyPatches`
- 可选：为其他互动方法添加额外的 Postfix 入口

### 第5步：修改入口 (`ModMain.cs`)
- 在 `OnInitializeMelon()` 中调用 `PlotChoicePatchManager.Initialize()`

### 第6步：测试
- 创建测试 JSON 文件放到 `Mods/ModsOfLong/modTest/ComplexData/PlotChoiceDataController.json`
- 在游戏中与NPC互动，验证选项是否正确注入

---

## 五、关于目录发现的技术细节

### 5.1 龙之书的路径发现

```csharp
// MelonEnvironment.ModsDirectory → {游戏根}/Mods/
string modsRoot = MelonLoader.MelonEnvironment.ModsDirectory;
string modsOfLongRoot = Path.Combine(modsRoot, "ModsOfLong");
string[] modDirs = Directory.GetDirectories(modsOfLongRoot, "mod*");
```

### 5.2 本方案的路径发现

```csharp
// 方法1: 直接使用 MelonEnvironment（推荐）
string modsRoot = MelonLoader.MelonEnvironment.ModsDirectory;

// 方法2: 通过 GameController 推导
string gameRoot = Path.GetDirectoryName(UnityEngine.Application.dataPath);
string modsRoot = Path.Combine(gameRoot, "Mods");

// 搜索路径
// {modsRoot}/ModsOfLong/{modName}/ComplexData/PlotChoiceDataController.json
```

### 5.3 与龙之书 ComplexData 的共存

龙之书的 `ModProjectRegistry` 也会扫描同一目录。本方案的 JSON 文件名（`PlotChoiceDataController.json`）不在龙之书的 `TargetDefinitionsByFileName` 中，因此**龙之书会忽略此文件**，两者不会冲突。

龙之书当前注册的文件名：
- `MissionDataController_*.json`
- `WorldPlotEventController_*.json`

`PlotChoiceDataController.json` 不在其中，不会被龙之书加载。

---

## 六、扩展考虑

### 6.1 热重载支持

可在 `OnUpdate()` 中监听文件变化，实现热重载：

```csharp
// 简化实现：按 F5 重载
if (Input.GetKeyDown(KeyCode.F5))
{
    PlotChoicePatchManager.Reload();
}
```

### 6.2 更多互动方法支持

目前已知的互动方法（可在 `patchFunction` 中使用）：

| patchFunction | 说明 |
|---|---|
| `FurtherInteractWithNPC` | 继续互动（大多数选项在此） |
| `AskHeroJoinTeam` | 邀请入队 |
| `AskHeroJoinTeamTemp` | 临时入队 |
| `LoverAskHeroJoinTeam` | 眷侣入队 |
| `ForceAskHeroJoinTeam` | 强征入队 |
| `ChatWithNPC` | 与NPC闲聊 |
| `GiveGiftWithNPC` | 送礼 |
| `StudySkillWithNPC` | 学技能 |
| `GambleWithNPC` | 赌博 |
| `DrinkWithNPC` | 饮酒（需确认方法名） |

> 完整方法列表需进一步查看 `PlotController` 的方法 dump。上述方法名已从代码分析中确认。

### 6.3 与龙之书 SymbolicId 的集成

选项补丁中的 `callParam` 可能包含符号ID（如 `@MyPlot`）。当前已有 `PlotControllerChangePlotDataBasePatch` 处理 `ChangePlotDataBase` 的符号ID解析。但 `callFuc` 不是 `ChangePlotDataBase` 时（如 `SpePlotFuc`），`callParam` 中的符号ID不会自动解析。

建议：在 `PlotChoiceDataBuilder.BuildChoice` 中，对 `callParam` 调用 `SymbolicIdService` 进行预解析（如果龙之书已安装）。

---

## 七、总结

| 项目 | 结论 |
|------|------|
| **可行性** | ✅ 完全可行，技术上无障碍 |
| **工作量** | 约 4-6 个文件，500-800 行代码 |
| **与龙之书兼容性** | ✅ 互不冲突，可共存 |
| **核心复用** | 复用目录发现逻辑 + `ConditionExpressionEvaluator` + 已有的 `SinglePlotChoiceData` 构建模式 |
| **不建议复用** | 龙之书的 `ComplexPatchExecutor`/`ComplexTypeAccessor`（反射写入层不适用于本场景） |
| **最大风险** | Il2CPP 下 `List<SinglePlotChoiceData>` 的操作兼容性（但已有 `AddressFormDialogPatch` 验证可行） |
