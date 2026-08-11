# CheckConditionCallFuc 指令实现方案

## 一、需求概述

新增指令 `CheckConditionCallFuc`，根据条件表达式的真假结果，通过 `SendMessage` 执行 PlotController 上对应的方法。

### 核心用法

```
; 基本格式
CheckConditionCallFuc*条件表达式#TrueCallFucName#TrueCallFucParam(可选)#FalseCallFucName(可选)#FalseCallFucParam(可选)

; 参数位置说明（SpePlotFucPrefix 按 # 分隔后的 fucParams）:
; fucParams[0] = 条件表达式
; fucParams[1] = TrueCallFucName   (SendMessage的方法名，如 "SpePlotFuc")
; fucParams[2] = TrueCallFucParam  (SendMessage的参数，需用{{}}保护含#的值)
; fucParams[3] = FalseCallFucName  (可选)
; fucParams[4] = FalseCallFucParam (可选，需用{{}}保护含#的值)

; 示例：CallFucParam 含 # 时必须用 {{}} 包裹保护
CheckConditionCallFuc*[$HeroData:Favor:小白$][>=]50#SpePlotFuc#{{SetStringValue*好感达标#1}}#SpePlotFuc#{{SetStringValue*好感不足#0}}

; CallFucParam 不含 # 时，{{}} 可选
CheckConditionCallFuc*[$HeroData:Favor:小白$][>=]50#SpePlotFuc#GenerateRandomItem*plotInteractItem

; 仅TrueCallFuc（条件为假不做任何事）
CheckConditionCallFuc*[$HeroData:Favor:小白$][>=]50#SpePlotFuc#{{SetStringValue*达标#1}}

; 调用非SpePlotFuc方法（如ChangePlotDataBase，参数不含#可省略{{}}）
CheckConditionCallFuc*[$HeroData:isFemale$][=]1#ChangePlotDataBase#女线剧情#ChangePlotDataBase#男线剧情

; 调用无参数的方法
CheckConditionCallFuc*[$WorldData:day$][>]100#SomeMethod
```

---

## 二、可行性评估

### ✅ 技术可行

| 评估项 | 结论 | 依据 |
|--------|------|------|
| 条件求值 | ✅ 可行 | 复用 `ConditionExpressionEvaluator.Evaluate()`，与 `CheckConditionChangePlotDataBase` 完全一致 |
| CallFuc执行 | ✅ 可行 | 通过 `__instance.gameObject.SendMessage(fucName, fucParam)` 执行，与 `ChangeNextPlot` 的执行方式一致 |
| 参数分隔 | ✅ 可行 | 用 `#` 做位置分隔，fucParams[1~4] 分别对应方法名和参数 |
| `#` 冲突保护 | ✅ 可行 | CallFucParam 含 `#` 时用 `{{}}` 包裹，SpePlotFucPrefix 解析时跳过 `{{}}` 内的 `#`；不含 `#` 时 `{{}}` 可选 |

### ⚠️ 注意事项

| 风险点 | 分析 | 应对 |
|--------|------|------|
| CallFucParam 含 `#` | `#` 是 SpePlotFucPrefix 的参数分隔符，会被提前拆分 | 用户需用 `{{}}` 包裹含 `#` 的 CallFucParam，如 `{{SetStringValue*key#value}}` |
| 空值处理 | FalseCallFucName/Param 可选，为空时条件为假不做任何事 | 合理默认行为 |
| 嵌套 CheckConditionCallFuc | 理论上支持（SpePlotFuc 调用会递归进入 Patch），但容易混乱 | 不禁止，但文档建议避免深层嵌套 |

### 🔑 关键设计决策

#### 决策1：执行方式 —— SendMessage

**使用 `__instance.gameObject.SendMessage(fucName, fucParam)`**

理由：
- 游戏本体中 `ChangeNextPlot` 就是通过 `SendMessage(clickCallFuc, fucParam)` 调用 PlotController 上的方法
- `SpePlotFuc` 只是 PlotController 上的方法之一，用 SendMessage 可以调用任意方法
- CallFucName 对应 SendMessage 的方法名（如 `"SpePlotFuc"`、`"ChangePlotDataBase"`）
- CallFucParam 对应 SendMessage 的参数（如 `"SetStringValue*达标#1"`、`"123"`）
- 注意：**不拆分 `*`**，整个 CallFucParam 作为 SendMessage 的参数传递

