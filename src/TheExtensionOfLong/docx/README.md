# 【剧情指令拓展】模组-自定义特殊剧情指令 (SpePlotFuc) 配置说明

## 安装方法
同【龙之书】模组

## 一、概述

本模组主要用于搭配模组框架【龙之书】一同使用，在龙之书抓取的剧情数据（PlotData.csv）、剧情控制器（WorldPlotEventController）、任务控制器（MissionDataController）中的相关指令输入栏中，可以使用SpePlotFuc指令调用此模组拓展的新自定义指令，具体格式为：

```
SpePlotFuc;指令名#参数1#参数2#...
```

- `SpePlotFuc;` 为固定前缀
- `指令名` 为具体功能名称
- `#` 分隔各参数
- 不同指令所需参数不同，详见下方各指令说明

### 角色ID写法

部分指令需要指定角色ID，支持以下写法：

|| 写法 | 含义 |
|------|------|
| `player` | 玩家角色 |
| `sourceInteractHero` | 当前交互的源角色 |
| `targetInteractHero` | 当前交互的目标角色 |
| 整数（如 `1001`） | 按 int ID 查找角色 |
| 字符串（如 `小白`） | 按字符串 ID 查找角色 |

---

## 二、条件分支类指令

条件分支类指令根据判断结果跳转到不同剧情ID，格式中均包含 `TruePlotId-FalsePlotId`，用 `-` 分隔：

- 条件成立 → 跳转 `TruePlotId`
- 条件不成立 → 跳转 `FalsePlotId`

---

### 2.1 HaveLoverChangePlotDataBase — 判断角色是否有恋人

**格式：**

```
SpePlotFuc;HaveLoverChangePlotDataBase#TruePlotId-FalsePlotId#heroId(可选)
```

|| 参数 | 位置 | 必填 | 说明 |
|------|------|------|------|
| `TruePlotId` | #1（`-`前） | 是 | 有恋人时跳转的剧情ID |
| `FalsePlotId` | #1（`-`后） | 是 | 无恋人时跳转的剧情ID |
| `heroId` | #2 | 否 | 要判断的角色，省略时默认为 `sourceInteractHero` |

**示例：**

```
SpePlotFuc;HaveLoverChangePlotDataBase#101-102
SpePlotFuc;HaveLoverChangePlotDataBase#101-102#小白
SpePlotFuc;HaveLoverChangePlotDataBase#101-102#player
```

- 第1行：判断 `sourceInteractHero` 是否有恋人
- 第2行：判断小白是否有恋人
- 第3行：判断玩家是否有恋人

---

### 2.2 IsLoverChangePlotDataBase — 判断两角色是否为恋人

**格式：**

```
SpePlotFuc;IsLoverChangePlotDataBase#TruePlotId-FalsePlotId#sourceHeroId(可选)#targetHeroId(可选)
```

|| 参数 | 位置 | 必填 | 说明 |
|------|------|------|------|
| `TruePlotId` | #1（`-`前） | 是 | 为恋人时跳转的剧情ID |
| `FalsePlotId` | #1（`-`后） | 是 | 非恋人时跳转的剧情ID |
| `sourceHeroId` | #2 | 否 | 源角色，省略时默认为 `sourceInteractHero` |
| `targetHeroId` | #3 | 否 | 目标角色，省略时默认为 `targetInteractHero` |

> 判断逻辑：源角色的恋人ID是否等于目标角色的heroID

**示例：**

```
SpePlotFuc;IsLoverChangePlotDataBase#101-102
SpePlotFuc;IsLoverChangePlotDataBase#101-102#player#小白
```

- 第1行：判断 `sourceInteractHero` 是否以 `targetInteractHero` 为恋人
- 第2行：判断玩家是否以小白为恋人

---

### 2.3 IsPreLoverChangePlotDataBase — 判断两角色是否为准恋人

**格式：**

```
SpePlotFuc;IsPreLoverChangePlotDataBase#TruePlotId-FalsePlotId#sourceHeroId(可选)#targetHeroId(可选)
```

|| 参数 | 位置 | 必填 | 说明 |
|------|------|------|------|
| `TruePlotId` | #1（`-`前） | 是 | 为准恋人时跳转的剧情ID |
| `FalsePlotId` | #1（`-`后） | 是 | 非准恋人时跳转的剧情ID |
| `sourceHeroId` | #2 | 否 | 源角色，省略时默认为 `sourceInteractHero` |
| `targetHeroId` | #3 | 否 | 目标角色，省略时默认为 `targetInteractHero` |

**示例：**

```
SpePlotFuc;IsPreLoverChangePlotDataBase#101-102
SpePlotFuc;IsPreLoverChangePlotDataBase#101-102#player#小白
```

- 第1行：判断 `sourceInteractHero` 是否以 `targetInteractHero` 为准恋人
- 第2行：判断玩家是否以小白为准恋人

---

### 2.4 HaveRelationBetterThanFriendChangePlotDataBase — 判断是否有超越朋友的关系

判断两个角色之间是否存在比朋友更亲密的关系（恋人、准恋人、师徒、结义兄弟等）。

