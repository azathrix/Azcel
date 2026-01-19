<p align="center">
  <img src="https://img.shields.io/badge/📊-Azcel-blue?style=for-the-badge&labelColor=black" alt="Azcel" height="60">
</p>

<h1 align="center">Azcel</h1>

<p align="center">
  轻量级 Excel 数据表转换和配置管理器
</p>

<p align="center">
  <a href="https://github.com/AzathrixDev/Azcel"><img src="https://img.shields.io/badge/GitHub-Azcel-black.svg" alt="GitHub"></a>
  <a href="#"><img src="https://img.shields.io/badge/version-1.0.0-green.svg" alt="Version"></a>
  <a href="#license"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License"></a>
  <a href="https://unity.com/"><img src="https://img.shields.io/badge/Unity-6000.3+-black.svg" alt="Unity"></a>
</p>

---

## 特性

- **Excel 解析** - 支持多文件、多 Sheet，灵活的行列配置
- **代码生成** - 自动生成强类型配置类、表类、枚举
- **数据导出** - Binary 格式，体积小加载快（可扩展 JSON 等）
- **运行时查询** - 零 GC 的高性能 API
- **表继承** - 支持配置继承，减少重复数据
- **索引查询** - 支持自定义索引字段快速查询
- **类型扩展** - 可注册自定义类型解析器

## 安装

### 方式一：Package Manager 添加 Scope（推荐）

1. 打开 `Edit > Project Settings > Package Manager`
2. 在 `Scoped Registries` 中添加：
   - **Name**: `Azathrix`
   - **URL**: `https://registry.npmjs.org`
   - **Scope(s)**: `com.azathrix`
3. 点击 `Save`
4. 打开 `Window > Package Manager`
5. 切换到 `My Registries`
6. 找到 `Azcel` 并安装

### 方式二：Git URL

1. 打开 `Window > Package Manager`
2. 点击 `+` > `Add package from git URL...`
3. 输入：`https://github.com/azathrix/Azcel.git#latest`

