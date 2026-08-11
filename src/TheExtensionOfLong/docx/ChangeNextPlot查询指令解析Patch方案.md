# ChangeNextPlot 查询指令解析 Patch — 可行性分析与实现方案

## 一、需求概述

当前查询指令 `[$类型:参数$]` 和算术表达式 `[&表达式&]` 仅能在特定位置使用（如剧情文本中的 `GlobalData.ReplaceSpeString`、`CheckConditionChangePlotDataBase` 条件表达式等），无法在 `clickCallFuc`（执行函数）中直接使用。

**目标**：对 `PlotController.ChangeNextPlot()` 进行 HarmonyPrefix Patch，在 `SendMessage` 执行前解析 `clickCallFuc` 中的查询指令和算术表达式，使查询/算术指令可以在执行函数的任何位置使用。

**额外规则**：被 `{{XX}}` 包裹的查询指令不在本次 Patch 中解析，保留原样传递给 `SendMessage` 做后续特殊解析。

---

## 二、目标方法分析

### 2.1 ChangeNextPlot 原始逻辑

```csharp
private void ChangeNextPlot()
{
    this.oldSinglePlot = this.nowSinglePlot;
    SinglePlotData firstPlot = this.nowPlot.plotDatas[0];
    bool noAutoJump = firstPlot.noAutoJump;

    if (firstPlot.clickCallFuc != null && firstPlot.clickCallFuc != "")
    {
        if (firstPlot.clickCallFuc == "ScreenBlack")
        {
            this.ScreenBlack();
            return;
        }

        string[] callBacks = firstPlot.clickCallFuc.Split('|');
        for (int i = 0; i < callBacks.Length; i++)
        {
            string[] parts = callBacks[i].Split(';');
            if (parts.Length < 2)
                base.SendMessage(parts[0]);
            else
                base.SendMessage(parts[0], parts[1]);

            if (this.oldSinglePlot != this.nowSinglePlot)
                noAutoJump = true;
        }

        if (noAutoJump)
        {
            this.ScreenBlack();
            return;
        }
    }

    if (!noAutoJump)
    {
        this.GoNextPlot();
    }
}
```

### 2.2 clickCallFuc 格式

| 格式 | 示例 | SendMessage 调用 |
|------|------|------------------|
| 无参回调 | `"MethodName"` | `SendMessage("MethodName")` |
| 有参回调 | `"MethodName;param"` | `SendMessage("MethodName", "param")` |
| 多回调并行 | `"Method1;param1\|Method2"` | 依次 `SendMessage` 每个回调 |

分隔符：`|` (ASCII 124) 分隔多个回调，`;` (ASCII 59) 分隔方法名与参数。

### 2.3 相关字段

| 类 | 字段 | 类型 | 说明 |
|----|------|------|------|
| `SinglePlotData` | `clickCallFuc` | `string` | 待解析的目标字段 |
| `SinglePlotData` | `noAutoJump` | `bool` | 是否阻止自动跳转 |
| `PlotController` | `nowPlot` | `PlotData` | 当前剧情数据 |
| `PlotController` | `nowSinglePlot` | `SinglePlotData` | 当前单条剧情 |
| `PlotController` | `oldSinglePlot` | `SinglePlotData` | 上一条剧情 |

---

## 三、可行性分析

### 3.1 技术可行性：✅ 可行

| 维度 | 评估 | 说明 |
|------|------|------|
| 字段可读写 | ✅ | `clickCallFuc` 是 `string` 类型（`SinglePlotData.cs:21`），可直接读写修改 |
| 已有先例 | ✅ | `GlobalDataReplaceSpeStringPatch.cs` 证明了 `Regex.Replace` + `ConditionQueryHandlers.ExecuteQuery()` 解析 `[$...$]` 和 `[&...&]` 的模式成熟 |
| Patch 模式 | ✅ | `PlotControllerChangePlotDataBasePatch.cs` 证明了对 `PlotController` 方法做 Prefix Patch 的模式可行 |
| 方法签名 | ✅ | `ChangeNextPlot` 是 `private void` 无参方法，Patch 时机清晰 |
| Il2Cpp 兼容 | ✅ | string 字段的赋值在 Il2Cpp 下是标准操作，与现有 Patch 一致 |
| 算术嵌套 | ✅ | `GlobalDataReplaceSpeStringPatch` 已支持 `[&[$A$]+[$B$]&]` 嵌套，解析顺序先 `[$]` 后 `[&]` |

