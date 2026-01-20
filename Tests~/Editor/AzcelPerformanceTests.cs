using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Azathrix.Framework.Core;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Azcel.Sample.Tests
{
    public class AzcelPerformanceTests
    {
        private sealed class PerfTestConfig : ConfigBase<int>
        {
            public override string ConfigName => "PerfTest";

            public int Type { get; private set; }
            [ConfigIndex]
            public string Group { get; private set; }
            public string Name { get; private set; }

            public override void Deserialize(BinaryReader reader)
            {
                Id = reader.ReadInt32();
                Type = reader.ReadInt32();
                Group = reader.ReadString();
                Name = reader.ReadString();
            }
        }

        private sealed class PerfTestConfigTable : ConfigTable<PerfTestConfig, int>
        {
        }

        private AzcelSettings _originalSettings;
        private SystemRuntimeManager _runtimeManager;

        private AzcelSystem _azcel;
        private IConfigTable _table;
        private List<object> _keys;
        private string _indexName;
        private object _indexValue;

        private Func<object, object> _getConfigFunc;
        private Func<object, bool> _tryGetConfigFunc;
        private Func<object> _getAllFunc;
        private Func<object> _getAllNoAllocFunc;
        private Func<string, object, object> _getByIndexFunc;

        [SetUp]
        public void SetUp()
        {
            _originalSettings = AzcelSettings.Instance;

            var settings = ScriptableObject.CreateInstance<AzcelSettings>();
            var excelDir = AzcelSampleEnvironment.ExcelDirectory;
            var testsRoot = Directory.GetParent(excelDir)?.Parent?.FullName ?? excelDir;
            settings.excelPaths = new List<string> { excelDir };
            settings.codeOutputPath = Path.Combine(testsRoot, "Generate");
            settings.dataOutputPath = Path.Combine(testsRoot, "Resources", "AzcelTestData");
            settings.codeNamespace = "Azcel.Sample";
            settings.dataFormatId = "binary";
            settings.arraySeparator = "|";
            settings.objectSeparator = ",";
            settings.defaultKeyField = "Id";
            settings.defaultKeyType = "int";
            settings.defaultFieldRow = 2;
            settings.defaultTypeRow = 3;

            AzcelSettings.SetSettings(settings);

            _runtimeManager = new SystemRuntimeManager { IsEditorMode = true };
            AzathrixFramework.SetEditorRuntimeManager(_runtimeManager);
            AzathrixFramework.MarkEditorStarted();
        }

        [TearDown]
        public void TearDown()
        {
            AzcelSettings.SetSettings(_originalSettings);
            AzathrixFramework.ResetEditorRuntime();
        }

        [UnityTest]
        public IEnumerator Stress_GetConfig_ById()
        {
            yield return EnsureReady().ToCoroutine();

            const int iterations = 100000;
            WarmupGetConfig(1000);

            ForceGC();
            var gcBefore = GC.GetTotalMemory(false);
            var gen0Before = GC.CollectionCount(0);

            var sw = Stopwatch.StartNew();
            int hit = 0;
            for (int i = 0; i < iterations; i++)
            {
                var cfg = _getConfigFunc(_keys[i % _keys.Count]);
                if (cfg != null) hit++;
            }
            sw.Stop();

            var gcAfter = GC.GetTotalMemory(false);
            var gen0After = GC.CollectionCount(0);

            UnityEngine.Debug.Log($"[AzcelPerf] GetConfig<{_table.ConfigType.Name}> x{iterations} | hit {hit} | {sw.ElapsedMilliseconds}ms | {ComputeOpsPerSecond(iterations, sw.ElapsedMilliseconds):F1} ops/s | GC {(gcAfter - gcBefore) / 1024f:F2} KB | Gen0 {gen0After - gen0Before}");
            Assert.Greater(hit, 0);
        }

        [UnityTest]
        public IEnumerator Stress_TryGetConfig_ById()
        {
            yield return EnsureReady().ToCoroutine();

            const int iterations = 100000;
            WarmupGetConfig(1000);

            ForceGC();
            var gcBefore = GC.GetTotalMemory(false);
            var gen0Before = GC.CollectionCount(0);

            var sw = Stopwatch.StartNew();
            int hit = 0;
            for (int i = 0; i < iterations; i++)
            {
                if (_tryGetConfigFunc(_keys[i % _keys.Count]))
                    hit++;
            }
            sw.Stop();

            var gcAfter = GC.GetTotalMemory(false);
            var gen0After = GC.CollectionCount(0);

            UnityEngine.Debug.Log($"[AzcelPerf] TryGetConfig<{_table.ConfigType.Name}> x{iterations} | hit {hit} | {sw.ElapsedMilliseconds}ms | {ComputeOpsPerSecond(iterations, sw.ElapsedMilliseconds):F1} ops/s | GC {(gcAfter - gcBefore) / 1024f:F2} KB | Gen0 {gen0After - gen0Before}");
            Assert.Greater(hit, 0);
        }

        [UnityTest]
        public IEnumerator Stress_GetByIndex()
        {
            yield return EnsureReady().ToCoroutine();

            if (string.IsNullOrEmpty(_indexName) || _indexValue == null)
            {
                UnityEngine.Debug.Log("[AzcelPerf] 未找到索引字段，跳过 GetByIndex 压力测试");
                yield break;
            }

            const int iterations = 100000;
            WarmupGetByIndex();

            ForceGC();
            var gcBefore = GC.GetTotalMemory(false);
            var gen0Before = GC.CollectionCount(0);

            var sw = Stopwatch.StartNew();
            int count = 0;
            for (int i = 0; i < iterations; i++)
            {
                var list = _getByIndexFunc(_indexName, _indexValue) as System.Collections.IEnumerable;
                if (list == null)
                    continue;
                if (list is System.Collections.ICollection collection)
                {
                    count += collection.Count;
                }
                else
                {
                    foreach (var _ in list)
                        count++;
                }
            }
            sw.Stop();

            var gcAfter = GC.GetTotalMemory(false);
            var gen0After = GC.CollectionCount(0);

            UnityEngine.Debug.Log($"[AzcelPerf] GetByIndex<{_table.ConfigType.Name}> index={_indexName} x{iterations} | total {count} | {sw.ElapsedMilliseconds}ms | {ComputeOpsPerSecond(iterations, sw.ElapsedMilliseconds):F1} ops/s | GC {(gcAfter - gcBefore) / 1024f:F2} KB | Gen0 {gen0After - gen0Before}");
        }

        [UnityTest]
        public IEnumerator CacheToggle_CompareQueries()
        {
            yield return EnsureReady().ToCoroutine();

            var original = _azcel.UseQueryCache;
            try
            {
                const int getAllLoops = 100000;
                var onAll = MeasureGetAll(getAllLoops, true);
                var offAll = MeasureGetAll(getAllLoops, false);
                UnityEngine.Debug.Log($"[AzcelCache] GetAllConfig<{_table.ConfigType.Name}> loops={getAllLoops} | cacheOn {onAll.ms}ms {ComputeOpsPerSecond(getAllLoops, onAll.ms):F1} ops/s {onAll.gcKb:F2}KB | cacheOff {offAll.ms}ms {ComputeOpsPerSecond(getAllLoops, offAll.ms):F1} ops/s {offAll.gcKb:F2}KB");

                if (!string.IsNullOrEmpty(_indexName) && _indexValue != null)
                {
                    const int byIndexLoops = 100000;
                    var onIndex = MeasureGetByIndex(byIndexLoops, true);
                    var offIndex = MeasureGetByIndex(byIndexLoops, false);
                    UnityEngine.Debug.Log($"[AzcelCache] GetByIndex<{_table.ConfigType.Name}> index={_indexName} loops={byIndexLoops} | cacheOn {onIndex.ms}ms {ComputeOpsPerSecond(byIndexLoops, onIndex.ms):F1} ops/s {onIndex.gcKb:F2}KB | cacheOff {offIndex.ms}ms {ComputeOpsPerSecond(byIndexLoops, offIndex.ms):F1} ops/s {offIndex.gcKb:F2}KB");
                }
                else
                {
                    UnityEngine.Debug.Log("[AzcelCache] 未找到索引字段，跳过 GetByIndex 缓存对比");
                }
            }
            finally
            {
                _azcel.UseQueryCache = original;
            }
        }

        [UnityTest]
        public IEnumerator GC_GetAllConfig_AllocVsNoAlloc()
        {
            yield return EnsureReady().ToCoroutine();

            const int loops = 100000;

            ForceGC();
            var allocBefore = GC.GetTotalMemory(false);
            var gen0Before = GC.CollectionCount(0);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < loops; i++)
                _getAllFunc();
            sw.Stop();
            var allocAfter = GC.GetTotalMemory(false);
            var gen0After = GC.CollectionCount(0);
            var timeAll = sw.ElapsedMilliseconds;

            ForceGC();
            var noAllocBefore = GC.GetTotalMemory(false);
            var gen0Before2 = GC.CollectionCount(0);
            sw.Restart();
            for (int i = 0; i < loops; i++)
                _getAllNoAllocFunc();
            sw.Stop();
            var noAllocAfter = GC.GetTotalMemory(false);
            var gen0After2 = GC.CollectionCount(0);
            var timeNoAlloc = sw.ElapsedMilliseconds;

            UnityEngine.Debug.Log($"[AzcelGC] GetAllConfig<{_table.ConfigType.Name}> loops={loops} | {timeAll}ms | {ComputeOpsPerSecond(loops, timeAll):F1} ops/s | alloc {(allocAfter - allocBefore) / 1024f:F2} KB | Gen0 {gen0After - gen0Before}");
            UnityEngine.Debug.Log($"[AzcelGC] GetAllConfigNoAlloc<{_table.ConfigType.Name}> loops={loops} | {timeNoAlloc}ms | {ComputeOpsPerSecond(loops, timeNoAlloc):F1} ops/s | alloc {(noAllocAfter - noAllocBefore) / 1024f:F2} KB | Gen0 {gen0After2 - gen0Before2}");
        }

        [UnityTest]
        public IEnumerator GC_GetConfig_ById()
        {
            yield return EnsureReady().ToCoroutine();

            const int loops = 100000;
            WarmupGetConfig(1000);

            ForceGC();
            var allocBefore = GC.GetTotalMemory(false);
            var gen0Before = GC.CollectionCount(0);

            var sw = Stopwatch.StartNew();
            int hit = 0;
            for (int i = 0; i < loops; i++)
            {
                if (_getConfigFunc(_keys[i % _keys.Count]) != null)
                    hit++;
            }
            sw.Stop();

            var allocAfter = GC.GetTotalMemory(false);
            var gen0After = GC.CollectionCount(0);

            UnityEngine.Debug.Log($"[AzcelGC] GetConfig<{_table.ConfigType.Name}> loops={loops} | {sw.ElapsedMilliseconds}ms | {ComputeOpsPerSecond(loops, sw.ElapsedMilliseconds):F1} ops/s | hit {hit} | alloc {(allocAfter - allocBefore) / 1024f:F2} KB | Gen0 {gen0After - gen0Before}");
            Assert.Greater(hit, 0);
        }

        private async UniTask EnsureReady()
        {
            if (_azcel != null && _table != null && _keys != null && _keys.Count > 0)
                return;

            await _runtimeManager.RegisterSystemAsync<AzcelSystem>();
            _azcel = AzathrixFramework.GetSystem<AzcelSystem>();
            Assert.NotNull(_azcel);

            _azcel.Clear();

            _table = new PerfTestConfigTable();

            _azcel.RegisterTable(_table);
            _table.CacheCapacity = 128;

            BuildSyntheticData(_table, 5000);

            _getConfigFunc = BuildGetConfigFunc(_azcel, _table.ConfigType);
            _tryGetConfigFunc = BuildTryGetConfigFunc(_azcel, _table.ConfigType);
            _getAllFunc = BuildGetAllFunc(_azcel, _table.ConfigType);
            _getAllNoAllocFunc = BuildGetAllNoAllocFunc(_azcel, _table.ConfigType, _table.KeyType);
            _getByIndexFunc = BuildGetByIndexFunc(_azcel, _table.ConfigType);
        }

        private static async UniTask ManualLoad(AzcelSystem azcel)
        {
            var loader = azcel.DataLoader ?? new ResourcesDataLoader();
            foreach (var table in azcel.GetAllTables())
            {
                var data = loader.Load(table.ConfigName);
                if (data != null)
                    azcel.LoadTable(table, data);
            }

            await UniTask.Yield();
        }

        private static bool HasDataFiles(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath))
                return false;

            var full = Path.GetFullPath(outputPath);
            if (!Directory.Exists(full))
                return false;

            return Directory.GetFiles(full, "*.bytes", SearchOption.AllDirectories).Length > 0;
        }

        private void BuildSyntheticData(IConfigTable table, int rowCount)
        {
            _keys = new List<object>(rowCount);
            _indexName = null;
            _indexValue = null;

            var configType = table.ConfigType;
            var indexMembers = GetIndexMembers(configType);
            var primaryIndex = PickIndexMember(indexMembers, configType);
            UnityEngine.Debug.Log($"[AzcelPerf] 使用脚本构造数据: {configType.Name}, rows={rowCount}");

            for (int i = 0; i < rowCount; i++)
            {
                var config = table.CreateInstance();
                var keyValue = CreateKeyValue(table.KeyType, i + 1);
                SetMemberValue(config, "Id", keyValue);
                _keys.Add(keyValue);

                foreach (var indexMember in indexMembers)
                {
                    if (TrySetIndexValue(config, indexMember.member, indexMember.name, i, out var usedValue))
                    {
                        if (_indexName == null && primaryIndex.name == indexMember.name)
                        {
                            _indexName = indexMember.name;
                            _indexValue = usedValue;
                        }
                    }
                }

                table.Add(config);
            }

            table.BuildIndexes();

            if (_keys.Count == 0)
                Assert.Inconclusive("未生成主键数据，无法进行性能测试");
        }

        private static (string name, MemberInfo member) PickIndexMember(List<(string name, MemberInfo member)> members, Type configType)
        {
            if (members == null || members.Count == 0)
                return (null, null);

            foreach (var m in members)
            {
                var memberType = GetMemberType(m.member);
                if (memberType == typeof(int) || memberType == typeof(long) || memberType == typeof(string))
                    return m;
            }

            return members[0];
        }

        private static object CreateKeyValue(Type keyType, int seed)
        {
            if (keyType == typeof(int))
                return seed;
            if (keyType == typeof(long))
                return (long)seed;
            if (keyType == typeof(string))
                return $"K{seed}";
            if (keyType != null && keyType.IsEnum)
            {
                var values = Enum.GetValues(keyType);
                return values.Length > 0 ? values.GetValue(seed % values.Length) : Activator.CreateInstance(keyType);
            }

            return seed;
        }

        private static bool TrySetIndexValue(object config, MemberInfo member, string indexName, int seed, out object usedValue)
        {
            usedValue = null;
            var memberType = GetMemberType(member);
            var value = CreateValue(memberType, seed);
            if (value != null && TrySetMemberValue(config, member, value))
            {
                usedValue = value;
                return true;
            }

            var idMember = FindMember(config.GetType(), indexName + "Id");
            if (idMember != null)
            {
                var idValue = CreateValue(GetMemberType(idMember), seed);
                if (idValue != null && TrySetMemberValue(config, idMember, idValue))
                {
                    usedValue = idValue;
                    return true;
                }
            }

            return false;
        }

        private static object CreateValue(Type type, int seed)
        {
            if (type == null)
                return null;
            if (type == typeof(int))
                return seed % 10;
            if (type == typeof(long))
                return (long)(seed % 10);
            if (type == typeof(string))
                return $"Group{seed % 5}";
            if (type == typeof(bool))
                return seed % 2 == 0;
            if (type == typeof(float))
                return (float)(seed % 10);
            if (type == typeof(double))
                return (double)(seed % 10);
            if (type.IsEnum)
            {
                var values = Enum.GetValues(type);
                return values.Length > 0 ? values.GetValue(seed % values.Length) : Activator.CreateInstance(type);
            }

            if (type == typeof(Vector2))
                return new Vector2(seed % 10, seed % 7);
            if (type == typeof(Vector3))
                return new Vector3(seed % 10, seed % 7, seed % 5);
            if (type == typeof(Vector4))
                return new Vector4(seed % 10, seed % 7, seed % 5, seed % 3);
            if (type == typeof(Color))
                return new Color((seed % 255) / 255f, (seed % 127) / 127f, (seed % 63) / 63f, 1f);

            return null;
        }

        private static bool TrySetMemberValue(object obj, MemberInfo member, object value)
        {
            if (obj == null || member == null)
                return false;

            if (member is PropertyInfo prop)
            {
                var setter = prop.GetSetMethod(true);
                if (setter == null)
                    return false;
                var converted = ConvertToType(value, prop.PropertyType);
                setter.Invoke(obj, new[] { converted });
                return true;
            }

            if (member is FieldInfo field)
            {
                var converted = ConvertToType(value, field.FieldType);
                field.SetValue(obj, converted);
                return true;
            }

            return false;
        }

        private static bool SetMemberValue(object obj, string name, object value)
        {
            var member = FindMember(obj.GetType(), name);
            return TrySetMemberValue(obj, member, value);
        }

        private static object ConvertToType(object value, Type targetType)
        {
            if (targetType == null)
                return value;
            if (value == null)
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            if (targetType.IsAssignableFrom(value.GetType()))
                return value;
            if (targetType.IsEnum)
            {
                var num = Convert.ChangeType(value, Enum.GetUnderlyingType(targetType));
                return Enum.ToObject(targetType, num);
            }
            return Convert.ChangeType(value, targetType);
        }

        private static void ForceGC()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private void WarmupGetConfig(int count)
        {
            for (int i = 0; i < count && _keys.Count > 0; i++)
                _getConfigFunc(_keys[i % _keys.Count]);
        }

        private void WarmupGetByIndex()
        {
            if (string.IsNullOrEmpty(_indexName) || _indexValue == null)
                return;
            _getByIndexFunc(_indexName, _indexValue);
        }

        private static int ComputeIterations(int rowCount, int min, int max, int multiplier)
        {
            var iter = rowCount <= 0 ? min : rowCount * multiplier;
            if (iter < min) iter = min;
            if (iter > max) iter = max;
            return iter;
        }

        private static double ComputeOpsPerSecond(int iterations, long elapsedMs)
        {
            return iterations * 1000.0 / Math.Max(1, elapsedMs);
        }

        private (long ms, float gcKb) MeasureGetAll(int loops, bool useCache)
        {
            _azcel.UseQueryCache = useCache;
            if (useCache)
                _getAllFunc();
            ForceGC();
            var gcBefore = GC.GetTotalMemory(false);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < loops; i++)
                _getAllFunc();
            sw.Stop();
            var gcAfter = GC.GetTotalMemory(false);
            return (sw.ElapsedMilliseconds, (gcAfter - gcBefore) / 1024f);
        }

        private (long ms, float gcKb) MeasureGetByIndex(int loops, bool useCache)
        {
            _azcel.UseQueryCache = useCache;
            if (useCache)
                _getByIndexFunc(_indexName, _indexValue);
            ForceGC();
            var gcBefore = GC.GetTotalMemory(false);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < loops; i++)
                _getByIndexFunc(_indexName, _indexValue);
            sw.Stop();
            var gcAfter = GC.GetTotalMemory(false);
            return (sw.ElapsedMilliseconds, (gcAfter - gcBefore) / 1024f);
        }

        private static Func<object, object> BuildGetConfigFunc(AzcelSystem azcel, Type configType)
        {
            var method = FindGenericMethod(typeof(AzcelSystem), "GetConfig", 1, 1, typeof(object));
            var generic = method.MakeGenericMethod(configType);
            var key = Expression.Parameter(typeof(object), "key");
            var call = Expression.Call(Expression.Constant(azcel), generic, key);
            var cast = Expression.Convert(call, typeof(object));
            return Expression.Lambda<Func<object, object>>(cast, key).Compile();
        }

        private static Func<object, bool> BuildTryGetConfigFunc(AzcelSystem azcel, Type configType)
        {
            var method = FindGenericMethod(typeof(AzcelSystem), "TryGetConfig", 1, 2, typeof(object));
            var generic = method.MakeGenericMethod(configType);
            var key = Expression.Parameter(typeof(object), "key");
            var configVar = Expression.Variable(configType, "config");
            var call = Expression.Call(Expression.Constant(azcel), generic, key, configVar);
            var body = Expression.Block(new[] { configVar }, call);
            return Expression.Lambda<Func<object, bool>>(body, key).Compile();
        }

        private static Func<object> BuildGetAllFunc(AzcelSystem azcel, Type configType)
        {
            var method = FindGenericMethod(typeof(AzcelSystem), "GetAllConfig", 1, 0);
            var generic = method.MakeGenericMethod(configType);
            var call = Expression.Call(Expression.Constant(azcel), generic);
            var cast = Expression.Convert(call, typeof(object));
            return Expression.Lambda<Func<object>>(cast).Compile();
        }

        private static Func<object> BuildGetAllNoAllocFunc(AzcelSystem azcel, Type configType, Type keyType)
        {
            var method = FindGenericMethod(typeof(AzcelSystem), "GetAllConfig", 2, 0);
            var generic = method.MakeGenericMethod(configType, keyType);
            var call = Expression.Call(Expression.Constant(azcel), generic);
            var cast = Expression.Convert(call, typeof(object));
            return Expression.Lambda<Func<object>>(cast).Compile();
        }

        private static Func<string, object, object> BuildGetByIndexFunc(AzcelSystem azcel, Type configType)
        {
            var method = FindGenericMethod(typeof(AzcelSystem), "GetByIndex", 1, 2);
            var generic = method.MakeGenericMethod(configType);
            var index = Expression.Parameter(typeof(string), "index");
            var value = Expression.Parameter(typeof(object), "value");
            var call = Expression.Call(Expression.Constant(azcel), generic, index, value);
            var cast = Expression.Convert(call, typeof(object));
            return Expression.Lambda<Func<string, object, object>>(cast, index, value).Compile();
        }

        private static MethodInfo FindGenericMethod(Type type, string name, int genericArgs, int paramCount, Type firstParamType = null)
        {
            return type.GetMethods()
                .First(m =>
                    m.Name == name &&
                    m.IsGenericMethodDefinition &&
                    m.GetGenericArguments().Length == genericArgs &&
                    m.GetParameters().Length == paramCount &&
                    (firstParamType == null || m.GetParameters()[0].ParameterType == firstParamType));
        }

        private static object GetMemberValue(object obj, MemberInfo member)
        {
            if (member is FieldInfo field)
                return field.GetValue(obj);
            if (member is PropertyInfo prop)
                return prop.GetValue(obj);
            return null;
        }

        private static object GetIdValue(object config)
        {
            if (config == null)
                return null;
            var type = config.GetType();
            var member = FindMember(type, "Id");
            return member != null ? GetMemberValue(config, member) : null;
        }

        private static Type GetMemberType(MemberInfo member)
        {
            if (member is FieldInfo field)
                return field.FieldType;
            if (member is PropertyInfo prop)
                return prop.PropertyType;
            return null;
        }

        private static MemberInfo FindMember(Type type, string name)
        {
            return type.GetMember(name, BindingFlags.Instance |
                                       BindingFlags.Public |
                                       BindingFlags.NonPublic)
                .FirstOrDefault(m => m.MemberType == MemberTypes.Field ||
                                     m.MemberType == MemberTypes.Property);
        }

        private static List<(string name, MemberInfo member)> GetIndexMembers(Type type)
        {
            var members = type.GetMembers(BindingFlags.Instance |
                                          BindingFlags.Public |
                                          BindingFlags.NonPublic);
            var result = new List<(string, MemberInfo)>();
            foreach (var member in members)
            {
                if (member.MemberType != MemberTypes.Field &&
                    member.MemberType != MemberTypes.Property)
                    continue;

                var attr = member.GetCustomAttribute<ConfigIndexAttribute>();
                if (attr == null)
                    continue;

                var name = string.IsNullOrEmpty(attr.Name) ? member.Name : attr.Name;
                result.Add((name, member));
            }

            return result;
        }

        private static bool HasIndex(Type type)
        {
            if (type == null)
                return false;
            return GetIndexMembers(type).Count > 0;
        }

        private static void RegisterTablesByReflection(AzcelSystem azcel, string codeNamespace)
        {
            if (azcel == null)
                return;

            var tables = FindTableTypes(codeNamespace);
            foreach (var tableType in tables)
            {
                try
                {
                    var instance = Activator.CreateInstance(tableType) as IConfigTable;
                    if (instance != null)
                        azcel.RegisterTable(instance);
                }
                catch
                {
                    // 忽略无法创建的表
                }
            }
        }

        private static List<Type> FindTableTypes(string codeNamespace)
        {
            return new List<Type>();
        }

        private static bool IsInNamespace(Type type, string codeNamespace)
        {
            return true;
        }
    }
}
