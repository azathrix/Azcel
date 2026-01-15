using System.Text;

namespace Azcel.Editor
{
    /// <summary>
    /// 全局配置代码生成器
    /// </summary>
    public static class GlobalCodeGenerator
    {
        public static string Generate(GlobalDefinition globalDef, string ns, DataFormat format)
        {
            var sb = new StringBuilder();
            var className = $"GlobalConfig{globalDef.Name}";

            sb.AppendLine("// 此文件由 Azcel 自动生成，请勿手动修改");
            sb.AppendLine();
            sb.AppendLine("using System.IO;");
            sb.AppendLine("using Azcel;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    public class {className} : GlobalConfig<{className}>");
            sb.AppendLine("    {");
            sb.AppendLine($"        public override string ConfigName => \"{className}\";");
            sb.AppendLine();

            // 生成强类型属性
            foreach (var value in globalDef.Values)
            {
                var csharpType = GetCSharpType(value.Type);
                sb.AppendLine($"        public {csharpType} {value.Key} => Get<{csharpType}>(\"{value.Key}\");");
            }

            sb.AppendLine();

            // ParseData - 由生成代码实现高性能解析
            sb.AppendLine("        public override void ParseData(byte[] data)");
            sb.AppendLine("        {");
            sb.AppendLine("            using var reader = new BinaryReader(new MemoryStream(data));");
            sb.AppendLine("            var count = reader.ReadInt32();");
            sb.AppendLine("            var values = new System.Collections.Generic.Dictionary<string, object>();");
            sb.AppendLine("            for (int i = 0; i < count; i++)");
            sb.AppendLine("            {");
            sb.AppendLine("                var key = reader.ReadString();");
            sb.AppendLine("                var type = reader.ReadString();");
            sb.AppendLine("                var valueStr = reader.ReadString();");
            sb.AppendLine("                values[key] = ParseValue(type, valueStr);");
            sb.AppendLine("            }");
            sb.AppendLine("            SetData(values);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private static object ParseValue(string type, string value)");
            sb.AppendLine("        {");
            sb.AppendLine("            return type?.ToLower() switch");
            sb.AppendLine("            {");
            sb.AppendLine("                \"int\" => int.TryParse(value, out var i) ? i : 0,");
            sb.AppendLine("                \"long\" => long.TryParse(value, out var l) ? l : 0L,");
            sb.AppendLine("                \"float\" => float.TryParse(value, out var f) ? f : 0f,");
            sb.AppendLine("                \"double\" => double.TryParse(value, out var d) ? d : 0d,");
            sb.AppendLine("                \"bool\" => value == \"1\" || value?.ToLower() == \"true\",");
            sb.AppendLine("                _ => value ?? \"\"");
            sb.AppendLine("            };");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GetCSharpType(string type)
        {
            return type?.ToLower() switch
            {
                "int" => "int",
                "long" => "long",
                "float" => "float",
                "double" => "double",
                "bool" => "bool",
                _ => "string"
            };
        }
    }
}