### 3.2 核心依赖

- `ConditionQueryHandlers.ContainsParseableSyntax(string)` — 检查字符串是否包含可解析指令（`ConditionQueryHandlers.cs`）
- `ConditionQueryHandlers.ResolveAllCommands(PlotController, string)` — 指令解析统一入口（`ConditionQueryHandlers.cs`）
- `ConditionQueryHandlers.ExecuteQuery(PlotController, string)` — 查询指令执行入口（`ConditionQueryHandlers.cs`）
- `ConditionExpressionEvaluator.ParseArithExpr(string)` — 算术表达式求值（`ConditionQueryHandlers.cs`）
- `PlotController._instance` — 获取 PlotController 实例

---

## 四、风险点与缓解方案

### 4.1 风险 #1 详细分析：clickCallFuc 数据持久化

**初评**：⚠️ 中 — 修改 `clickCallFuc` 会持久化到 `SinglePlotData` 对象，若同一条剧情被多次触发，第二次执行时查询指令已被替换。

**深入分析**（结合 `GoNextPlot` 执行逻辑）：

`ChangeNextPlot` 执行后有 3 条路径：

| 路径 | 条件 | `RemoveAt(0)` | 持久化风险 |
|------|------|---------------|-----------|
| GoNextPlot | `noAutoJump=false` | ✅ 执行，`SinglePlotData` 被移除 | **不存在** |
| ScreenBlack（clickCallFuc 处理完，noAutoJump=true） | `noAutoJump=true` | ❌ 未执行 | **存在** |
| ScreenBlack（clickCallFuc=="ScreenBlack"） | 无 `[$`/`[&`，不进入解析 | — | 不涉及 |

**结论**：

- 最常见的 `GoNextPlot` 路径下，`RemoveAt(0)` 已将修改过的 `SinglePlotData` 从列表中移除，**不存在持久化问题**
- `noAutoJump=true` 路径虽不移除，但 Postfix 会恢复原值，**实际无风险**
- **但仍采用 Prefix+Postfix 配对方案**作为双重保险，原因：
  1. `noAutoJump=true` 路径下若不恢复，同一剧情节点下次点击时 `clickCallFuc` 仍是修改后的值
  2. SendMessage 触发剧情切换时，需通过对象引用而非 `nowPlot` 查找来恢复
  3. 防御性编程，确保所有路径安全

**风险等级调整**：⚠️中 → ⚡低

### 4.2 风险 #2 详细分析：`[&算术&]` 支持与 `[$查询$]` 嵌套

**需求**：支持 `[&算术表达式&]`，且允许嵌套 `[&[$查询A$]+[$查询B$]&]`。

**可行性**：✅ 完全可行

| 维度 | 评估 | 说明 |
|------|------|------|
| 已有先例 | ✅ | `GlobalDataReplaceSpeStringPatch.cs` 已实现 `[&算术&]` 解析，使用 `ConditionExpressionEvaluator.ParseArithExpr()` |
| 嵌套原理 | ✅ | 先解析 `[$...$]` 为具体数值，再对结果做 `[&...&]` 算术求值。两步顺序执行，天然支持嵌套 |
| 解析顺序 | ✅ | `GlobalDataReplaceSpeStringPatch` 采用 **Step1: `[$]` → Step2: `[&]`** 顺序，本方案照搬即可 |
| 核心依赖 | ✅ | `ConditionExpressionEvaluator.ParseArithExpr(string)` 已实现，返回 `double` |

**嵌套解析示例**：

```
原始:    [&[$HeroData:GetMoney:player$]+[$HeroData:GetMoney:sourceInteractHero$]&]
Step 1:  [&5000+3000&]        ← [$查询$] 先解析为数值
Step 2:  8000                  ← [&算术&] 再求值
```

**新增风险点**：

