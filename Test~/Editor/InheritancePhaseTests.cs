using System.Linq;
using System.Threading.Tasks;
using Azcel.Editor;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Azcel.Tests.Editor
{
    public class InheritancePhaseTests
    {
        private InheritancePhase _phase;
        private ConvertContext _context;

        [SetUp]
        public void Setup()
        {
            _phase = new InheritancePhase();
            _context = new ConvertContext();
        }

        [Test]
        public async Task Inherit_Fields_From_Parent()
        {
            // 父表
            var parent = new TableDefinition { Name = "ItemConfig" };
            parent.Fields.Add(new FieldDefinition { Name = "id", Type = "int", IsKey = true });
            parent.Fields.Add(new FieldDefinition { Name = "name", Type = "string" });
            parent.Fields.Add(new FieldDefinition { Name = "price", Type = "int" });

            // 子表继承父表
            var child = new TableDefinition { Name = "WeaponConfig", ParentTable = "ItemConfig" };
            child.Fields.Add(new FieldDefinition { Name = "atk", Type = "int" });
            child.Fields.Add(new FieldDefinition { Name = "crit", Type = "float" });

            _context.Tables.Add(parent);
            _context.Tables.Add(child);

            await _phase.ExecuteAsync(_context).AsTask();

            // 子表应该有 5 个字段：id, name, price (继承) + atk, crit (自己的)
            Assert.AreEqual(5, child.Fields.Count);
            Assert.AreEqual("id", child.Fields[0].Name);
            Assert.AreEqual("name", child.Fields[1].Name);
            Assert.AreEqual("price", child.Fields[2].Name);
            Assert.AreEqual("atk", child.Fields[3].Name);
            Assert.AreEqual("crit", child.Fields[4].Name);

            // 继承的字段属性应该正确
            Assert.IsTrue(child.Fields[0].IsKey);
        }

        [Test]
        public async Task Skip_Duplicate_Fields()
        {
            var parent = new TableDefinition { Name = "BaseConfig" };
            parent.Fields.Add(new FieldDefinition { Name = "id", Type = "int" });
            parent.Fields.Add(new FieldDefinition { Name = "name", Type = "string" });

            // 子表有同名字段 name
            var child = new TableDefinition { Name = "ChildConfig", ParentTable = "BaseConfig" };
            child.Fields.Add(new FieldDefinition { Name = "name", Type = "string", Comment = "子表的name" });
            child.Fields.Add(new FieldDefinition { Name = "value", Type = "int" });

            _context.Tables.Add(parent);
            _context.Tables.Add(child);

            await _phase.ExecuteAsync(_context).AsTask();

            // 子表应该有 3 个字段：id (继承) + name (自己的，不重复继承) + value
            Assert.AreEqual(3, child.Fields.Count);
            Assert.AreEqual("id", child.Fields[0].Name);
            Assert.AreEqual("name", child.Fields[1].Name);
            Assert.AreEqual("子表的name", child.Fields[1].Comment); // 保留子表的定义
            Assert.AreEqual("value", child.Fields[2].Name);
        }

        [Test]
        public async Task Warn_When_Parent_Not_Found()
        {
            var child = new TableDefinition { Name = "OrphanConfig", ParentTable = "NonExistent" };
            child.Fields.Add(new FieldDefinition { Name = "id", Type = "int" });

            _context.Tables.Add(child);

            await _phase.ExecuteAsync(_context).AsTask();

            Assert.AreEqual(1, _context.Warnings.Count);
            Assert.IsTrue(_context.Warnings[0].Contains("NonExistent"));
            Assert.IsTrue(_context.Warnings[0].Contains("不存在"));
        }

        [Test]
        public async Task Inherit_Key_Config()
        {
            var parent = new TableDefinition
            {
                Name = "BaseConfig",
                KeyField = "uid",
                KeyType = "string"
            };
            parent.Fields.Add(new FieldDefinition { Name = "uid", Type = "string", IsKey = true });

            var child = new TableDefinition { Name = "ChildConfig", ParentTable = "BaseConfig" };
            child.Fields.Add(new FieldDefinition { Name = "value", Type = "int" });

            _context.Tables.Add(parent);
            _context.Tables.Add(child);

            await _phase.ExecuteAsync(_context).AsTask();

            // 子表应该继承父表的主键配置
            Assert.AreEqual("uid", child.KeyField);
            Assert.AreEqual("string", child.KeyType);
        }

        [Test]
        public async Task No_Inherit_When_No_Parent()
        {
            var table = new TableDefinition { Name = "StandaloneConfig" };
            table.Fields.Add(new FieldDefinition { Name = "id", Type = "int" });
            table.Fields.Add(new FieldDefinition { Name = "name", Type = "string" });

            _context.Tables.Add(table);

            await _phase.ExecuteAsync(_context).AsTask();

            // 没有继承，字段数不变
            Assert.AreEqual(2, table.Fields.Count);
            Assert.AreEqual(0, _context.Warnings.Count);
        }

        [Test]
        public async Task Inherit_Index_And_Ref_Properties()
        {
            var parent = new TableDefinition { Name = "BaseConfig" };
            parent.Fields.Add(new FieldDefinition
            {
                Name = "type",
                Type = "ItemType",
                IsIndex = true,
                IsEnumRef = true,
                RefEnumName = "ItemType"
            });
            parent.Fields.Add(new FieldDefinition
            {
                Name = "nextItem",
                Type = "ref:ItemConfig",
                IsTableRef = true,
                RefTableName = "ItemConfig"
            });

            var child = new TableDefinition { Name = "ChildConfig", ParentTable = "BaseConfig" };
            child.Fields.Add(new FieldDefinition { Name = "id", Type = "int" });

            _context.Tables.Add(parent);
            _context.Tables.Add(child);

            await _phase.ExecuteAsync(_context).AsTask();

            // 继承的字段应该保留 Index 和 Ref 属性
            var typeField = child.Fields.First(f => f.Name == "type");
            Assert.IsTrue(typeField.IsIndex);
            Assert.IsTrue(typeField.IsEnumRef);
            Assert.AreEqual("ItemType", typeField.RefEnumName);

            var refField = child.Fields.First(f => f.Name == "nextItem");
            Assert.IsTrue(refField.IsTableRef);
            Assert.AreEqual("ItemConfig", refField.RefTableName);
        }
    }
}
