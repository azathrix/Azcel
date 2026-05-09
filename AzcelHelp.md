# Azcel Excel 配置速查

## 普通配置表

| ItemConfig | key:Id | index:Name,Type |  |  |
|---|---|---|---|---|
| Id | Name | Type | Price | Target |
| int | string | #ItemType | int | @NpcConfig |
| #comment | 主键 | 名称 | 类型 | 价格 | 关联 NPC |
| #setting |  |  |  |  | skip:false |
| 1 | Apple | Food | 10 | 1001 |
| 2 | Sword | Weapon | 80 | 1002 |

- 第 1 行第 1 格是配置名，会生成同名 C# 类。
- 第 2 行是字段名，第 3 行是类型。
- 数据行从字段/类型行之后开始。
- 任意以 `#` 开头的行都会被跳过。

## 标记行

| 标记 | 用途 |
|---|---|
| `#comment` | 字段注释行。可以首列单独写 `#comment`，也可以和字段列数对齐。 |
| `#setting` | 字段配置行。常用配置为 `skip:true` / `skip:false`。 |
| `#任意文本` | 整行跳过，适合临时注释、分组或说明。 |

## 表参数

参数写在第 1 行第 2 格以后，格式为 `key:value`。

| 参数 | 示例 | 说明 |
|---|---|---|
| `config_type` | `config_type:table` | 配置类型。可选 `table`、`enum`、`global`，未填写时默认 `table`。 |
| `key` | `key:Id` | 主键字段名。 |
| `keytype` | `keytype:int` | 主键类型。 |
| `index` | `index:Name,Type` | 索引字段列表，用逗号分隔。 |
| `extends` | `extends:BaseItem` | 继承另一张表的字段、字段配置和索引。 |
| `arrayseparator` | `arrayseparator:||` | 当前表数组分隔符。 |
| `objectseparator` | `objectseparator:;` | 当前表对象分隔符。 |
| `fieldrow` | `fieldrow:2` | 字段名所在行，1-based。 |
| `typerow` | `typerow:3` | 类型所在行，1-based。 |
| `field_keymap` / `fieldkeymap` | `field_keymap:true` | 启用字段名动态取值缓存。 |

## 类型写法

| 写法 | 说明 |
|---|---|
| `int` / `long` / `float` / `double` / `bool` / `string` | 基础类型。 |
| `Vector2` / `Vector3` / `Vector4` | Unity 向量类型。 |
| `Vector2Int` / `Vector3Int` | Unity 整数向量类型。 |
| `Color` / `Rect` | Unity 常用结构类型。 |
| `int[]` / `Vector3[]` / `#ItemType[]` | 数组类型。 |
| `@NpcConfig` | 表引用。引用 ID 类型会跟随目标表 `keytype`。 |
| `#ItemType` | 枚举引用。单元格可以写枚举名或数值。 |

## 枚举表

| ItemType | config_type:enum |  |
|---|---|---|
|  |  |  |
|  |  |  |
|  |  |  |
| Food | 1 | 食物 |
| Weapon | 2 | 武器 |

## 全局配置

| GameGlobal | config_type:global |  |  |
|---|---|---|---|
| #comment | 可放说明行 |  |  |
| key | value | type | comment |
| MaxLevel | 99 | int | 最大等级 |
| DefaultNpc | 1001 | @NpcConfig | 默认 NPC |
| StartItems | 1\|\|2 | int[] | 初始道具 |

## 注意事项

- 表名、字段名、枚举名、枚举值和全局 `key` 会生成 C# 代码，必须是合法 C# 标识符，不能使用关键字。
- 数据文件默认输出到 `Assets/Resources/TableData`，运行时 Resources 路径会自动转为 `TableData`。
- 使用多字符分隔符时，数组和对象值会按完整分隔符切分。
- 表引用需要确保目标表存在；引用 ID 会按目标表主键类型校验和序列化。