SendMessage 执行 SpePlotFuc 时的调用链：
```
SendMessage("SpePlotFuc", "GenerateRandomItem*plotInteractItem")
  → PlotController.SpePlotFuc("GenerateRandomItem*plotInteractItem")
    → SpePlotFucPrefix 拦截 → 按 * 和 # 拆分 fucParams → 匹配自定义指令 → 执行
```

#### 决策2：CallFucParam 用 `{{}}` 保护 `#`

CallFucParam 中常含 `#`（如 `SetStringValue*key#value`），而 `#` 是 SpePlotFucPrefix 的参数分隔符。

**不用 `{{}}` 保护的错误示例**：
```
CheckConditionCallFuc*expr#SpePlotFuc#SetStringValue*好感达标#1#SpePlotFuc#SetStringValue*好感不足#0
```
SpePlotFucPrefix 按 `#` 分隔后：
```
fucParams[0] = "expr"
fucParams[1] = "SpePlotFuc"
fucParams[2] = "SetStringValue*好感达标"     ← #1 被拆开了！
fucParams[3] = "1"                            ← 本应是参数的一部分
fucParams[4] = "SpePlotFuc"
fucParams[5] = "SetStringValue*好感不足"
fucParams[6] = "0"
```

**用 `{{}}` 保护的正确示例**：
```
CheckConditionCallFuc*expr#SpePlotFuc#{{SetStringValue*好感达标#1}}#SpePlotFuc#{{SetStringValue*好感不足#0}}
```
SpePlotFucPrefix 按 `#` 分隔时跳过 `{{}}` 内的 `#`：
```
fucParams[0] = "expr"
fucParams[1] = "SpePlotFuc"
fucParams[2] = "{{SetStringValue*好感达标#1}}"  ← 完整保留
fucParams[3] = "SpePlotFuc"
fucParams[4] = "{{SetStringValue*好感不足#0}}"  ← 完整保留
```
然后 `StripBraces` 剥离 `{{}}` 后得到实际参数。

**规则**：CallFucParam 含 `#` → 必须用 `{{}}` 包裹；不含 `#` → `{{}}` 可选。

---

## 三、实现方案

### 3.1 数据流

```
CheckConditionCallFuc*[$HeroData:Favor:小白$][>=]50#SpePlotFuc#{{SetStringValue*好感达标#1}}#SpePlotFuc#{{SetStringValue*好感不足#0}}
    │
    ├─ SpePlotFucPrefix 解析参数（按 # 分隔，使用 SplitRespectingBraces 跳过{{}}内的#）:
    │   fucParams[0] = "[$HeroData:Favor:小白$][>=]50"        ← 条件表达式
    │   fucParams[1] = "SpePlotFuc"                           ← TrueCallFucName
    │   fucParams[2] = "{{SetStringValue*好感达标#1}}"         ← TrueCallFucParam（含{{}}保护）
    │   fucParams[3] = "SpePlotFuc"                           ← FalseCallFucName
    │   fucParams[4] = "{{SetStringValue*好感不足#0}}"         ← FalseCallFucParam（含{{}}保护）
    │
    ├─ 条件求值: ConditionExpressionEvaluator.Evaluate(__instance, expression)
    │   → [$HeroData:Favor:小白$] 替换为 "60"
    │   → "60[>=]50" → true
    │
    ├─ 选择 True 分支:
    │   callFucName = "SpePlotFuc"
    │   callFucParam = StripBraces("{{SetStringValue*好感达标#1}}") = "SetStringValue*好感达标#1"
    │
    └─ 执行: __instance.gameObject.SendMessage("SpePlotFuc", "SetStringValue*好感达标#1")
         → PlotController.SpePlotFuc("SetStringValue*好感达标#1")
           → SpePlotFucPrefix 再次拦截
             → fucName = "SetStringValue", fucParams = ["好感达标", "1"]
             → 匹配 SetStringValue → 执行设置
```

### 3.2 代码改动

#### 改动1：`PlotControllerSpePlotFucPatch.cs` — SpePlotFucPrefix 优化

**问题**：原 `paramPart.Split('#')` 会暴力拆分所有 `#`，包括 `{{}}` 内的 `#`，导致 `{{SetStringValue*key#value}}` 被错误拆分。

**修复**：将 `Split('#')` 替换为 `SplitRespectingBraces(paramPart, '#')`，跳过 `{{}}` 内的 `#`。

```csharp
// 旧代码
string[] fucParams = string.IsNullOrEmpty(paramPart) ? new string[0] : paramPart.Split('#');

// 新代码
string[] fucParams = string.IsNullOrEmpty(paramPart) ? new string[0] : SplitRespectingBraces(paramPart, '#');
```

