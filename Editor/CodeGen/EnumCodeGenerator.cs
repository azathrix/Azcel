using System.Text;

namespace Azcel.Editor
{
    /// <summary>
    /// 枚举代码生成器
    /// </summary>
    public static class EnumCodeGenerator
    {
        public static string Generate(EnumDefinition enumDef, string ns)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// 此文件由 Azcel 自动生成，请勿手动修改");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    public enum {enumDef.Name}");
            sb.AppendLine("    {");

            foreach (var value in enumDef.Values)
            {
                if (!string.IsNullOrEmpty(value.Comment))
                    sb.AppendLine($"        /// <summary>{value.Comment}</summary>");
                sb.AppendLine($"        {value.Name} = {value.Value},");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}
