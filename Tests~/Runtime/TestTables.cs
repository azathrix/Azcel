using System.IO;
using Azcel;

namespace Azcel.Sample
{
    public enum TestEnum
    {
        Alpha = 1,
        Beta = 2
    }

    public sealed class TestItem : ConfigBase<int>
    {
        public override string ConfigName => "TestItem";

        [ConfigIndex]
        public int Type { get; private set; }

        public string Name { get; private set; }

        [ConfigIndex]
        public int Group { get; private set; }

        public int[] Scores { get; private set; }

        public int RefId { get; private set; }

        public TestEnum EnumVal { get; private set; }

        public bool Flag { get; private set; }

        public override void Deserialize(BinaryReader reader)
        {
            Id = reader.ReadInt32();
            Type = reader.ReadInt32();
            Name = reader.ReadString();
            Group = reader.ReadInt32();
            Scores = AzcelBinary.ReadArray<int>(reader);
            RefId = reader.ReadInt32();
            EnumVal = (TestEnum)reader.ReadInt32();
            Flag = reader.ReadBoolean();
        }
    }

    public sealed class TestItemTable : ConfigTable<TestItem, int>
    {
    }

    public sealed class TestOther : ConfigBase<string>
    {
        public override string ConfigName => "TestOther";

        public string Code { get; private set; }
        public int Value { get; private set; }
        public float Rate { get; private set; }

        public override void Deserialize(BinaryReader reader)
        {
            Code = reader.ReadString();
            Value = reader.ReadInt32();
            Rate = reader.ReadSingle();
            Id = Code;
        }
    }

    public sealed class TestOtherTable : ConfigTable<TestOther, string>
    {
    }

    public static class TableRegistry
    {
        public static void Apply(AzcelSystem system)
        {
            if (system == null)
                return;

            system.RegisterTable(new TestItemTable());
            system.RegisterTable(new TestOtherTable());
        }
    }
}
