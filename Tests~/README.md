# Azcel Tests Sample

本示例用于验证 **Azcel 完整流程（转换 + 运行加载 + API 调用）**。

## 内容
- `TestEnvironment/Excel/AzcelTestWorkbook.xlsx`：测试用 Excel（表/枚举/全局）
- `Runtime/TestTables.cs`：测试用配置类与 `TableRegistry`
- `Editor/AzcelFullFlowTests.cs`：转换 + 运行加载 + API 覆盖测试

## 使用方式
1. 在 Package Manager 中导入本 Sample。
2. 打开 Unity Test Runner（EditMode）运行 `AzcelFullFlowTests`。

> 测试会将数据输出到 `Assets/Resources/AzcelTestData`，仅用于测试环境。
