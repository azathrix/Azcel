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
                AppendComment(sb, value.Comment, 8);
                sb.AppendLine($"        {value.Name} = {value.Value},");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static void AppendComment(StringBuilder sb, string comment, int indent)
        {
            if (string.IsNullOrEmpty(comment))
                return;

            var pad = new string(' ', indent);
            sb.AppendLine($"{pad}/// <summary>");
            var lines = comment.Replace("\r", "").Split('\n');
            foreach (var line in lines)
            {
                var text = EscapeXml(line);
                sb.AppendLine($"{pad}/// {text}");
            }
            sb.AppendLine($"{pad}/// </summary>");
        }

        private static string EscapeXml(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }
    }
}
