# SetSubArg 指令实现方案

## 一、需求概述

新增指令 `SetSubArg`，用于设置查询指令的替换参数。当查询指令中需要动态指定某个参数值时，使用 `SUBARG` 关键字作为占位符，运行时由 `SetSubArg` 设置的实际值替换。

### 核心用法

```
; 1. 设置替换参数
SpePlotFuc;SetSubArg*100

; 2. 查询指令中使用 SUBARG 占位
[$HeroData:heroName:SUBARG$]     → 替换为 [$HeroData:heroName:100$] → "小白"
[$HeroData:GetMoney:SUBARG$]     → 替换为 [$HeroData:GetMoney:100$] → "5000"
[$IsLover:player:SUBARG$]        → 替换为 [$IsLover:player:100$]    → "1"
```

---

## 二、可行性评估

### ✅ 技术可行

| 评估项 | 结论 | 依据 |
|--------|------|------|
| 存储机制 | ✅ 可行 | `PlotEventLogData.Set("SUBARG", value)` 与 `SetStringValue`、`SetHeroAddressForm` 完全一致 |
| 替换时机 | ✅ 可行 | 在 `ResolveAllCommands` 入口处（Step 0）执行替换，早于 `[$]` 和 `[&]` 解析 |
| 替换范围 | ✅ 可行 | 仅在指令解析流中替换，不影响显示文本（3个入口均走 `ResolveAllCommands`） |
| 多入口覆盖 | ✅ 可行 | 替换逻辑加在 `ResolveAllCommands` 内部，3个入口自动覆盖 |

### ⚠️ 注意事项

| 风险点 | 分析 | 应对 |
|--------|------|------|
| SUBARG 未设置 | 替换时读取 PlotEventLogData 中无 "SUBARG" key → 不替换，保留原文 | 合理行为，文档说明即可 |
| SUBARG 值含特殊字符 | 如值包含 `:` `$` `[` `]` 等，可能破坏查询语法 | 用户责任，文档警示 |
| 值跨场景持久化 | PlotEventLogData 是存档级持久化，SUBARG 值不会自动清除 | 提供 `SetSubArg*`（空值）清除；文档说明需手动清除 |
| 嵌套查询中的 SUBARG | `[$GetStrVal:SUBARG$]` 替换后变为 `[$GetStrVal:之前存的值$]` → 再解析 | 符合预期，两层解析 |

---

## 三、实现方案

### 3.1 数据流

```
SetSubArg*100
    │
    └─ PlotEventLogData.Set("SUBARG", "100")
                                         ↓
指令字符串: "[$HeroData:heroName:SUBARG$]"
    │
    ├─ ResolveAllCommands 入口 (Step 0)
    │   ├─ 检测到包含 "SUBARG"
    │   ├─ 从 PlotEventLogData 读取 "SUBARG" → "100"
    │   └─ 替换: "[$HeroData:heroName:100$]"
    │
    ├─ Step 1: 保护 {{...}}
    ├─ Step 2: 解析 [$HeroData:heroName:100$] → "小白"
    ├─ Step 3: 解析 [&...&]
    └─ Step 4: 还原 {{...}}
                                         ↓
结果: "小白"
```

### 3.2 代码改动

#### 改动1：`PlotControllerSpePlotFucPatch.cs` — 新增指令

**FucHandlers 注册**：
```csharp
{ "SetSubArg",  TryCallFucSetSubArg },
```

**TryCallFucSetSubArg 方法**：
```csharp
/// <summary>
/// 设置查询指令替换参数，SUBARG关键字在指令解析时替换为实际值
/// 格式: SetSubArg*设置值
///   设置值为空时清除替换参数
/// 示例: SetSubArg*100          → SUBARG替换为"100"
///       SetSubArg*小白         → SUBARG替换为"小白"
///       SetSubArg*             → 清除替换参数
/// </summary>
private static void TryCallFucSetSubArg(PlotController __instance, string fucName, string[] fucParams)
{
    PlotEventLogData plotEventLogData = CommonHandlers.GetPlotEventLogData();
    if (plotEventLogData == null)
    {
        LoggerManager.Error($"{fucName}: PlotEventLogData实例不存在");
        return;
    }

    string value = fucParams.Length > 0 ? fucParams[0] : "";

    if (string.IsNullOrEmpty(value))
    {
        // 清除：设置为空字符串
        plotEventLogData.Set("SUBARG", "");
        LoggerManager.Debug($"{fucName}: 已清除替换参数 SUBARG");
    }
    else
    {
        plotEventLogData.Set("SUBARG", value);
        LoggerManager.Debug($"{fucName}: 已设置替换参数 SUBARG={value}");
    }
}
```

#### 改动2：`ConditionQueryHandlers.cs` — ResolveAllCommands 增加 Step 0

在 `ResolveAllCommands` 方法的 `ContainsParseableSyntax` 检查**之后**、Step 1 保护 `{{...}}` **之前**，插入 SUBARG 替换：