**格式：**

```
SpePlotFuc;HaveRelationBetterThanFriendChangePlotDataBase#TruePlotId-FalsePlotId#sourceHeroId(可选)#targetHeroId(可选)#checkTeacher(可选)#checkBrother(可选)
```

|| 参数 | 位置 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `TruePlotId` | #1（`-`前） | 是 | — | 有超越朋友关系时跳转的剧情ID |
| `FalsePlotId` | #1（`-`后） | 是 | — | 无超越朋友关系时跳转的剧情ID |
| `sourceHeroId` | #2 | 否 | `sourceInteractHero` | 源角色 |
| `targetHeroId` | #3 | 否 | `targetInteractHero` | 目标角色 |
| `checkTeacher` | #4 | 否 | `1`（检查） | 是否检查师徒关系，`1`=检查，`0`=不检查 |
| `checkBrother` | #5 | 否 | `1`（检查） | 是否检查结义关系，`1`=检查，`0`=不检查 |

> 恋人和准恋人关系始终检查，`checkTeacher` 和 `checkBrother` 仅控制是否额外检查师徒和结义兄弟关系。

**示例：**

```
SpePlotFuc;HaveRelationBetterThanFriendChangePlotDataBase#101-102
SpePlotFuc;HaveRelationBetterThanFriendChangePlotDataBase#101-102#player#小白
SpePlotFuc;HaveRelationBetterThanFriendChangePlotDataBase#101-102#player#小白#0#0
```

- 第1行：判断 `sourceInteractHero` 与 `targetInteractHero` 是否有超越朋友的关系（检查全部关系）
- 第2行：判断玩家与小白是否有超越朋友的关系（检查全部关系）
- 第3行：判断玩家与小白是否有超越朋友的关系（仅检查恋人/准恋人，不检查师徒和结义）

---

### 2.5 HaveStringKeyChangePlotDataBase — 判断变量是否存在且非空

检查 PlotEventLogData 中指定 Key 是否存在且值非空。

**格式：**

```
SpePlotFuc;HaveStringKeyChangePlotDataBase#TruePlotId-FalsePlotId#key
```

|| 参数 | 位置 | 必填 | 说明 |
|------|------|------|------|
| `TruePlotId` | #1（`-`前） | 是 | Key存在且非空时跳转的剧情ID |
| `FalsePlotId` | #1（`-`后） | 是 | Key不存在或为空时跳转的剧情ID |
| `key` | #2 | 是 | 要检查的变量名 |

**示例：**

```
SpePlotFuc;HaveStringKeyChangePlotDataBase#101-102#事件标记
```

- `事件标记` 这个变量存在且非空 → 跳转剧情101
- `事件标记` 不存在或值为空 → 跳转剧情102

---

### 2.6 CheckStringValueChangePlotDataBase — 比较变量值

检查 PlotEventLogData 中指定 Key 的值与目标值的关系。

**格式：**

```
SpePlotFuc;CheckStringValueChangePlotDataBase#TruePlotId-FalsePlotId#key#运算符#目标值
```

|| 参数 | 位置 | 必填 | 说明 |
|------|------|------|------|
| `TruePlotId` | #1（`-`前） | 是 | 条件成立时跳转的剧情ID |
| `FalsePlotId` | #1（`-`后） | 是 | 条件不成立时跳转的剧情ID |
| `key` | #2 | 是 | 要检查的变量名 |
| `运算符` | #3 | 是 | 比较运算符（见下表） |
| `目标值` | #4 | 是 | 比较的目标值 |

**支持的运算符：**

|| 运算符 | 含义 | 数值可用 | 字符串可用 |
|--------|------|----------|-----------|
| `>` | 大于 | ✓ | ✗ |
| `<` | 小于 | ✓ | ✗ |
| `>=` | 大于等于 | ✓ | ✗ |
| `<=` | 小于等于 | ✓ | ✗ |
| `=` 或 `==` | 等于 | ✓ | ✓ |
| `!=` 或 `<>` | 不等于 | ✓ | ✓ |

> 若变量值和目标值都能解析为数字，则做数值比较（浮点相等用 0.0001 精度）；否则做字符串比较，此时只有 `=`/`==` 和 `!=`/`<>` 有效。

**示例：**

```
SpePlotFuc;CheckStringValueChangePlotDataBase#101-102#好感度#>80
SpePlotFuc;CheckStringValueChangePlotDataBase#101-102#当前路线#=白线
SpePlotFuc;CheckStringValueChangePlotDataBase#101-102#章节进度#>=3
```

- 第1行：好感度 > 80 → 跳转101
- 第2行：当前路线等于"白线" → 跳转101
- 第3行：章节进度 >= 3 → 跳转101

---

### 2.7 CheckConditionChangePlotDataBase — 复合条件判断

支持在一条指令中组合多个查询、算术运算、逻辑运算，功能最强大。

**格式：**

```
SpePlotFuc;CheckConditionChangePlotDataBase#条件表达式#TruePlotId-FalsePlotId
```

