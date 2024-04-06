namespace Due;

using System;
using System.IO;

public enum ByteOrder : byte
{
    LittleEndian = 0,
    BigEndian = 1
}

public class ByteBuffer
{
    // 字节序；默认为大端序
    private ByteOrder byteOrder = ByteOrder.BigEndian;
    // 创建一个内存流对象
    private MemoryStream buffer = new MemoryStream();

    public ByteBuffer()
    {

    }

    public ByteBuffer(ByteOrder byteOrder)
    {
        this.byteOrder = byteOrder;
    }

    public ByteBuffer(ByteOrder byteOrder, byte[] buffer)
    {
        this.byteOrder = byteOrder;
        this.buffer = new MemoryStream(buffer);
    }

    // 获取buffer长度
    public long Length()
    {
        return this.buffer.Length;
    }

    // 写入byte
    public void WriteByte(byte v)
    {
        this.buffer.WriteByte(v);
    }

    // 写入bool值
    public void WriteBool(bool v)
    {
        this.buffer.WriteByte((byte)(v ? 1 : 0));
    }

    // 写入int8值
    public void WriteInt8(int v)
    {
        this.buffer.WriteByte((byte)v);
    }

    // 写入int6值
    public void WriteInt16(Int16 v)
    {
        if (this.byteOrder == ByteOrder.BigEndian)
        {
            this.buffer.WriteByte((byte)(v >> 8));
            this.buffer.WriteByte((byte)(v & 0xff));
        }
        else
        {
            this.buffer.WriteByte((byte)(v & 0xff));
            this.buffer.WriteByte((byte)(v >> 8));
        }
    }

    // 写入int32值
    public void WriteInt32(Int32 v)
    {
        if (this.byteOrder == ByteOrder.BigEndian)
        {
            this.buffer.WriteByte((byte)(v >> 24));
            this.buffer.WriteByte((byte)(v >> 16));
            this.buffer.WriteByte((byte)(v >> 8));
            this.buffer.WriteByte((byte)(v & 0xff));
        }
        else
        {
            this.buffer.WriteByte((byte)(v & 0xff));
            this.buffer.WriteByte((byte)(v >> 8));
            this.buffer.WriteByte((byte)(v >> 16));
            this.buffer.WriteByte((byte)(v >> 24));
        }
    }

    // 写入int64
    public void WriteInt64(Int64 v)
    {
        if (this.byteOrder == ByteOrder.BigEndian)
        {
            this.buffer.WriteByte((byte)(v >> 56));
            this.buffer.WriteByte((byte)(v >> 48));
            this.buffer.WriteByte((byte)(v >> 40));
            this.buffer.WriteByte((byte)(v >> 32));
            this.buffer.WriteByte((byte)(v >> 24));
            this.buffer.WriteByte((byte)(v >> 16));
            this.buffer.WriteByte((byte)(v >> 8));
            this.buffer.WriteByte((byte)(v & 0xff));
        }
        else
        {
            this.buffer.WriteByte((byte)(v & 0xff));
            this.buffer.WriteByte((byte)(v >> 8));
            this.buffer.WriteByte((byte)(v >> 16));
            this.buffer.WriteByte((byte)(v >> 24));
            this.buffer.WriteByte((byte)(v >> 32));
            this.buffer.WriteByte((byte)(v >> 40));
            this.buffer.WriteByte((byte)(v >> 48));
            this.buffer.WriteByte((byte)(v >> 56));
        }
    }

    // 写入bytes值
    public void WriteBytes(byte[] v)
    {
        this.buffer.Write(v, 0, v.Length);
    }

    // 写入string值
    public void WriteString(string v)
    {

    }

    // 读取int8值
    public int ReadInt8()
    {
        int v = this.buffer.ReadByte();
        if (v == -1)
        {
            throw new Exception("Invalid buffer");
        }
        return v;
    }

    // 读取int16值
    public int ReadInt16()
    {
        if (this.byteOrder == ByteOrder.BigEndian)
        {
            return (this.ReadInt8() << 8) | this.ReadInt8();
        }
        else
        {
            return this.ReadInt8() | (this.ReadInt8() << 8);
        }
    }

    // 读取int32值
    public Int32 ReadInt32()
    {
        if (this.byteOrder == ByteOrder.BigEndian)
        {
            return ((Int32)this.ReadInt8() << 24) | ((Int32)this.ReadInt8() << 16) | ((Int32)this.ReadInt8() << 8) | (Int32)this.ReadInt8();
        }
        else
        {
            return (Int32)this.ReadInt8() | ((Int32)this.ReadInt8() << 8) | ((Int32)this.ReadInt8() << 16) | ((Int32)this.ReadInt8() << 24);
        }
    }

    // 读取int64值
    public Int64 ReadInt64()
    {
        if (this.byteOrder == ByteOrder.BigEndian)
        {
            return ((Int64)this.ReadInt8() << 56) | ((Int64)this.ReadInt8() << 48) | ((Int64)this.ReadInt8() << 40) | ((Int64)this.ReadInt8() << 32) | ((Int64)this.ReadInt8() << 24) | ((Int64)this.ReadInt8() << 16) | ((Int64)this.ReadInt8() << 8) | (Int64)this.ReadInt8();
        }
        else
        {
            return ((Int64)this.ReadInt8() | ((Int64)this.ReadInt8() << 8) | ((Int64)this.ReadInt8() << 16) | ((Int64)this.ReadInt8() << 24) | ((Int64)this.ReadInt8() << 32) | ((Int64)this.ReadInt8() << 40) | ((Int64)this.ReadInt8() << 48) | ((Int64)this.ReadInt8() << 56));
        }
    }

    // 读取字节数组
    public byte[] ReadBytes(Int32 n)
    {
        Int32 remaining = (Int32)(this.Remaining());

        if (n > remaining)
        {
            n = remaining;
        }

        byte[] buff = new byte[n];
        this.buffer.Read(buff, 0, n);

        return buff;
    }

    // 跳到指定位置
    public void Skip(Int64 n)
    {
        this.buffer.Seek(n, SeekOrigin.Begin);
    }

    // 获取剩余的数据长度
    public Int64 Remaining()
    {
        return this.buffer.Length - this.buffer.Position;
    }

    public byte[] ToArrayBuffer()
    {
        return this.buffer.ToArray();
    }
}