| # | 风险 | 等级 | 说明 | 缓解方案 |
|---|------|------|------|----------|
| 7 | **`[&]` 结果含小数** | ⚡ 低 | 算术求值结果可能为浮点数（如 `10/3`），作为 SendMessage 参数传递时，接收方可能期望整数 | 整数结果不显示小数点，非整数保留小数（与 `GlobalDataReplaceSpeStringPatch` 一致） |
| 8 | **`[&]` 内嵌套 `[$]` 解析顺序** | ⚡ 低 | 顺序错误会导致 `[&查询文本&]` 无法求值 | 严格按 Step1→Step2 顺序：先 `[$]` 后 `[&]` |
| 9 | **`[&]` 中 `+` 与 `;` 冲突** | ⚡ 低 | 若写入 `SetStringValue;key#[&1+2&]`，`;` 分隔正常 | `[&]` 内部的 `+` 不会影响外层 `;` 分割，因 `;` 分割在外层；`[&]` 整体作为参数值的一部分 |
| 10 | **`[&]` 中 `|` 冲突** | ⚡ 低 | 极罕见，`[&]` 内不太可能含 `|`（算术运算无此符号） | 若出现，可先按 `|` 分割再逐段解析 |

### 4.3 完整风险矩阵

| # | 风险 | 等级 | 缓解方案 |
|---|------|------|----------|
| 1 | **clickCallFuc 数据持久化** | ⚡ 低 | Prefix+Postfix 配对保存/恢复（双重保险，见4.1节详细分析） |
| 2 | **`{{}}` 与 `[$]/[&]` 的嵌套** | ⚠️ 中 | 分步解析：先提取/保护 `{{...}}` 区域，再对剩余部分做 `[$]` → `[&]` 替换 |
| 3 | **`|` 和 `;` 分隔冲突** | ⚡ 低 | 文档中说明限制；若未来出现冲突，可在解析前先按 `|` 和 `;` 拆分，仅对参数部分做查询替换 |
| 4 | **ScreenBlack 特殊值** | ⚡ 低 | 概率极低，无需特殊处理；若需防护可在解析后额外检查 |
| 5 | **Postfix 恢复时机** | ⚡ 低 | Harmony 保证 Postfix 必执行，无需额外处理 |
| 6 | **多回调中部分解析失败** | ⚡ 低 | 异常捕获后保留原始查询指令文本，不替换 |
| 7 | **`[&]` 结果含小数** | ⚡ 低 | 整数结果不显示小数点（与 `GlobalDataReplaceSpeStringPatch` 一致） |
| 8 | **`[&]` 内嵌套 `[$]` 解析顺序** | ⚡ 低 | 严格 Step1→Step2 顺序：先 `[$]` 后 `[&]` |
| 9 | **`[&]` 中 `+` 与 `;` 冲突** | ⚡ 低 | `;` 分割在外层，不影响 `[&]` 内部 |
| 10 | **`[&]` 中 `|` 冲突** | ⚡ 低 | 算术运算无 `|`，极罕见 |

---

## 五、实现方案

### 5.1 整体策略：Prefix + Postfix 配对 + 集中化解析

```
Prefix: 保存原始 clickCallFuc + firstPlot引用 → 调用 ConditionQueryHandlers.ResolveAllCommands → 修改 clickCallFuc → 放行原方法
原方法: 使用修改后的 clickCallFuc 执行 SendMessage
Postfix: 通过引用恢复原始 clickCallFuc
```

**关键架构决策**：解析逻辑（"能否解析"、"如何解析"）集中在 `ConditionQueryHandlers`，而非各 Patch 中。Patch 只负责"何时解析"。

解析顺序（由 `ConditionQueryHandlers.ResolveAllCommands` 统一执行）：
1. **Step 1**: 保护 `{{...}}` 区域
2. **Step 2**: 解析 `[$查询$]` — 先解析查询，为算术提供数值
3. **Step 3**: 解析 `[&算术&]` — 后解析算术，可使用查询结果
4. **Step 4**: 还原 `{{...}}` 区域

扩展新语法时只需修改 `ConditionQueryHandlers`（`SyntaxMarkers` + `ResolveAllCommands`），所有 Patch 自动受益。

### 5.2 代码骨架

