using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Azcel
{
    /// <summary>
    /// 配置基类（行级）
    /// </summary>
    public abstract class ConfigBase
    {
        private static readonly Dictionary<Type, Dictionary<string, MemberInfo>> MemberCache = new();
        private static readonly object MemberCacheLock = new();

        /// <summary>
        /// 配置名
        /// </summary>
        public abstract string ConfigName { get; }

        /// <summary>
        /// 按字段名获取值（用于 field_keymap 或反射回退）
        /// </summary>
        public T GetValue<T>(string fieldName)
        {
            return TryGetValue(fieldName, out T value) ? value : default;
        }

        /// <summary>
        /// 按字段名尝试获取值
        /// </summary>
        public bool TryGetValue<T>(string fieldName, out T value)
        {
            value = default;
            if (string.IsNullOrEmpty(fieldName))
                return false;

            return TryGetValueInternal(fieldName, out value);
        }

        /// <summary>
        /// 由子类覆盖以提供更高效的取值方式（如字段字典）
        /// </summary>
        protected virtual bool TryGetValueInternal<T>(string fieldName, out T value)
        {
            value = default;
            var type = GetType();
            var members = GetMemberMap(type);
            if (!members.TryGetValue(fieldName, out var member))
                return false;

            object raw = null;
            if (member is PropertyInfo prop)
            {
                if (prop.GetMethod == null)
                    return false;
                raw = prop.GetValue(this);
            }
            else if (member is FieldInfo field)
            {
                raw = field.GetValue(this);
            }

            if (raw == null)
                return false;

            return TryConvertValue(raw, out value);
        }

        private static Dictionary<string, MemberInfo> GetMemberMap(Type type)
        {
            lock (MemberCacheLock)
            {
                if (MemberCache.TryGetValue(type, out var cached))
                    return cached;

                var map = new Dictionary<string, MemberInfo>(StringComparer.OrdinalIgnoreCase);
                var members = type.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var member in members)
                {
                    if (member.MemberType != MemberTypes.Field && member.MemberType != MemberTypes.Property)
                        continue;

                    if (member is PropertyInfo prop && prop.GetMethod == null)
                        continue;

                    if (!map.ContainsKey(member.Name))
                        map[member.Name] = member;
                }

                MemberCache[type] = map;
                return map;
            }
        }

        protected static bool TryConvertValue<T>(object raw, out T value)
        {
            value = default;
            if (raw == null)
                return false;

            if (raw is T typed)
            {
                value = typed;
                return true;
            }

            var targetType = typeof(T);
            try
            {
                if (targetType.IsEnum)
                {
                    if (raw is string str)
                    {
                        value = (T)Enum.Parse(targetType, str, true);
                        return true;
                    }

                    var underlying = Convert.ChangeType(raw, Enum.GetUnderlyingType(targetType));
                    value = (T)Enum.ToObject(targetType, underlying);
                    return true;
                }

                if (targetType == typeof(Guid))
                {
                    if (raw is Guid guid)
                    {
                        value = (T)(object)guid;
                        return true;
                    }

                    if (raw is string guidStr && Guid.TryParse(guidStr, out var parsedGuid))
                    {
                        value = (T)(object)parsedGuid;
                        return true;
                    }
                }

                value = (T)Convert.ChangeType(raw, targetType);
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }
    }

    /// <summary>
    /// 行级配置基类（以主键定位）
    /// </summary>
    public abstract class ConfigBase<TKey> : ConfigBase
    {
        /// <summary>
        /// 主键
        /// </summary>
        public TKey Id { get; protected set; }

        /// <summary>
        /// 反序列化（由生成代码实现）
        /// </summary>
        public abstract void Deserialize(BinaryReader reader);
    }
}