**新增工具方法**：

```csharp
/// <summary>
/// 按指定分隔符拆分字符串，但跳过 {{...}} 内的分隔符
/// </summary>
private static string[] SplitRespectingBraces(string input, char separator)
{
    var parts = new System.Collections.Generic.List<string>();
    int depth = 0;
    int start = 0;

    for (int i = 0; i < input.Length; i++)
    {
        if (i + 1 < input.Length && input[i] == '{' && input[i + 1] == '{')
        {
            depth++;
            i++; // 跳过第二个 {
        }
        else if (i + 1 < input.Length && input[i] == '}' && input[i + 1] == '}')
        {
            depth--;
            i++; // 跳过第二个 }
        }
        else if (input[i] == separator && depth == 0)
        {
            parts.Add(input.Substring(start, i - start));
            start = i + 1;
        }
    }

    parts.Add(input.Substring(start));
    return parts.ToArray();
}

/// <summary>
/// 剥离外层 {{...}} 包裹（如果存在）
/// </summary>
private static string StripBraces(string input)
{
    if (string.IsNullOrEmpty(input)) return input;
    if (input.StartsWith("{{") && input.EndsWith("}}") && input.Length > 4)
    {
        return input.Substring(2, input.Length - 4);
    }
    return input;
}
```

#### 改动2：`PlotControllerSpePlotFucPatch.cs` — 新增指令

**FucHandlers 注册**：
```csharp
{ "CheckConditionCallFuc", TryCallFucCheckConditionCallFuc },
```

**TryCallFucCheckConditionCallFuc 方法**：
```csharp
/// <summary>
/// 根据条件表达式结果执行对应的函数调用
/// 格式: CheckConditionCallFuc*条件表达式#TrueCallFucName#TrueCallFucParam(可选)#FalseCallFucName(可选)#FalseCallFucParam(可选)
///   CallFucParam 含 # 时必须用 {{}} 包裹，如 {{SetStringValue*key#value}}
///   通过 SendMessage 调用 PlotController 上的方法，可执行任意方法（SpePlotFuc、ChangePlotDataBase 等）
/// 示例:
///   CheckConditionCallFuc*[$HeroData:Favor:小白$][>=]50#SpePlotFuc#{{SetStringValue*达标#1}}
///   CheckConditionCallFuc*[$HeroData:Favor:小白$][>=]50#SpePlotFuc#{{SetStringValue*达标#1}}#SpePlotFuc#{{SetStringValue*不足#0}}
///   CheckConditionCallFuc*[$HeroData:isFemale$][=]1#ChangePlotDataBase#女线剧情#ChangePlotDataBase#男线剧情
/// </summary>
private static void TryCallFucCheckConditionCallFuc(PlotController __instance, string fucName, string[] fucParams)
{
    if (fucParams.Length < 2 || string.IsNullOrWhiteSpace(fucParams[1]))
    {
        LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*条件表达式#TrueCallFucName#TrueCallFucParam(可选)#FalseCallFucName(可选)#FalseCallFucParam(可选)]");
        return;
    }

    string expression = fucParams[0];

    // 条件求值
    bool result = ConditionExpressionEvaluator.Evaluate(__instance, expression);
    LoggerManager.Debug($"{fucName}: 条件求值结果={result}, 表达式={expression}");

    // 根据条件结果选择分支
    string callFucName;
    string callFucParam = "";

    if (result)
    {
        callFucName = fucParams[1];
        callFucParam = fucParams.Length > 2 ? StripBraces(fucParams[2]) : "";
    }
    else
    {
        // FalseCallFucName 在 fucParams[3]，可选
        if (fucParams.Length < 4 || string.IsNullOrWhiteSpace(fucParams[3]))
        {
            LoggerManager.Debug($"{fucName}: 条件=false, 无FalseCallFuc可执行");
            return;
        }
        callFucName = fucParams[3];
        callFucParam = fucParams.Length > 4 ? StripBraces(fucParams[4]) : "";
    }

    LoggerManager.Debug($"{fucName}: 执行CallFuc: SendMessage(\"{callFucName}\", \"{callFucParam}\")");

    // 通过 SendMessage 执行，与 ChangeNextPlot 方式一致
    __instance.gameObject.SendMessage(callFucName, callFucParam);
}

/// <summary>
/// 剥离外层 {{...}} 包裹（如果存在）
/// </summary>
private static string StripBraces(string input)
{
    if (string.IsNullOrEmpty(input)) return input;
    if (input.StartsWith("{{") && input.EndsWith("}}") && input.Length > 4)
    {
        return input.Substring(2, input.Length - 4);
    }
    return input;
}
```

