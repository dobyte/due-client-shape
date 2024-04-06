namespace Due;
using System.Text.Json;

public class Json : IEncoding
{
    public Json()
    {

    }

    // 编码
    public byte[] Encode(object data)
    {
        return System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data));
    }

    // 解码
    public Data Decode(byte[] buff)
    {
        return new Data(buff);
    }
}

public class Data(byte[] buff) : IData
{
    private byte[] Buff { get; set; } = buff;

    // 获取对象数据
    public T? GetObject<T>()
    {
        return JsonSerializer.Deserialize<T>(this.GetString());
    }

    // 获取字符串数据
    public string GetString()
    {
        return System.Text.Encoding.UTF8.GetString(this.Buff);
    }

    // 获取字节数据
    public byte[] GetBytes()
    {
        return this.Buff;
    }
}