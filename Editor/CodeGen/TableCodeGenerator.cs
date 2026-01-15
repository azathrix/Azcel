using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Azcel.Editor
{
    /// <summary>
    /// 配置代码生成器
    /// </summary>
    public static class TableCodeGenerator
    {
        public static string Generate(TableDefinition table, string ns, DataFormat format)
        {
            var sb = new StringBuilder();
            var rowName = $"{table.Name}Row";

            sb.AppendLine("// 此文件由 Azcel 自动生成，请勿手动修改");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.IO;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine("using Azcel;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");

            // 生成行结构体
            GenerateRowStruct(sb, table, rowName, ns);

            sb.AppendLine();

            // 生成配置类
            GenerateConfigClass(sb, table, rowName, ns, format);

            sb.AppendLine("}");

            return sb.ToString();
        }

        private static void GenerateRowStruct(StringBuilder sb, TableDefinition table, string rowName, string ns)
        {
            sb.AppendLine($"    public struct {rowName}");
            sb.AppendLine("    {");

            foreach (var field in table.Fields)
            {
                var csharpType = GetCSharpType(field, ns);

                if (!string.IsNullOrEmpty(field.Comment))
                    sb.AppendLine($"        /// <summary>{field.Comment}</summary>");

                // 表引用字段：存储ID，提供属性访问
                if (field.IsTableRef)
                {
                    sb.AppendLine($"        private int _{field.Name}Id;");
                    sb.AppendLine($"        public ref {field.RefTableName}Row {field.Name} => ref {field.RefTableName}Config.Get(_{field.Name}Id);");
                }
                else
                {
                    sb.AppendLine($"        public {csharpType} {field.Name};");
                }
            }

            sb.AppendLine("    }");
        }

        private static void GenerateConfigClass(StringBuilder sb, TableDefinition table, string rowName, string ns, DataFormat format)
        {
            var keyType = GetCSharpType(table.KeyType);

            sb.AppendLine($"    public class {table.Name}Config : ConfigBase<{rowName}, {keyType}>");
            sb.AppendLine("    {");
            sb.AppendLine($"        public override string ConfigName => \"{table.Name}\";");
            sb.AppendLine();

            // 静态访问
            sb.AppendLine($"        public static {table.Name}Config I => Azathrix.Framework.Core.AzathrixFramework.GetSystem<AzcelSystem>()?.Get<{table.Name}Config>();");
            sb.AppendLine();

            // 静态Get方法
            sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"        public static ref {rowName} Get({keyType} key) => ref I.GetByKeyRef(key);");
            sb.AppendLine();

            // 生成索引字段的查询方法
            foreach (var indexField in table.IndexFields)
            {
                var field = table.Fields.FirstOrDefault(f => f.Name == indexField);
                if (field == null) continue;

                var indexType = GetCSharpType(field, ns);
                sb.AppendLine($"        private Dictionary<{indexType}, List<int>> _{indexField}Index;");
                sb.AppendLine();
                sb.AppendLine($"        public IReadOnlyList<{rowName}> GetBy{indexField}({indexType} value)");
                sb.AppendLine("        {");
                sb.AppendLine($"            if (_{indexField}Index == null) return Array.Empty<{rowName}>();");
                sb.AppendLine($"            if (!_{indexField}Index.TryGetValue(value, out var indices)) return Array.Empty<{rowName}>();");
                sb.AppendLine($"            var result = new {rowName}[indices.Count];");
                sb.AppendLine("            for (int i = 0; i < indices.Count; i++)");
                sb.AppendLine("                result[i] = _rows[indices[i]];");
                sb.AppendLine("            return result;");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            // ParseData - 由生成代码实现高性能解析
            sb.AppendLine("        public override void ParseData(byte[] data)");
            sb.AppendLine("        {");
            sb.AppendLine("            using var reader = new BinaryReader(new MemoryStream(data));");
            sb.AppendLine("            var count = reader.ReadInt32();");
            sb.AppendLine($"            var rows = new {rowName}[count];");
            sb.AppendLine($"            var keyIndex = new Dictionary<{keyType}, int>(count);");
            sb.AppendLine();
            sb.AppendLine("            for (int i = 0; i < count; i++)");
            sb.AppendLine("            {");
            sb.AppendLine($"                var row = new {rowName}();");

            foreach (var field in table.Fields)
            {
                var readCode = GetBinaryReadCode(field);
                if (field.IsTableRef)
                    sb.AppendLine($"                row._{field.Name}Id = reader.ReadInt32();");
                else
                    sb.AppendLine($"                row.{field.Name} = {readCode};");
            }

            var keyField = table.Fields.FirstOrDefault(f => f.IsKey) ?? table.Fields.FirstOrDefault();
            if (keyField != null)
            {
                if (keyField.IsTableRef)
                    sb.AppendLine($"                keyIndex[row._{keyField.Name}Id] = i;");
                else
                    sb.AppendLine($"                keyIndex[row.{keyField.Name}] = i;");
            }

            sb.AppendLine("                rows[i] = row;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            SetData(rows, keyIndex);");
            sb.AppendLine("        }");

            // OnDataLoaded - 构建索引
            if (table.IndexFields.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("        protected override void OnDataLoaded()");
                sb.AppendLine("        {");

                foreach (var indexField in table.IndexFields)
                {
                    var field = table.Fields.FirstOrDefault(f => f.Name == indexField);
                    if (field == null) continue;

                    var indexType = GetCSharpType(field, ns);
                    sb.AppendLine($"            _{indexField}Index = new Dictionary<{indexType}, List<int>>();");
                    sb.AppendLine("            for (int i = 0; i < _rows.Length; i++)");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                var key = _rows[i].{indexField};");
                    sb.AppendLine($"                if (!_{indexField}Index.TryGetValue(key, out var list))");
                    sb.AppendLine($"                    _{indexField}Index[key] = list = new List<int>();");
                    sb.AppendLine("                list.Add(i);");
                    sb.AppendLine("            }");
                }

                sb.AppendLine("        }");
            }

            sb.AppendLine("    }");
        }

        public static string GenerateRegister(List<TableDefinition> tables, List<GlobalDefinition> globals, string ns, DataFormat format)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// 此文件由 Azcel 自动生成，请勿手动修改");
            sb.AppendLine();
            sb.AppendLine("using Azcel;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine("    public static class ConfigRegister");
            sb.AppendLine("    {");
            sb.AppendLine("        public static void RegisterAll(AzcelSystem system)");
            sb.AppendLine("        {");

            foreach (var table in tables)
            {
                sb.AppendLine($"            system.RegisterConfig(new {table.Name}Config());");
            }

            foreach (var global in globals)
            {
                sb.AppendLine($"            system.RegisterGlobal(new GlobalConfig{global.Name}());");
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GetCSharpType(FieldDefinition field, string ns)
        {
            if (field.IsEnumRef)
                return field.RefEnumName;

            if (field.IsTableRef)
                return $"{field.RefTableName}Row";

            return GetCSharpType(field.Type);
        }

        private static string GetCSharpType(string type)
        {
            var parser = TypeRegistry.Get(type);
            return parser?.CSharpTypeName ?? "string";
        }

        private static string GetBinaryReadCode(FieldDefinition field)
        {
            if (field.IsEnumRef)
                return $"({field.RefEnumName})reader.ReadInt32()";

            var parser = TypeRegistry.Get(field.Type);
            return parser?.GenerateBinaryReadCode("reader") ?? "reader.ReadString()";
        }
    }
}
