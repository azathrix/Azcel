# Changelog

## [1.0.0] - 2026-01-19

**轻量级 Excel 数据表转换和配置管理器**

### 核心功能

- **Excel 解析** - 支持多 Excel 文件、多 Sheet 解析
- **代码生成** - 自动生成强类型 C# 配置类、表类、枚举
- **数据导出** - 支持 Binary 格式（可扩展 JSON 等）
- **运行时加载** - 零 GC 的配置查询 API

### 特性

- 表继承 (Inheritance)
- 表引用解析 (Reference Resolve)
- 多表合并 (Table Merge)
- 索引查询支持
- 自定义类型解析器 (TypeParser)
- 可扩展的数据格式插件

### 转换流程

```
Excel → Parse → Merge → Inheritance → Reference → CodeGen → Export
```

### 运行时 API

```csharp
// 获取单条配置
var config = azcel.GetConfig<ItemConfig>(1001);

// 获取全部配置
var all = azcel.GetAllConfig<ItemConfig>();

// 按索引查询
var weapons = azcel.GetByIndex<ItemConfig>("Type", ItemType.Weapon);
```

### 依赖

- `com.azathrix.framework` 1.0.0
- `com.azathrix.unitask` 2.5.10