---

## 四、影响分析

### 4.1 对现有功能的影响

| 组件 | 影响 | 说明 |
|------|------|------|
| `SpePlotFucPrefix` | 无改动 | 通过 SendMessage → SpePlotFuc 调用，自动走 Patch |
| `CheckConditionChangePlotDataBase` | 无改动 | 独立指令，互不影响 |
| `ChangeNextPlotQueryResolvePatch` | 无改动 | CallFuc 不经过 clickCallFuc 流程 |
| `ConditionExpressionEvaluator` | 无改动 | 直接复用 |
| `ResolveAllCommands` (SUBARG) | 自动生效 | SpePlotFuc 调用时会先解析 SUBARG |

### 4.2 与 `CheckConditionChangePlotDataBase` 的关系

| 指令 | 条件为真 | 条件为假 | 典型场景 |
|------|---------|---------|---------|
| `CheckConditionChangePlotDataBase` | 跳转剧情ID | 跳转剧情ID(可选) | 条件分支剧情 |
| `CheckConditionCallFuc` | SendMessage调用方法 | SendMessage调用方法(可选) | 条件执行任意方法 |

`CheckConditionCallFuc` 是 `CheckConditionChangePlotDataBase` 的通用版——后者只能跳转剧情，前者可通过 SendMessage 执行 PlotController 上的任意方法。

---

## 五、使用示例

### 示例1：根据好感度设置不同变量

```
; 条件为真时设置"好感达标"为"1"，否则设为"0"
SpePlotFuc;CheckConditionCallFuc*[$HeroData:Favor:小白$][>=]50#SpePlotFuc#{{SetStringValue*好感达标#1}}#SpePlotFuc#{{SetStringValue*好感不足#0}}
```

### 示例2：条件跳转剧情（等价于 CheckConditionChangePlotDataBase）

```
; ChangePlotDataBase 参数不含 #，{{}} 可选
SpePlotFuc;CheckConditionCallFuc*[$HeroData:isFemale$][=]1#ChangePlotDataBase#女线剧情#ChangePlotDataBase#男线剧情
```

### 示例3：条件获取任务

```
; 角色好感达标时获取支线任务（参数不含#，无需{{}}）
SpePlotFuc;CheckConditionCallFuc*[$HeroData:Favor:小白$][>=]80#SpePlotFuc#GetBranchMissionBySpeMissionID*96312
```

### 示例4：嵌套查询 + SUBARG 配合

```
; 先设置SUBARG，再条件判断
SpePlotFuc;SetSubArg*[$HeroData:heroID:小白$]
SpePlotFuc;CheckConditionCallFuc*[$HeroData:Favor:SUBARG$][>=]50#SpePlotFuc#{{SetStringValue*小白好感#达标}}
```

### 示例5：仅TrueCallFuc

```
; 只在条件为真时执行，为假时无操作
SpePlotFuc;CheckConditionCallFuc*[$WorldData:chapter$][>]3#SpePlotFuc#{{SetStringValue*后期标记#1}}
```

### 示例6：调用 GenerateRandomItem

```
; 条件为真时生成随机物品（参数不含#，无需{{}}）
SpePlotFuc;CheckConditionCallFuc*[$HeroData:Favor:小白$][>=]80#SpePlotFuc#GenerateRandomItem*plotInteractItem
```

### 示例7：True/False 调用不同方法

```
; 条件为真时跳转剧情，为假时设置变量
SpePlotFuc;CheckConditionCallFuc*[$HeroData:Favor:小白$][>=]50#ChangePlotDataBase#好感线剧情#SpePlotFuc#{{SetStringValue*好感不足#1}}
```

---

## 六、实现清单

| 序号 | 改动文件 | 改动内容 | 复杂度 |
|------|----------|----------|--------|
| 1 | `PlotControllerSpePlotFucPatch.cs` | SpePlotFucPrefix: `Split('#')` → `SplitRespectingBraces(paramPart, '#')` | 低 |
| 2 | `PlotControllerSpePlotFucPatch.cs` | FucHandlers 注册 + TryCallFucCheckConditionCallFuc + SplitRespectingBraces + StripBraces | 低 |
| 3 | `剧情指令使用说明.md` | 新增 CheckConditionCallFuc 指令文档 | 低 |

改动1是必须的基础优化（使 `{{}}` 保护 `#` 的机制生效），改动2是新增指令。
