using System;

namespace Azcel
{
    /// <summary>
    /// 类型解析器辅助
    /// </summary>
    public static class TypeParserUtil
    {
        public static string NormalizeArraySeparator(string separator)
        {
            if (!string.IsNullOrEmpty(separator))
                return separator;
            return AzcelSettings.Instance?.arraySeparator ?? "|";
        }

        public static string NormalizeObjectSeparator(string separator)
        {
            if (!string.IsNullOrEmpty(separator))
                return separator;
            return AzcelSettings.Instance?.objectSeparator ?? ",";
        }

        public static void SplitKeyValue(string value, string separator, out string key, out string val)
        {
            var idx = value.IndexOf(separator, StringComparison.Ordinal);
            if (idx < 0)
            {
                key = value ?? "";
                val = "";
                return;
            }

            key = value[..idx];
            val = value[(idx + separator.Length)..];
        }
    }
}
