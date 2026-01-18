using System.IO;
using Azcel;

namespace Azcel.Editor
{
    /// <summary>
    /// 二进制基础写入器（只关心基础类型）
    /// </summary>
    public sealed class BinaryValueWriter : IValueWriter
    {
        private readonly BinaryWriter _writer;

        public BinaryValueWriter(BinaryWriter writer)
        {
            _writer = writer;
        }

        public void WriteInt(int value) => _writer.Write(value);
        public void WriteLong(long value) => _writer.Write(value);
        public void WriteFloat(float value) => _writer.Write(value);
        public void WriteDouble(double value) => _writer.Write(value);
        public void WriteBool(bool value) => _writer.Write(value);
        public void WriteString(string value) => _writer.Write(value ?? "");

        public void BeginArray(int count) => _writer.Write(count);
        public void EndArray() { }

        public void BeginObject() { }
        public void EndObject() { }
        public void WritePropertyName(string name) { }
    }
}
