namespace Due;

public interface IEncoding
{
    // 编码
    byte[] Encode(object data);

    // 解码
    Data Decode(byte[] buff);
}