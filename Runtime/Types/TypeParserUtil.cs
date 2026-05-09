using System;

namespace Azcel
{
    /// <summary>
    /// 类型解析器辅助
    /// </summary>
    public static class TypeParserUtil
    {
        public static string[] Split(string value, string separator)
        {
            if (string.IsNullOrEmpty(value))
                return Array.Empty<string>();

            if (string.IsNullOrEmpty(separator))
                return new[] { value };

            return value.Split(new[] { separator }, StringSplitOptions.None);
        }

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

    }
}
