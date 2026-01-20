using System.IO;
using Azcel;

namespace Azcel.Editor
{
    /// <summary>
    /// 二进制数据序列化器（编辑器，仅负责数据序列化）
    /// </summary>
    public sealed class BinaryConfigDataSerializer : ConfigDataSerializerBase
    {
        private FileStream _stream;
        private BinaryWriter _writer;
        private long _lastBytes;

        protected override IValueWriter BeginTable(TableDefinition table, string outputPath)
        {
            var filePath = Path.Combine(outputPath, $"{table.Name}.bytes");
            _stream = File.Create(filePath);
            _writer = new BinaryWriter(_stream);
            var valueWriter = new BinaryValueWriter(_writer);
            var schemaHash = TableCodeGenerator.ComputeSchemaHash(table);
            var fieldCount = TableCodeGenerator.GetSerializedFieldCount(table);
            valueWriter.WriteInt(BinaryConfigHeader.Magic);
            valueWriter.WriteInt(schemaHash);
            valueWriter.WriteInt(fieldCount);
            valueWriter.WriteInt(table.Rows.Count);
            return valueWriter;
        }

        protected override long EndTable(TableDefinition table, IValueWriter writer)
        {
            _writer?.Flush();
            _stream?.Flush();
            _lastBytes = _stream?.Length ?? 0;
            _writer?.Dispose();
            _stream?.Dispose();
            _writer = null;
            _stream = null;
            return _lastBytes;
        }

    }
}
