namespace Due;

public class Packer
{
    // 默认size位字节长度
    public const int DEFAULT_SIZE_BYTES = 4;

    // 默认header位字节长度
    public const int DEFAULT_HEADER_BYTES = 1;

    // 默认route位字节长度
    public const int DEFAULT_ROUTE_BYTES = 2;

    // 默认seq位字节长度
    public const int DEFAULT_SEQ_BYTES = 2;

    // 默认buffer数据位字节长度
    public const int DEFAULT_BUFFER_BYTES = 5000;

    // 字节序；默认为big
    private ByteOrder byteOrder = ByteOrder.BigEndian;
    // 路由字节长度（字节）；默认为2字节，最大值为65535
    private int routeBytes = 2;
    // 序列号字节长度（字节），长度为0时不开启序列号编码；默认为2字节，最大值为65535
    private int seqBytes = 2;
    // 编解码器
    private IEncoding encoding = new Json();

    public Packer()
    {

    }

    public Packer(ByteOrder byteOrder)
    {
        this.byteOrder = byteOrder;
    }

    public Packer(ByteOrder byteOrder, int routeBytes)
    {
        this.byteOrder = byteOrder;
        this.routeBytes = routeBytes;
    }

    public Packer(ByteOrder byteOrder, int routeBytes, int seqBytes)
    {
        this.byteOrder = byteOrder;
        this.routeBytes = routeBytes;
        this.seqBytes = seqBytes;
    }

    public Packer(ByteOrder byteOrder, int routeBytes, int seqBytes, IEncoding encoding)
    {
        this.byteOrder = byteOrder;
        this.routeBytes = routeBytes;
        this.seqBytes = seqBytes;
        this.encoding = encoding;
    }

    // 打包心跳
    public byte[] PackHeartbeat()
    {
        ByteBuffer bb = new ByteBuffer(this.byteOrder);

        bb.WriteInt32(DEFAULT_HEADER_BYTES);

        bb.WriteInt8(1 << 7);

        return bb.ToArrayBuffer();
    }

    // 打包消息
    public byte[] PackMessage(Int32 route, Int32? seq, object? data)
    {
        ByteBuffer bb = new ByteBuffer(this.byteOrder);
        int header = 0;
        seq ??= 0;

        bb.Skip(DEFAULT_SIZE_BYTES);

        bb.WriteInt8(header);

        switch (this.routeBytes)
        {
            case 1:
                bb.WriteInt8((int)route);
                break;
            case 2:
                bb.WriteInt16((Int16)route);
                break;
            case 4:
                bb.WriteInt32((Int32)route);
                break;
        }

        switch (this.seqBytes)
        {
            case 1:
                bb.WriteInt8((int)seq);
                break;
            case 2:
                bb.WriteInt16((Int16)seq);
                break;
            case 4:
                bb.WriteInt32((Int32)seq);
                break;
        }

        if (data != null)
        {
            if (data is byte[] v)
            {
                bb.WriteBytes(v);
            }
            else
            {
                bb.WriteBytes(this.encoding.Encode(data));
            }
        }

        bb.Skip(0);
        bb.WriteInt32((Int32)(bb.Length() - DEFAULT_SIZE_BYTES));

        return bb.ToArrayBuffer();
    }

    // 解包消息
    public Packet Unpack(byte[] buff)
    {
        ByteBuffer bb = new(this.byteOrder, buff);

        Int32 size = bb.ReadInt32();

        if ((DEFAULT_SIZE_BYTES + size) != bb.Length())
        {
            throw new Exception("Invalid data");
        }

        int header = bb.ReadInt8();
        bool isHeartbeat = header >> 7 == 1;

        if (isHeartbeat)
        {
            if (bb.Remaining() > 0)
            {
                return new Packet(isHeartbeat, bb.ReadInt64(), null);
            }
            else
            {
                return new Packet(isHeartbeat, null, null);
            }
        }

        Int32 route = 0;
        Int32 seq = 0;

        switch (this.routeBytes)
        {
            case 1:
                route = (Int32)(bb.ReadInt8());
                break;
            case 2:
                route = (Int32)(bb.ReadInt16());
                break;
            case 4:
                route = bb.ReadInt32();
                break;
        }

        switch (this.seqBytes)
        {
            case 1:
                seq = (Int32)(bb.ReadInt8());
                break;
            case 2:
                seq = (Int32)(bb.ReadInt16());
                break;
            case 4:
                seq = bb.ReadInt32();
                break;
        }

        byte[] data = bb.ReadBytes((Int32)(bb.Remaining()));

        return new Packet(false, null, new Message(seq, route, this.encoding.Decode(data)));
    }

    // 解包大小
    public Int32 UnpackSize(byte[] buff)
    {
        ByteBuffer bb = new ByteBuffer(this.byteOrder, buff);

        return bb.ReadInt32();
    }
}

public struct Packet(bool isHeartbeat, Int64? millisecond, Message? message)
{
    // 是否是心跳包
    public bool IsHeartbeat = isHeartbeat;
    // 心跳包携带的服务器时间（毫秒）
    public Int64? Millisecond = millisecond;
    // 消息数据
    public Message? Message = message;
}

public struct Message(Int32 seq, Int32 route, Data? data)
{
    // 消息序列号
    public Int32 Seq = seq;
    // 消息路由
    public Int32 Route = route;
    // 消息数据
    public Data? Data = data;
}