```csharp
[HarmonyPatch(typeof(PlotController), "ChangeNextPlot")]
public class ChangeNextPlotQueryResolvePatch
{
    // 保存原始 clickCallFuc，用于 Postfix 恢复
    [ThreadStatic]
    private static string _originalClickCallFuc;

    // 保存被修改的 firstPlot 引用，用于 Postfix 精准恢复
    // （SendMessage 可能触发 ChangePlot 导致 nowPlot 变化，不能通过 nowPlot 查找）
    [ThreadStatic]
    private static SinglePlotData _modifiedPlot;

    [HarmonyPrefix]
    public static void ChangeNextPlotPrefix(PlotController __instance)
    {
        _modifiedPlot = null;
        _originalClickCallFuc = null;

        // 1. 获取 firstPlot
        var nowPlot = __instance.nowPlot;
        if (nowPlot == null) return;
        var plotDatas = nowPlot.plotDatas;
        if (plotDatas == null || plotDatas.Count == 0) return;
        SinglePlotData firstPlot = plotDatas[0];
        if (firstPlot == null) return;

        string clickCallFuc = firstPlot.clickCallFuc;
        if (string.IsNullOrEmpty(clickCallFuc)) return;

        // 2. 快速跳过：不含任何可解析指令语法则无需处理（逻辑集中化在 ConditionQueryHandlers）
        if (!ConditionQueryHandlers.ContainsParseableSyntax(clickCallFuc)) return;

        // 3. 保存原值 + 引用
        _originalClickCallFuc = clickCallFuc;
        _modifiedPlot = firstPlot;

        // 4. 解析所有指令语法（查询 + 算术 + 未来扩展，统一由 ConditionQueryHandlers 处理）
        string resolved = ConditionQueryHandlers.ResolveAllCommands(__instance, clickCallFuc);

        // 5. 写回
        if (resolved != clickCallFuc)
        {
            firstPlot.clickCallFuc = resolved;
            LoggerManager.Debug($"ChangeNextPlot查询解析: \"{Truncate(clickCallFuc, 100)}\" → \"{Truncate(resolved, 100)}\"");
        }
        else
        {
            // 无变化，不需要恢复
            _modifiedPlot = null;
            _originalClickCallFuc = null;
        }
    }

    [HarmonyPostfix]
    public static void ChangeNextPlotPostfix(PlotController __instance)
    {
        if (_modifiedPlot == null || _originalClickCallFuc == null) return;

        // 通过引用直接恢复，不依赖 nowPlot（可能已被 ChangePlot 替换）
        _modifiedPlot.clickCallFuc = _originalClickCallFuc;
        LoggerManager.Debug("ChangeNextPlot查询解析: 已恢复原始clickCallFuc");

        _modifiedPlot = null;
        _originalClickCallFuc = null;
    }

    private static string Truncate(string text, int maxLength)
    {
        if (text == null) return "null";
        if (text.Length <= maxLength) return text;
        return text.Substring(0, maxLength) + $"...(总长{text.Length})";
    }
}
```

> **对比旧方案**：旧代码骨架中包含私有方法 `ResolveQueryAndArithCommands`，内含 `{{}}` 保护、`[$]` 正则、`[&]` 正则等解析逻辑。新方案将全部解析逻辑移至 `ConditionQueryHandlers.ResolveAllCommands`，Patch 代码从 ~140 行精简至 ~60 行，且与 `GlobalDataReplaceSpeStringPatch` 共享同一解析管道。

### 5.3 Postfix 恢复的安全性论证

| 场景 | Prefix | 原方法 | Postfix | 恢复是否安全 |
|------|--------|--------|---------|-------------|
| 正常流程（GoNextPlot路径） | 修改 clickCallFuc | SendMessage + GoNextPlot（RemoveAt移除） | 恢复原值 | ✅ 安全，虽对象已被移除但恢复无害 |
| noAutoJump=true 路径 | 修改 clickCallFuc | SendMessage + ScreenBlack | 通过引用恢复原值 | ✅ 安全，确保下次点击重新求值 |
| SendMessage 导致剧情切换 | 修改 clickCallFuc | SendMessage 触发 ChangePlot | 通过 `_modifiedPlot` 引用恢复 | ✅ 安全（见下方说明） |
| 异常 | 修改 clickCallFuc | 异常抛出 | 恢复原值 | ✅ 安全 |

**关键：SendMessage 导致剧情切换时**：

