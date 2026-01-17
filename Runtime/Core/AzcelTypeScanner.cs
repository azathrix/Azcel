using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Azcel
{
    /// <summary>
    /// 类型扫描辅助（运行时）
    /// </summary>
    internal static class AzcelTypeScanner
    {
        private static readonly string[] ExcludePrefixes =
        {
            "System", "Microsoft", "Unity", "mscorlib", "netstandard", "Mono", "nunit"
        };

        private static readonly string[] ManualIncludePrefixes = { };
        private static readonly string[] ManualExcludePrefixes = { };
        private static readonly string[] ManualExcludeNames = { };

        public static IEnumerable<Assembly> GetAssemblies()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(ShouldScanAssembly);
        }

        public static bool ShouldScanAssembly(Assembly assembly)
        {
            var name = assembly.GetName().Name;

            if (name.Contains("-") && name.Length > 50)
                return false;

            if (ExcludePrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                return false;

            if (ManualIncludePrefixes.Length > 0 &&
                !ManualIncludePrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                return false;

            if (ManualExcludePrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                return false;

            if (ManualExcludeNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
                return false;

            return true;
        }

        public static IEnumerable<Type> GetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }
    }
}
