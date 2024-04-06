namespace Due;

public interface IData
{
    // 获取对象数据
    T? GetObject<T>();

    // 获取字符串数据
    string GetString();

    // 获取字节数据
    byte[] GetBytes();
}