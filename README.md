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
- **运行时查询** - 高性能 API（含无分配版本 + 可控缓存）
- **表继承** - 支持配置继承，减少重复数据
- **索引查询** - 支持自定义索引字段快速查询
- **Schema 校验** - 二进制数据与脚本字段一致性校验（避免错位读取）
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
| Excel Paths | Excel 文件目录列表 | Assets/Excel |
| Code Output Path | 生成代码目录 | Assets/Scripts/Tables |
| Data Output Path | 数据文件目录 | Assets/Resources/TableData |
| Code Namespace | 代码命名空间 | Game.Tables |
| Data Format Id | 数据格式 | binary |
| Array Separator | 数组分隔符 | \| |
| Object Separator | 对象分隔符 | , |
| Use Query Cache | 查询缓存开关（GetAllConfig/GetByIndex） | true |
| Default Key Field | 默认主键字段 | Id |
| Default Key Type | 默认主键类型 | int |
| Default Field Row | 默认字段行 | 2 |
| Default Type Row | 默认类型行 | 3 |

### 2. 创建 Excel 表

**普通配置表** - 最常用的表结构

| ItemConfig |        |       |
|------------|--------|-------|
| id         | name   | price |
| int        | string | int   |
| #comment   | 名称   | 价格  |
| 1          | 苹果   | 10    |
| 2          | 橘子   | 15    |
| 3          | 香蕉   | 8     |

- 第1行：表名（生成的类名）
- 第2行：字段名
- 第3行：类型
- `#comment` / `#setting` 行位置不固定（扫描到即识别）
- 任意以 `#` 开头的行都会被跳过（纯注释行）
- 数据行可不连续，空行会自动跳过

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
| int        | #ItemType  | string | int   | string[]   |
| 1001       | Weapon     | 铁剑   | 100   | 新手\|武器 |
| 1002       | Weapon     | 钢剑   | 200   | 武器       |
| 2001       | Armor      | 布甲   | 50    | 新手\|防具 |
| 3001       | Consume    | 红药   | 10    | 消耗品     |

- `index:type` 为 type 字段创建索引，支持 `GetByIndex<ItemConfig>("type", ItemType.Weapon)`
- `#ItemType` 引用枚举类型，`#` 前缀表示枚举
- `string[]` 数组类型，使用 `|` 分隔

**带继承的表** - 减少重复配置

| WeaponConfig | extends:ItemConfig |        |      |
|--------------|--------------------|--------|------|
| id           | atk                | crit   | level|
| int          | int                | float  | int  |
| 1001         | 50                 | 0.1    | 1    |
| 1002         | 80                 | 0.15   | 5    |

- `extends:ItemConfig` 继承 ItemConfig 的所有字段
- 生成的 WeaponConfig 包含 id, type, name, price, tags, atk, crit, level

**带引用的表** - 关联其他配置

| DropConfig |            |                  |
|------------|------------|------------------|
| id         | itemId     | rewards          |
| int        | @ItemConfig | @ItemConfig[]   |
| 1          | 1001       | 1001\|1002\|3001 |
| 2          | 2001       | 2001\|3001       |

- `@ItemConfig` 表引用，运行时自动解析为对应配置对象
- `@ItemConfig[]` 表引用数组

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

// 关闭查询缓存（GetAllConfig/GetByIndex 将走非缓存路径）
azcel.UseQueryCache = false;

// 无分配版本（推荐用于性能敏感场景）
var allItemsNoAlloc = azcel.GetAllConfig<ItemConfig, int>();

// 按索引查询
var weapons = azcel.GetByIndex<ItemConfig>("Type", ItemType.Weapon);
```

## Excel 配置语法

### 配置行参数（第1行）

| 参数 | 说明 | 示例 |
|------|------|------|
| `key` | 主键字段 | `key:Id` |
| `keytype` | 主键类型 | `keytype:string` |
| `index` | 索引字段 | `index:Type,Group` |
| `extends` | 继承表 | `extends:BaseConfig` |
| `fieldrow` | 字段行号 | `fieldrow:2` |
| `typerow` | 类型行号 | `typerow:3` |
| `arrayseparator` | 数组分隔符 | `arrayseparator:|` |
| `objectseparator` | 对象分隔符 | `objectseparator:,` |
| `field_keymap` | 字段映射 | `field_keymap:true` |

- `#comment` / `#setting` 行位置不固定（扫描到即识别）
- 任何以 `#` 开头的行都会被跳过

### 支持的类型

