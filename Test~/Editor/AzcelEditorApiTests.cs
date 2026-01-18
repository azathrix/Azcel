using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azathrix.Framework.Core;
using NUnit.Framework;

namespace Azcel.Tests.Editor
{
    public class AzcelEditorApiTests
    {
        private sealed class DummyConfig : ConfigBase<int>
        {
            public override string ConfigName => "EditorDummy";

            [ConfigIndex]
            public string Name { get; private set; }
            public int Value { get; private set; }

            public override void Deserialize(BinaryReader reader)
            {
                Id = reader.ReadInt32();
                Name = reader.ReadString();
                Value = reader.ReadInt32();
            }
        }

        private sealed class DummyTable : ConfigTable<DummyConfig, int>
        {
            protected override List<IndexAccessor> CreateIndexAccessors()
            {
                return new List<IndexAccessor>
                {
                    new IndexAccessor("Name", typeof(string), config => config.Name)
                };
            }
        }

        [Test]
        public async Task Editor_CanGetSystem_AndCallApi()
        {
            var manager = new SystemRuntimeManager
            {
                IsEditorMode = true
            };
            AzathrixFramework.SetEditorRuntimeManager(manager);

            await manager.CreateSystemFromTypesAsync(new[] { typeof(AzcelSystem) });

            Assert.IsTrue(AzathrixFramework.HasSystem<AzcelSystem>());
            var system = AzathrixFramework.GetSystem<AzcelSystem>();
            Assert.NotNull(system);

            system.SetTableLoader(BinaryConfigTableLoader.Instance);

            var table = new DummyTable();
            system.RegisterTable(table);
            system.LoadTable(table, BuildDummyData());

            var byId = system.GetConfig<DummyConfig, int>(1);
            Assert.NotNull(byId);
            Assert.AreEqual("A", byId.Name);
            Assert.AreEqual(10, byId.Value);

            Assert.IsTrue(system.TryGetConfig<DummyConfig, int>(2, out var byId2));
            Assert.AreEqual("B", byId2.Name);

            var allStrong = system.GetAllConfig<DummyConfig, int>();
            Assert.AreEqual(2, allStrong.Count);

            var allCopy = system.GetAllConfig<DummyConfig>();
            Assert.AreEqual(2, allCopy.Count);

            var byIndex = system.GetByIndex<DummyConfig, int>("Name", "A");
            Assert.AreEqual(1, byIndex.Count);
            Assert.AreEqual(1, byIndex[0].Id);

            var byIndexCopy = system.GetByIndex<DummyConfig>("Name", "B");
            Assert.AreEqual(1, byIndexCopy.Count);

            var tableByConfig = system.GetTable<DummyConfig, int>();
            Assert.NotNull(tableByConfig);

            var tableByType = system.GetTable<DummyTable>();
            Assert.NotNull(tableByType);
        }

        private static byte[] BuildDummyData()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(2);

            writer.Write(1);
            writer.Write("A");
            writer.Write(10);

            writer.Write(2);
            writer.Write("B");
            writer.Write(20);

            return stream.ToArray();
        }
    }
}
