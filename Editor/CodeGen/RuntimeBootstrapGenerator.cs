using System;
using System.Collections.Generic;
using System.Text;

namespace Azcel.Editor
{
    /// <summary>
    /// 运行时引导代码生成器（设置 TableLoader）
    /// </summary>
    public static class RuntimeBootstrapGenerator
    {
        public static string Generate(string codeNamespace, string loaderExpression, IReadOnlyList<TableDefinition> tables)
        {
            var ns = string.IsNullOrEmpty(codeNamespace) ? "Game.Tables" : codeNamespace.Trim();
            var sb = new StringBuilder();

            sb.AppendLine("namespace " + ns);
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Azcel 运行时引导（自动应用 TableLoader）");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static class TableRegistry");
            sb.AppendLine("    {");
            sb.AppendLine("        public static void Apply(Azcel.AzcelSystem system)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (system == null)");
            sb.AppendLine("                return;");
            sb.AppendLine();
            var loaderByType = IsTypeExpression(loaderExpression, out var loaderType);

            if (loaderByType)
            {
                sb.AppendLine("            object CreateInstance(string typeName)");
                sb.AppendLine("            {");
                sb.AppendLine("                if (string.IsNullOrEmpty(typeName))");
                sb.AppendLine("                    return null;");
                sb.AppendLine("                var type = System.Type.GetType(typeName);");
                sb.AppendLine("                if (type == null)");
                sb.AppendLine("                {");
                sb.AppendLine("                    var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();");
                sb.AppendLine("                    for (int i = 0; i < assemblies.Length; i++)");
                sb.AppendLine("                    {");
                sb.AppendLine("                        type = assemblies[i].GetType(typeName);");
                sb.AppendLine("                        if (type != null)");
                sb.AppendLine("                            break;");
                sb.AppendLine("                    }");
                sb.AppendLine("                }");
                sb.AppendLine("                return type != null ? System.Activator.CreateInstance(type) : null;");
                sb.AppendLine("            }");
                sb.AppendLine();
            }

            if (loaderByType)
            {
                sb.AppendLine("            if (system.TableLoader == null)");
                sb.AppendLine($"                if (CreateInstance(\"{Escape(loaderType)}\") is Azcel.IConfigTableLoader loader)");
                sb.AppendLine("                    system.SetTableLoader(loader);");
                sb.AppendLine();
            }
            else if (!string.IsNullOrEmpty(loaderExpression))
            {
                sb.AppendLine("            if (system.TableLoader == null)");
                sb.AppendLine($"                system.SetTableLoader({loaderExpression});");
                sb.AppendLine();
            }

            if (tables != null)
            {
                for (int i = 0; i < tables.Count; i++)
                {
                    var table = tables[i];
                    if (table == null || string.IsNullOrEmpty(table.Name))
                        continue;
                    sb.AppendLine($"            system.RegisterTable(new {table.Name}Table());");
                }
            }
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static bool IsTypeExpression(string expr, out string typeName)
        {
            typeName = null;
            if (string.IsNullOrEmpty(expr))
                return false;

            if (!expr.StartsWith("type:", StringComparison.OrdinalIgnoreCase))
                return false;

            typeName = expr.Substring(5).Trim();
            return !string.IsNullOrEmpty(typeName);
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