| 类型 | 示例 |
|------|------|
| 基础类型 | `int`, `float`, `string`, `bool`, `long` |
| Unity 类型 | `Vector2`, `Vector3`, `Color` |
| 数组 | `int[]`, `string[]` |
| 枚举 | `ItemType` |
| 引用 | `@ItemConfig` |

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
| `GetAllConfig<T>()` | 获取全部配置（可能分配；可受缓存影响） |
| `GetAllConfig<T, TKey>()` | 获取全部配置（无分配版本） |
| `GetByIndex<T>(indexName, value)` | 按索引查询（可能分配；可受缓存影响） |
| `GetByIndex<T, TKey>(indexName, value)` | 按索引查询（无分配版本） |
| `GetTable<T>()` | 获取表实例 |
| `UseQueryCache` | 查询缓存开关（默认 true） |

## 扩展

### 自定义类型解析器

```csharp
// 需要放在 Runtime 程序集下，且有无参构造
[TypeParserPlugin("MyType")]
public sealed class MyTypeParser : ITypeParser
{
    public string CSharpTypeName => "MyNamespace.MyType";
    public bool IsValueType => true;
    public string DefaultValueExpression => "default";

    public object Parse(string value, string separator)
    {
        if (string.IsNullOrEmpty(value))
            return default(MyType);

        var sep = TypeParserUtil.NormalizeObjectSeparator(separator);
        var parts = value.Split(sep[0]);
        var x = parts.Length > 0 ? float.Parse(parts[0]) : 0f;
        var y = parts.Length > 1 ? float.Parse(parts[1]) : 0f;
        return new MyType(x, y);
    }

    public string GenerateBinaryReadCode(string readerExpr)
        => $"new MyNamespace.MyType({readerExpr}.ReadSingle(), {readerExpr}.ReadSingle())";

    public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
        => $"{writerExpr}.Write({valueExpr}.X); {writerExpr}.Write({valueExpr}.Y)";

    public void Serialize(IValueWriter writer, string value, string arraySep, string objectSep)
    {
        var v = (MyType)Parse(value, TypeParserUtil.NormalizeObjectSeparator(objectSep));
        writer.BeginObject();
        writer.WritePropertyName("x");
        writer.WriteFloat(v.X);
        writer.WritePropertyName("y");
        writer.WriteFloat(v.Y);
        writer.EndObject();
    }
}
```

### 自定义数据格式

```csharp
[ConfigFormatPlugin("json")]
public sealed class JsonFormatEditor : IConfigFormat
{
    private readonly JsonConfigDataSerializer _serializer = new();

    // 编辑器导出（写入 json/bytes 到 outputPath）
    public void Serialize(ConvertContext context, string outputPath)
    {
        _serializer.Serialize(context, outputPath);
    }

    // 代码生成（可按需生成 TableRegistry / Loader）
    public void Generate(ConvertContext context, string outputPath, string codeNamespace)
    {
        ConfigCodeGenerator.Generate(context, outputPath, codeNamespace);
        var bootstrap = RuntimeBootstrapGenerator.Generate(
            codeNamespace,
            "Azcel.JsonConfigTableLoader.Instance",
            context.Tables);
        File.WriteAllText(Path.Combine(outputPath, "TableRegistry.cs"), bootstrap, Encoding.UTF8);
    }
}
```

> `JsonConfigDataSerializer` 需要你自行实现（遍历 `context.Tables/Enums/Globals` 写出文件）。  
> `IConfigFormat` 为编辑器插件，请放在 Editor 程序集中。  
> 自定义格式通常还需要一个运行时加载器，实现 `IConfigTableLoader` 并在启动时设置：  
> `azcel.SetTableLoader(JsonConfigTableLoader.Instance);`

## 转换流程

```
Excel → Parse → Merge → Inheritance → Reference → Validation → CodeGen → Export
```

## 性能结果（示例）

性能测试网格（100k loops / Editor / rows=5000）

| 测试项 | 次数 | 耗时 | 吞吐 | GC |
|---|---:|---:|---:|---:|
| GetAllConfig (cache on) | 100000 | 21 ms | 4.76 M/s | 8.6 MB |
| GetAllConfig (cache off) | 100000 | 34.6 s | 2.89 K/s | 70.6 MB |
| GetByIndex (cache on) | 100000 | 31 ms | 3.23 M/s | 8.6 MB |
| GetByIndex (cache off) | 100000 | 7.11 s | 14.1 K/s | 161.5 MB |
| GetAllConfig | 100000 | 22 ms | 4.55 M/s | 8.7 MB |
| GetAllConfigNoAlloc | 100000 | 7 ms | 14.3 M/s | 0 B |
| GetConfig | 100000 | 16 ms | 6.25 M/s | 0 B |
| TryGetConfig | 100000 | 17 ms | 5.88 M/s | 0 B |
| GetByIndex | 100000 | 33 ms | 3.03 M/s | 8.6 MB |

## License

MIT License
