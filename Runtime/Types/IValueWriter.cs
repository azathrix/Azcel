namespace Azcel
{
    /// <summary>
    /// 基础类型写入接口（由具体格式实现）
    /// </summary>
    public interface IValueWriter
    {
        void WriteInt(int value);
        void WriteLong(long value);
        void WriteFloat(float value);
        void WriteDouble(double value);
        void WriteBool(bool value);
        void WriteString(string value);

        void BeginArray(int count);
        void EndArray();

        void BeginObject();
        void EndObject();
        void WritePropertyName(string name);
    }
}
