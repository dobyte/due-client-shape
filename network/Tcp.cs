namespace Due.Network;

using System;
using System.Net;
using System.Net.Sockets;

// TCP 传输层实现
public class Tcp : INetwork
{
    // Socket
    private Socket? Socket;
    // 发送锁，避免并发发送导致帧交错
    private readonly object sendLock = new object();
    // 打包器（用于按字节序解析长度前缀）
    private readonly Packer packer;

    public Action? OnConnected { get; set; }
    public Action? OnDisconnected { get; set; }
    public Action<byte[]>? OnPacket { get; set; }

    public Tcp(Packer packer)
    {
        this.packer = packer;
    }

    // 接收状态（缓冲区 + 已读偏移）
    private class ReceiveState
    {
        public byte[] Buffer = Array.Empty<byte>();
        public int Offset;
    }

    // 连接服务器
    public bool Connect(string addr)
    {
        try
        {
            IPEndPoint iep = IPEndPoint.Parse(addr);
            this.Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.Socket.BeginConnect(iep, this.HandleConnected, this.Socket);

            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }

    // 处理连接成功请求
    private void HandleConnected(IAsyncResult ar)
    {
        if (this.Socket == null)
        {
            return;
        }

        try
        {
            this.Socket.EndConnect(ar);
            this.ReceiveAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return;
        }

        this.OnConnected?.Invoke();
    }

    // 当前是否已连接
    public bool IsConnected()
    {
        return this.Socket != null && this.Socket.Connected;
    }

    // 发送完整数据包
    public bool Send(byte[] buffer)
    {
        if (this.Socket != null && this.IsConnected())
        {
            try
            {
                lock (this.sendLock)
                {
                    this.Socket.Send(buffer);
                }
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        return false;
    }

    // 断开连接
    public void Disconnect()
    {
        if (this.Socket == null)
        {
            return;
        }

        try
        {
            this.Socket.Shutdown(SocketShutdown.Both);
            this.Socket.Close();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return;
        }

        this.OnDisconnected?.Invoke();
    }

    // 异步接收消息
    private void ReceiveAsync()
    {
        this.ReceiveSize();
    }

    // 接收包大小消息
    private void ReceiveSize()
    {
        if (this.Socket != null)
        {
            try
            {
                ReceiveState state = new ReceiveState { Buffer = new byte[Packer.DEFAULT_SIZE_BYTES] };
                this.Socket.BeginReceive(state.Buffer, 0, state.Buffer.Length, SocketFlags.None, this.HandleReceivedSize, state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

    // 处理接收到的包大小
    private void HandleReceivedSize(IAsyncResult ar)
    {
        if (this.Socket == null)
        {
            return;
        }

        try
        {
            if (ar.AsyncState is not ReceiveState state)
            {
                return;
            }

            int length = this.Socket.EndReceive(ar);
            if (length <= 0)
            {
                // 对端关闭连接
                this.Disconnect();
                return;
            }

            state.Offset += length;
            if (state.Offset < state.Buffer.Length)
            {
                // 长度头未收全（TCP 分片），继续读取剩余字节
                this.Socket.BeginReceive(state.Buffer, state.Offset, state.Buffer.Length - state.Offset, SocketFlags.None, this.HandleReceivedSize, state);
                return;
            }

            int size = Packer.DEFAULT_SIZE_BYTES + this.packer.UnpackSize(state.Buffer);

            byte[] buff = new byte[size];

            Array.Copy(state.Buffer, buff, Packer.DEFAULT_SIZE_BYTES);

            ReceiveState bodyState = new ReceiveState { Buffer = buff, Offset = Packer.DEFAULT_SIZE_BYTES };
            this.Socket.BeginReceive(buff, Packer.DEFAULT_SIZE_BYTES, size - Packer.DEFAULT_SIZE_BYTES, SocketFlags.None, this.HandleReceivedBody, bodyState);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    // 处理接收到的包体数据
    private void HandleReceivedBody(IAsyncResult ar)
    {
        if (this.Socket == null)
        {
            return;
        }

        try
        {
            if (ar.AsyncState is not ReceiveState state)
            {
                return;
            }

            int length = this.Socket.EndReceive(ar);
            if (length <= 0)
            {
                // 对端关闭连接
                this.Disconnect();
                return;
            }

            state.Offset += length;
            if (state.Offset < state.Buffer.Length)
            {
                // 包体未收全（TCP 分片），继续读取剩余字节
                this.Socket.BeginReceive(state.Buffer, state.Offset, state.Buffer.Length - state.Offset, SocketFlags.None, this.HandleReceivedBody, state);
                return;
            }

            this.OnPacket?.Invoke(state.Buffer);

            this.ReceiveAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}