```csharp
public static string ResolveAllCommands(PlotController pc, string input)
{
    if (string.IsNullOrEmpty(input) || !ContainsParseableSyntax(input))
        return input;

    // Step 0: 替换 SUBARG 关键字为实际值
    if (input.Contains("SUBARG"))
    {
        PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
        if (logData != null && logData.HaveKey("SUBARG"))
        {
            string subArgValue = logData.Get("SUBARG");
            if (!string.IsNullOrEmpty(subArgValue))
            {
                input = input.Replace("SUBARG", subArgValue);
            }
        }
    }

    // Step 1: 保护 {{...}} 区域（原有逻辑不变）
    ...
}
```

**关键设计决策**：SUBARG 替换放在 `ResolveAllCommands` 内部而非各 Patch 入口，原因：
- 3个调用入口（SpePlotFucPrefix、ChangeNextPlotQueryResolvePatch、GlobalDataReplaceSpeStringPatch）统一覆盖
- 替换逻辑集中维护，不遗漏
- 对各 Patch 文件零改动

#### 改动3（可选优化）：`ContainsParseableSyntax` 扩展

如果希望 `SUBARG` 单独出现（无 `[$` `[&`）时也触发解析，需扩展：

```csharp
private static readonly string[] SyntaxMarkers = { "[$", "[&", "SUBARG" };
```

**是否需要**：根据需求，SUBARG 总是配合 `[$]` 或 `[&]` 使用，不应单独出现。**建议不扩展**，保持 SUBARG 仅在已有查询语法的上下文中替换。如果用户写了 `SetSubArg*100` 后单独使用 `SUBARG`（不在 `[$]` 中），则不会被替换——这是合理的。

---

## 四、影响分析

### 4.1 对现有功能的影响

| 组件 | 影响 | 说明 |
|------|------|------|
| `SpePlotFucPrefix` | 无改动 | SUBARG 替换在 `ResolveAllCommands` 内部完成 |
| `ChangeNextPlotQueryResolvePatch` | 无改动 | 同上 |
| `GlobalDataReplaceSpeStringPatch` | 无改动 | 同上，剧情文本中的 SUBARG 也会被替换 |
| `ConditionExpressionEvaluator.Evaluate` | 无改动 | Evaluate 直接处理表达式，不走 ResolveAllCommands；ChooseHero 的 `{{}}` 中若含 SUBARG 会在 Evaluate 内调用前已被 ResolveAllCommands 替换 |

### 4.2 与 `{{}}` 延迟求值的交互

`{{}}` 的保护在 Step 1，SUBARG 替换在 Step 0（更早），因此：

```
ChooseHero*{{[$HeroData:heroName:SUBARG$][=]小白}}
```

解析流程：
1. Step 0: `SUBARG` → 替换为实际值 → `ChooseHero*{{[$HeroData:heroName:100$][=]小白}}`
2. Step 1: `{{...}}` → 被保护，不预解析
3. 遍历角色时 Evaluate: 剥离 `{{}}` → `[$HeroData:heroName:100$][=]小白` → 逐角色求值

**注意**：这意味着 SUBARG 在 ChooseHero 遍历前就已固定，不会随遍历角色变化。如果需要动态替换为当前遍历角色的 ID，应使用 `targetInteractHero` 等内置关键字而非 SUBARG。

---

## 五、使用示例

### 示例1：根据变量查询指定角色

```
; 将变量"目标角色ID"的值设为SUBARG
SpePlotFuc;SetSubArg*[$GetStrVal:目标角色ID$]

; 后续查询中使用SUBARG
[$HeroData:heroName:SUBARG$]    → 查询变量指定的角色名称
[$HeroData:GetMoney:SUBARG$]    → 查询变量指定的角色金钱
```

### 示例2：与 ChooseHero 回调配合

```
; ChooseHero 选中角色后，回调中将选中角色ID存为SUBARG
; 回调指令: SetSubArg*[$HeroData:heroID:ChooseHero$]

; 后续剧情中使用SUBARG引用选中角色
[$HeroData:heroName:SUBARG$]    → 选中角色的名称
[$HeroData:GetMoney:SUBARG$]    → 选中角色的金钱
```

### 示例3：清除替换参数

```
; 使用完毕后清除，避免影响后续剧情
SpePlotFuc;SetSubArg*
```

---

## 六、实现清单

| 序号 | 改动文件 | 改动内容 | 复杂度 |
|------|----------|----------|--------|
| 1 | `PlotControllerSpePlotFucPatch.cs` | FucHandlers 注册 + TryCallFucSetSubArg 方法 | 低 |
| 2 | `ConditionQueryHandlers.cs` | ResolveAllCommands 增加 Step 0 SUBARG 替换 | 低 |
| 3 | `查询指令使用说明.md` | 新增 SUBARG 说明 | 低 |
| 4 | `剧情指令使用说明.md` | 新增 SetSubArg 指令文档 | 低 |

总计改动量极小，无需新增文件，无需新增 Patch。