当 `SendMessage` 触发了 `ChangePlot`，`nowPlot` 会被替换为新剧情。若 Postfix 通过 `__instance.nowPlot.plotDatas[0]` 查找，会错误地修改新剧情的 `clickCallFuc`。

本方案已在代码骨架中使用 `_modifiedPlot` 引用方案解决：Prefix 中保存 `firstPlot` 对象引用，Postfix 中直接对该引用恢复，**不依赖 `nowPlot` 查找**。

> **注意**：`[ThreadStatic]` 在 Unity 主线程场景下等价于静态变量（游戏基本是单线程），但使用 `[ThreadStatic]` 是更好的实践。由于 Il2Cpp 环境的限制，如果 `[ThreadStatic]` 不可用，改用普通静态字段即可。

---

## 六、使用示例

### 6.1 基础用法：在 clickCallFuc 中嵌入查询指令

```
;clickCallFuc 原始值:
SetStringValue;counter#[$GetIntVal:counter$]

;解析后（假设 counter=5）:
SetStringValue;counter#5
```

```
;clickCallFuc 原始值:
ChangePlotDataBase;[$GetStrVal:目标剧情ID$]

;解析后（假设 目标剧情ID=1024）:
ChangePlotDataBase;1024
```

### 6.2 多回调并行

```
;clickCallFuc 原始值:
SetStringValue;money#[$HeroData:GetMoney:player$]|ChangePlotDataBase;[$GetStrVal:下一剧情$]

;解析后:
SetStringValue;money#5000|ChangePlotDataBase;2048
```

### 6.3 算术表达式

```
;clickCallFuc 原始值:
SetStringValue;total#([&10+20*3&])

;解析后:
SetStringValue;total#70
```

### 6.4 算术嵌套查询

```
;clickCallFuc 原始值:
SetStringValue;combined#([&[$HeroData:GetMoney:player$]+[$HeroData:GetMoney:sourceInteractHero$]&])

;解析过程:
;Step1: SetStringValue;combined#([&5000+3000&])
;Step2: SetStringValue;combined#8000
```

```
;clickCallFuc 原始值:
SetStringValue;damage#([&[$GetIntVal:baseAtk$]*2+[$GetIntVal:bonus$]&])

;解析过程（假设 baseAtk=50, bonus=10）:
;Step1: SetStringValue;damage#([&50*2+10&])
;Step2: SetStringValue;damage#110
```

### 6.5 使用 {{}} 保护查询指令

被 `{{}}` 包裹的查询指令不在本次 Patch 中解析，保留原样传递：

```
;clickCallFuc 原始值:
CustomHandler;{{[$GetIntVal:counter$]}}

;解析后（{{$...$}}被保护，不解析）:
CustomHandler;{{[$GetIntVal:counter$]}}

;CustomHandler 收到的参数为: "[$GetIntVal:counter$]"
;可由 CustomHandler 自行决定如何解析
```

---

## 七、扩展考虑

### 7.1 指令解析逻辑集中化

**问题**：此前 `GlobalDataReplaceSpeStringPatch` 直接检查 `"[$"` / `"[&"` 并内联正则解析，`ChangeNextPlotQueryResolvePatch` 也拟采用相同做法。这导致：
- 解析逻辑散落在多个 Patch 中，扩展新语法时需逐个修改
- "能否解析"的判断（`Contains("[$")`）与"如何解析"的实现（正则模式）紧耦合在 Patch 中
- `ConditionQueryHandlers` 作为查询逻辑的核心，却无法控制"哪些语法标记可触发解析"

**重构**：将解析管道集中到 `ConditionQueryHandlers`：

| 方法 | 职责 | 调用者 |
|------|------|--------|
| `ContainsParseableSyntax(string)` | 判断字符串是否含可解析指令 | 所有 Patch 的快速跳过判断 |
| `ResolveAllCommands(PlotController, string)` | 统一解析管道（`{{}}` 保护 → `[$]` → `[&]` → `{{}}` 还原） | 所有 Patch 的实际解析 |
| `SyntaxMarkers` | 已知语法标记数组，扩展新语法时只需添加 | `ContainsParseableSyntax` 内部使用 |