> ⚠️ Git 方式无法自动解析依赖，需要先手动安装：
> - [Azathrix Framework](https://github.com/azathrix/AzathrixFramework)
> - [UniTask](https://github.com/Cysharp/UniTask)

### 方式三：npm 命令

在项目的 `Packages` 目录下执行：

```bash
npm install com.azathrix.azcel
```

## 依赖

| 包名 | 版本 |
|------|------|
| com.azathrix.framework | 1.0.0 |
| com.azathrix.unitask | 2.5.10 |

## 快速开始

### 1. 配置设置

打开 `Project Settings > Azcel配置`：

| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| Excel Paths | Excel 文件目录 | Assets/Excel |
| Code Output Path | 生成代码目录 | Assets/Scripts/Tables |
| Data Output Path | 数据文件目录 | Assets/Resources/TableData |
| Code Namespace | 代码命名空间 | Game.Tables |

### 2. 创建 Excel 表

**普通配置表** - 最常用的表结构

| ItemConfig |        |       |
|------------|--------|-------|
| id         | name   | price |
| int        | string | int   |
| #id        | #名称  | #价格 |
| 1          | 苹果   | 10    |
| 2          | 橘子   | 15    |
| 3          | 香蕉   | 8     |

- 第1行：表名（生成的类名）
- 第2行：字段名
- 第3行：类型
- 第4行：`#` 开头为注释行（可选）
- 第5行+：数据

**全局配置表** - 键值对形式

| GlobalConfig | config_type:keymap |        |          |
|--------------|--------------------|--------|----------|
| key          | value              | type   | comment  |
| Version      | 1.0.0              | string | 版本号   |
| MaxLevel     | 100                | int    | 最大等级 |
| Debug        | true               | bool   | 调试模式 |

**枚举表** - 自动生成枚举类型

| ItemType | config_type:enum |            |
|----------|------------------|------------|
| name     | value            | comment    |
| None     | 0                | 无         |
| Weapon   | 1                | 武器       |
| Armor    | 2                | 防具       |
| Consume  | 3                | 消耗品     |

**带索引的表** - 支持按字段快速查询

| ItemConfig | index:type |        |       |            |
|------------|------------|--------|-------|------------|
| id         | type       | name   | price | tags       |
| int        | ItemType   | string | int   | string[]   |
| 1001       | Weapon     | 铁剑   | 100   | 新手\|武器 |
| 1002       | Weapon     | 钢剑   | 200   | 武器       |
| 2001       | Armor      | 布甲   | 50    | 新手\|防具 |
| 3001       | Consume    | 红药   | 10    | 消耗品     |

- `index:type` 为 type 字段创建索引，支持 `GetByIndex<ItemConfig>("type", ItemType.Weapon)`
- `string[]` 数组类型，使用 `|` 分隔

**带继承的表** - 减少重复配置

| WeaponConfig | inherit:ItemConfig |        |      |
|--------------|--------------------|--------|------|
| id           | atk                | crit   | level|
| int          | int                | float  | int  |
| 1001         | 50                 | 0.1    | 1    |
| 1002         | 80                 | 0.15   | 5    |

- `inherit:ItemConfig` 继承 ItemConfig 的所有字段
- 生成的 WeaponConfig 包含 id, type, name, price, tags, atk, crit, level

**带引用的表** - 关联其他配置

| DropConfig |            |                  |
|------------|------------|------------------|
| id         | itemId     | rewards          |
| int        | ref:ItemConfig | ref:ItemConfig[] |
| 1          | 1001       | 1001\|1002\|3001 |
| 2          | 2001       | 2001\|3001       |

- `ref:ItemConfig` 引用类型，运行时自动解析为对应配置对象
- `ref:ItemConfig[]` 引用数组

### 3. 转换配置

菜单：`Azathrix > Azcel > 转换配置`（快捷键 Alt + `）

### 4. 运行时使用

```csharp
// 获取 Azcel 系统
var azcel = AzathrixFramework.GetSystem<AzcelSystem>();

// 获取单条配置
var item = azcel.GetConfig<ItemConfig>(1001);
Debug.Log(item.Name);  // 铁剑

// 获取全部配置
var allItems = azcel.GetAllConfig<ItemConfig>();

// 按索引查询
var weapons = azcel.GetByIndex<ItemConfig>("Type", ItemType.Weapon);
```

## Excel 配置语法

### 表头指令

| 指令 | 说明 | 示例 |
|------|------|------|
| `#config:` | 配置类名 | `#config: ItemConfig` |
| `#key:` | 主键字段 | `#key: Id` |
| `#keytype:` | 主键类型 | `#keytype: string` |
| `#index:` | 索引字段 | `#index: Type` |
| `#inherit:` | 继承表 | `#inherit: BaseConfig` |
| `#fieldrow:` | 字段行号 | `#fieldrow: 2` |
| `#typerow:` | 类型行号 | `#typerow: 3` |

### 支持的类型

| 类型 | 示例 |
|------|------|
| 基础类型 | `int`, `float`, `string`, `bool`, `long` |
| Unity 类型 | `Vector2`, `Vector3`, `Color` |
| 数组 | `int[]`, `string[]` |
| 枚举 | `ItemType` |
| 引用 | `ref:ItemConfig` |

### 数组语法

默认使用 `|` 分隔：

```
1|2|3|4|5  →  int[] { 1, 2, 3, 4, 5 }
```

## API 参考

### AzcelSystem

| 方法 | 说明 |
|------|------|
| `GetConfig<T>(key)` | 通过主键获取配置 |
| `TryGetConfig<T>(key, out config)` | 尝试获取配置 |
| `GetAllConfig<T>()` | 获取全部配置 |
| `GetByIndex<T>(indexName, value)` | 按索引查询 |
| `GetTable<T>()` | 获取表实例 |

## 扩展

### 自定义类型解析器

```csharp
[TypeParserPlugin]
public class MyTypeParser
{
    [TypeParser("MyType")]
    public static MyType Parse(string value)
    {
        return new MyType(value);
    }
}
```

### 自定义数据格式

```csharp
[ConfigFormatPlugin("json")]
public class JsonFormat : IConfigFormat
{
    // 实现序列化/反序列化
}
```

## 转换流程

```
Excel → Parse → Merge → Inheritance → Reference → Validation → CodeGen → Export
```

## License

MIT License
