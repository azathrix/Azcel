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
        public static string Generate(TableDefinition table, string ns)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// 此文件由 Azcel 自动生成，请勿手动修改");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.IO;");
            sb.AppendLine("using Azcel;");
            sb.AppendLine("using Azathrix.Framework.Core;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            // 生成配置类
            GenerateConfigClass(sb, table, ns);

            sb.AppendLine("}");

            return sb.ToString();
        }

        private static void GenerateConfigClass(StringBuilder sb, TableDefinition table, string ns)
        {
            var keyType = GetCSharpType(table.KeyType);
            var keyField = table.Fields.FirstOrDefault(f => f.IsKey) ?? table.Fields.FirstOrDefault();
            var keyFieldName = keyField?.Name ?? "Id";
            var keyIsId = string.Equals(keyFieldName, "Id", System.StringComparison.OrdinalIgnoreCase);

            sb.AppendLine($"    public class {table.Name} : ConfigBase<{keyType}>");
            sb.AppendLine("    {");
            sb.AppendLine($"        public override string ConfigName => \"{table.Name}\";");
            sb.AppendLine();

            if (table.FieldKeymap)
            {
                sb.AppendLine("        private Dictionary<string, object> _fieldKeymap;");
                sb.AppendLine("        private bool _fieldKeymapBuilt;");
                sb.AppendLine();
            }

            foreach (var field in table.Fields)
            {
                if (IsSkipped(field))
                    continue;

                var isKeyField = keyField != null && field.Name == keyFieldName;
                if (field.IsTableRef)
                {
                    var idFieldName = $"{field.Name}Id";
                    if (!string.IsNullOrEmpty(field.Comment))
                        sb.AppendLine($"        /// <summary>{field.Comment}</summary>");
                    if (table.IndexFields.Contains(field.Name))
                        sb.AppendLine("        [ConfigIndex]");
                    sb.AppendLine($"        private {field.RefTableName} _{field.Name}Cache;");
                    sb.AppendLine($"        private bool _{field.Name}CacheReady;");
                    sb.AppendLine($"        public int {idFieldName} {{ get; private set; }}");
                    sb.AppendLine($"        public {field.RefTableName} {field.Name}");
                    sb.AppendLine("        {");
                    sb.AppendLine("            get");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                if (!_{field.Name}CacheReady)");
                    sb.AppendLine("                {");
                    sb.AppendLine($"                    _{field.Name}CacheReady = true;");
                    sb.AppendLine($"                    var table = AzathrixFramework.GetSystem<AzcelSystem>()?.GetTable<{field.RefTableName}Table>();");
                    sb.AppendLine($"                    _{field.Name}Cache = table?.GetById({idFieldName});");
                    sb.AppendLine("                }");
                    sb.AppendLine($"                return _{field.Name}Cache;");
                    sb.AppendLine("            }");
                    sb.AppendLine("        }");
                }
                else
                {
                    var csharpType = GetCSharpType(field, ns);
                    if (!isKeyField || !keyIsId)
                    {
                        if (!string.IsNullOrEmpty(field.Comment))
                            sb.AppendLine($"        /// <summary>{field.Comment}</summary>");
                        if (table.IndexFields.Contains(field.Name))
                            sb.AppendLine("        [ConfigIndex]");
                        sb.AppendLine($"        public {csharpType} {field.Name} {{ get; private set; }}");
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine("        public override void Deserialize(BinaryReader reader)");
            sb.AppendLine("        {");
            foreach (var field in table.Fields)
            {
                if (IsSkipped(field))
                    continue;

                var readCode = GetBinaryReadCode(field);
                var isKeyField = keyField != null && field.Name == keyFieldName;
                if (field.IsTableRef)
                {
                    sb.AppendLine($"            {field.Name}Id = reader.ReadInt32();");
                    if (isKeyField)
                        sb.AppendLine($"            Id = {field.Name}Id;");
                }
                else if (isKeyField && keyIsId)
                {
                    sb.AppendLine($"            Id = {readCode};");
                }
                else
                {
                    sb.AppendLine($"            {field.Name} = {readCode};");
                }
            }

            if (keyField != null && !(keyField.IsTableRef && string.Equals(keyFieldName, "Id", System.StringComparison.OrdinalIgnoreCase)))
            {
                if (!keyIsId && !IsSkipped(keyField))
                {
                    if (keyField.IsTableRef)
                        sb.AppendLine($"            Id = {keyFieldName}Id;");
                    else
                        sb.AppendLine($"            Id = {keyFieldName};");
                }
            }

            sb.AppendLine("        }");

            if (!table.FieldKeymap)
            {
                sb.AppendLine();
                sb.AppendLine("        protected override bool TryGetValueInternal<T>(string fieldName, out T value)");
                sb.AppendLine("        {");
                sb.AppendLine("            value = default;");
                sb.AppendLine("            if (string.IsNullOrEmpty(fieldName))");
                sb.AppendLine("                return false;");
                sb.AppendLine();

                foreach (var field in table.Fields)
                {
                    if (IsSkipped(field))
                        continue;

                    var isKeyField = keyField != null && field.Name == keyFieldName;
                    string valueExpr;
                    if (field.IsTableRef)
                        valueExpr = $"{field.Name}Id";
                    else if (isKeyField && keyIsId)
                        valueExpr = "Id";
                    else
                        valueExpr = field.Name;

                    sb.AppendLine($"            if (string.Equals(fieldName, \"{field.Name}\", StringComparison.OrdinalIgnoreCase))");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                return TryConvertValue({valueExpr}, out value);");
                    sb.AppendLine("            }");
                }

                sb.AppendLine();
                sb.AppendLine("            return false;");
                sb.AppendLine("        }");
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("        protected override bool TryGetValueInternal<T>(string fieldName, out T value)");
                sb.AppendLine("        {");
                sb.AppendLine("            value = default;");
                sb.AppendLine("            if (string.IsNullOrEmpty(fieldName))");
                sb.AppendLine("                return false;");
                sb.AppendLine();
                sb.AppendLine("            EnsureFieldKeymap();");
                sb.AppendLine("            if (_fieldKeymap == null || !_fieldKeymap.TryGetValue(fieldName, out var raw))");
                sb.AppendLine("                return false;");
                sb.AppendLine();
                sb.AppendLine("            return TryConvertValue(raw, out value);");
                sb.AppendLine("        }");
                sb.AppendLine();
                sb.AppendLine("        private void EnsureFieldKeymap()");
                sb.AppendLine("        {");
                sb.AppendLine("            if (_fieldKeymapBuilt)");
                sb.AppendLine("                return;");
                sb.AppendLine();
                sb.AppendLine("            _fieldKeymapBuilt = true;");
                sb.AppendLine("            _fieldKeymap = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);");

                foreach (var field in table.Fields)
                {
                    if (IsSkipped(field))
                        continue;

                    var isKeyField = keyField != null && field.Name == keyFieldName;
                    string valueExpr;
                    if (field.IsTableRef)
                        valueExpr = $"{field.Name}Id";
                    else if (isKeyField && keyIsId)
                        valueExpr = "Id";
                    else
                        valueExpr = field.Name;

                    sb.AppendLine($"            _fieldKeymap[\"{field.Name}\"] = {valueExpr};");
                }

                sb.AppendLine("        }");
            }

            sb.AppendLine("    }");

            sb.AppendLine();
            sb.AppendLine($"    public sealed class {table.Name}Table : ConfigTable<{table.Name}, {keyType}>");
            sb.AppendLine("    {");
            if (table.IndexFields.Count > 0)
            {
                sb.AppendLine("        protected override List<IndexAccessor> CreateIndexAccessors()");
                sb.AppendLine("        {");
                sb.AppendLine($"            var list = new List<IndexAccessor>({table.IndexFields.Count});");
                foreach (var field in table.Fields)
                {
                    if (IsSkipped(field))
                        continue;

                    if (!table.IndexFields.Contains(field.Name))
                        continue;

                    var isKeyField = keyField != null && field.Name == keyFieldName;
                    var accessorName = field.Name;
                    string valueTypeExpr;
                    string getterExpr;

                    if (field.IsTableRef)
                    {
                        valueTypeExpr = "typeof(int)";
                        getterExpr = $"config => config.{field.Name}Id";
                    }
                    else if (isKeyField && keyIsId)
                    {
                        valueTypeExpr = $"typeof({keyType})";
                        getterExpr = "config => config.Id";
                    }
                    else
                    {
                        var csharpType = GetCSharpType(field, ns);
                        valueTypeExpr = $"typeof({csharpType})";
                        getterExpr = $"config => config.{field.Name}";
                    }

                    sb.AppendLine($"            list.Add(new IndexAccessor(\"{accessorName}\", {valueTypeExpr}, {getterExpr}));");
                }
                sb.AppendLine("            return list;");
                sb.AppendLine("        }");
            }
            sb.AppendLine("    }");
        }

        private static string GetCSharpType(FieldDefinition field, string ns)
        {
            if (field.IsEnumRef)
                return field.RefEnumName;

            if (field.IsTableRef)
                return field.RefTableName;

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

        private static bool IsSkipped(FieldDefinition field)
        {
            if (field == null)
                return false;

            if (!field.Options.TryGetValue("skip", out var value))
                return false;

            if (string.IsNullOrEmpty(value))
                return true;

            return value.Equals("true", System.StringComparison.OrdinalIgnoreCase)
                   || value.Equals("1", System.StringComparison.OrdinalIgnoreCase)
                   || value.Equals("yes", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