**已同步重构**：`GlobalDataReplaceSpeStringPatch` 已改为调用 `ConditionQueryHandlers.ContainsParseableSyntax` / `ResolveAllCommands`，原内联正则代码已移除。

**扩展新语法的步骤**（以 `[@变量@]` 为例）：
1. 在 `SyntaxMarkers` 添加 `"[@"`
2. 在 `ResolveAllCommands` 中添加 Step N 解析 `[@...@]`
3. 所有 Patch 自动受益，无需修改

### 7.2 `[&算术&]` 已纳入方案

`ConditionQueryHandlers.ResolveAllCommands` 统一处理了 `[$查询$]` 和 `[&算术&]`，包括 `[&[$A$]+[$B$]&]` 嵌套。

实现要点：
- 解析顺序严格为 `[$]` → `[&]`，确保嵌套正确
- 使用 `ConditionExpressionEvaluator.ParseArithExpr()` 求值，与现有实现一致
- 整数结果不显示小数点，非整数保留小数

### 7.3 是否影响 PlotControllerSpePlotFucPatch？

`PlotControllerSpePlotFucPatch` 拦截的是 `SpePlotFuc` 方法，与 `ChangeNextPlot` 是不同的调用路径。本 Patch 在 `ChangeNextPlot` 的 `SendMessage` 之前解析，不影响 `SpePlotFuc` 的 Prefix 逻辑。

两者关系：
- `SpePlotFuc` 是 `clickCallFuc` 为 `"SpePlotFuc;指令名*参数"` 时的执行入口
- 本 Patch 在 `ChangeNextPlot` 中、`SendMessage("SpePlotFuc", param)` **之前**解析 `param` 中的查询指令
- 因此 `SpePlotFuc` 的 Prefix 收到的 `param` 已经是解析后的值

### 7.4 与 ChangePlotDataBasePatch 的交互

`ChangePlotDataBasePatch` 对 `ChangePlotDataBase(string plotID)` 做 Prefix，将符号化 ID 解析为数字 ID。若 `clickCallFuc` 中的查询指令解析后产生 `ChangePlotDataBase;SymbolicID` 的效果，两者会串联执行：

1. 本 Patch 将 `[$GetStrVal:目标剧情$]` 解析为具体 ID（如 `"1024"`）
2. `ChangePlotDataBasePatch` 判断 `"1024"` 可解析为 int，放行原方法

若查询结果本身是符号化 ID（如 `"myMod_plot1"`），则由 `ChangePlotDataBasePatch` 的 `SymbolicIdService.TryResolveId` 进一步解析。

---

## 八、测试要点

| # | 测试场景 | 预期 |
|---|----------|------|
| 1 | clickCallFuc 中无 `[$` 和 `[&` | 不做任何修改，原方法正常执行 |
| 2 | clickCallFuc 含 `[$GetIntVal:counter$]` | 查询值替换，SendMessage 收到解析后参数 |
| 3 | clickCallFuc 含 `[&10+20*3&]` | 算术求值，SendMessage 收到 `"70"` |
| 4 | clickCallFuc 含 `[&[$GetIntVal:base$]+[$GetIntVal:bonus$]&]` | 先查询后算术，嵌套正确求值 |
| 5 | clickCallFuc 含 `{{[$GetIntVal:counter$]}}` | 保护区域不解析，原样传递 |
| 6 | 同一剧情多次触发（noAutoJump=true） | 每次触发时查询指令重新求值（Postfix 恢复了原值） |
| 7 | SendMessage 触发剧情切换 | Postfix 仍通过 `_modifiedPlot` 引用正确恢复原 firstPlot 的 clickCallFuc |
| 8 | clickCallFuc == "ScreenBlack" | 无 `[$`/`[&`，不进入解析逻辑，ScreenBlack 正常触发 |
| 9 | 查询指令解析异常 | 保留原文，不替换，不崩溃 |
| 10 | 算术表达式求值异常（如除零） | 保留原文 `[&...&]`，不替换，不崩溃 |
| 11 | clickCallFuc 为 null 或空字符串 | 不进入解析逻辑 |
| 12 | 算术结果为整数（如 `[&10/2&]`） | 输出 `"5"` 不含小数点 |
| 13 | 算术结果为小数（如 `[&10/3&]`） | 输出 `"3.333333333333333"` 保留小数 |