条件表达式支持 `[$查询$]`、`[&算术&]`、关系运算符、`[AND]`、`[OR]`、`()` 等元素，详见 → [复合条件指令使用说明.md](复合条件指令使用说明.md)

**示例：**

```
SpePlotFuc;CheckConditionChangePlotDataBase#[$IsLover:玩家:小白$][=]1#101-102
SpePlotFuc;CheckConditionChangePlotDataBase#[$HeroData:favor:小白$][>]80[AND][$GetStrVal:章节进度$][>]3#101-102
```

> **提示**：上述 2.1 ~ 2.6 的所有简单条件指令均可改用 `CheckConditionChangePlotDataBase` 实现。简单指令书写更简便，复合指令功能更灵活，按需选择即可。

---

## 三、变量操作类指令

### 3.1 SetStringValue — 设置字符串变量

向 PlotEventLogData 中写入一个键值对。

**格式：**

```
SpePlotFuc;SetStringValue#key#value
```

|| 参数 | 位置 | 必填 | 说明 |
|------|------|------|------|
| `key` | #1 | 是 | 变量名，不能为空或空白 |
| `value` | #2 | 是 | 变量值 |

**示例：**

```
SpePlotFuc;SetStringValue#章节进度#3
SpePlotFuc;SetStringValue#当前路线#白线
```

- 第1行：设置 `章节进度` = `"3"`
- 第2行：设置 `当前路线` = `"白线"`

> 设置的变量可通过 `HaveStringKeyChangePlotDataBase`、`CheckStringValueChangePlotDataBase`、`CheckConditionChangePlotDataBase` 的 `GetStrVal`/`GetFlotVal` 查询来读取。

---

## 四、显示定制类指令

### 4.1 SetHeroRelationShipText — 设置角色关系文本

为两个角色之间设置自定义关系文本，文本会保存在 PlotEventLogData 中，在角色交互界面展示。

**格式：**

```
SpePlotFuc;SetHeroRelationShipText#sourceHeroId(可选)#targetHeroId(可选)#关系文本
```

|| 参数 | 位置 | 必填 | 说明 |
|------|------|------|------|
| `sourceHeroId` | #1 | 否 | 源角色，省略时默认为 `sourceInteractHero` |
| `targetHeroId` | #2 | 否 | 目标角色，省略时默认为 `targetInteractHero` |
| `关系文本` | #3 | 是 | 要显示的自定义关系文本 |

> 两个角色ID均可省略，但 `#` 分隔符仍需保留。若省略角色ID，`#` 之间留空即可。

**示例：**

```
SpePlotFuc;SetHeroRelationShipText#player#小白#青梅竹马
SpePlotFuc;SetHeroRelationShipText###恋人
```

- 第1行：设置玩家与小白的关系文本为"青梅竹马"
- 第2行：源角色和目标角色均使用交互上下文默认值，关系文本设为"恋人"

---

## 五、指令速查表

| 指令名 | 功能 | 参数概要 |
|--------|------|----------|
| `HaveLoverChangePlotDataBase` | 角色是否有恋人 | `#TrueId-FalseId#heroId?` |
| `IsLoverChangePlotDataBase` | 两角色是否为恋人 | `#TrueId-FalseId#sourceId?#targetId?` |
| `IsPreLoverChangePlotDataBase` | 两角色是否为准恋人 | `#TrueId-FalseId#sourceId?#targetId?` |
| `HaveRelationBetterThanFriendChangePlotDataBase` | 是否有超越朋友的关系 | `#TrueId-FalseId#sourceId?#targetId?#checkTeacher?#checkBrother?` |
| `HaveStringKeyChangePlotDataBase` | 变量是否存在且非空 | `#TrueId-FalseId#key` |
| `CheckStringValueChangePlotDataBase` | 比较变量值 | `#TrueId-FalseId#key#运算符#目标值` |
| `CheckConditionChangePlotDataBase` | 复合条件判断 | `#表达式#TrueId-FalseId` |
| `SetStringValue` | 设置字符串变量 | `#key#value` |
| `SetHeroRelationShipText` | 设置角色关系文本 | `#sourceId?#targetId?#关系文本` |

> `?` 表示该参数可选。`TrueId-FalseId` 表示用 `-` 分隔的两个剧情ID。

---

## 六、注意事项

1. **参数分隔符**：各参数之间用 `#` 分隔，剧情ID对（TrueId-FalseId）内部用 `-` 分隔
2. **角色ID可省略**：省略时自动使用交互上下文中的角色，无需额外填写
3. **布尔参数**：`checkTeacher`/`checkBrother` 使用 `1`（TRUE）/ `0`（FALSE），省略时默认为 `1`
4. **简单指令 vs 复合指令**：单一条件判断优先使用简单指令（书写更短），多条件组合使用 `CheckConditionChangePlotDataBase`
5. **SetStringValue 先于条件判断**：如需在同一段剧情中先设置变量再判断，请分为两条 SpePlotFuc 指令依次执行
6. **变量值的比较**：`CheckStringValueChangePlotDataBase` 会自动识别数值/字符串，做数值比较时支持 `>`、`<` 等运算符；做字符串比较时仅支持 `=` 和 `!=`
