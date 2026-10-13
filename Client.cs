namespace Due;

using System;
using System.Net;
using System.Net.Sockets;
using System.Timers;

// 连接处理器
public delegate void ConnectHandler(Client client);

// 断开连接处理器
public delegate void DisconnectHandler(Client client);

// 消息接收处理器
public delegate void ReceiveHandler(Client client, Message message);

// 心跳消息处理器
public delegate void HeartbeatHandler(Client client, Int64? millisecond);

public class Client
{
    // 默认心跳间隔
    const int DEFAULT_HEARTBEAT_INTERVAL = 10 * 1000;
    // 拨号地址
    private string? Addr { get; set; }
    // Socket
    private Socket? Socket { get; set; }
    // 打包器
    private Packer Packer = new Packer();
    // 定时器
    private Timer? Timer;
    // 心跳包
    private byte[]? HeartbeatBuffer;
    // 连接处理器
    private ConnectHandler? ConnectHandler;
    // 断开连接处理器
    private DisconnectHandler? DisconnectHandler;
    // 消息接收处理器
    private ReceiveHandler? ReceiveHandler;
    // 心跳消息处理器
    private HeartbeatHandler? HeartbeatHandler;
    // 发送锁，避免心跳线程与业务线程并发发送导致帧交错
    private readonly object sendLock = new object();

    public Client()
    {
        this.Timer = new Timer(DEFAULT_HEARTBEAT_INTERVAL);
        this.Timer.Elapsed += OnTimedEvent;
        this.HeartbeatBuffer = this.Packer.PackHeartbeat();
    }

    public Client(string addr)
    {
        this.Addr = addr;
        this.Timer = new Timer(DEFAULT_HEARTBEAT_INTERVAL);
        this.Timer.Elapsed += OnTimedEvent;
        this.HeartbeatBuffer = this.Packer.PackHeartbeat();
    }

    public Client(string addr, Packer packer)
    {
        this.Addr = addr;
        this.Packer = packer;
        this.Timer = new Timer(DEFAULT_HEARTBEAT_INTERVAL);
        this.Timer.Elapsed += OnTimedEvent;
        this.HeartbeatBuffer = this.Packer.PackHeartbeat();
    }

    public Client(string addr, int heartbeatInterval)
    {
        this.Addr = addr;

        if (heartbeatInterval > 0)
        {
            this.Timer = new Timer(heartbeatInterval);
            this.Timer.Elapsed += OnTimedEvent;
            this.HeartbeatBuffer = this.Packer.PackHeartbeat();
        }
        else
        {
            this.Timer = null;
        }
    }

    public Client(string addr, Packer packer, int heartbeatInterval)
    {
        this.Addr = addr;
        this.Packer = packer;

        if (heartbeatInterval > 0)
        {
            this.Timer = new Timer(heartbeatInterval);
            this.Timer.Elapsed += OnTimedEvent;
            this.HeartbeatBuffer = this.Packer.PackHeartbeat();
        }
        else
        {
            this.Timer = null;
        }
    }

    // 设置连接处理器
    public void OnConnect(ConnectHandler handler)
    {
        this.ConnectHandler = handler;
    }

    // 设置断开连接处理器
    public void OnDisconnect(DisconnectHandler handler)
    {
        this.DisconnectHandler = handler;
    }

    // 消息接收处理器
    public void OnReceive(ReceiveHandler handler)
    {
        this.ReceiveHandler = handler;
    }

    // 心跳消息处理器
    public void OnHeartbeat(HeartbeatHandler handler)
    {
        this.HeartbeatHandler = handler;
    }

    // 定时器
    private void OnTimedEvent(object? source, ElapsedEventArgs e)
    {
        this.Heartbeat();
    }

    // 连接服务器
    public bool Connect()
    {
        if (this.Addr == null)
        {
            return false;
        }

        return this.Connect(this.Addr);
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
        if (this.Socket != null)
        {
            try
            {
                this.Socket.EndConnect(ar);
                this.ReceiveAsync();
                this.Heartbeat();
                this.Timer?.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return;
            }

            if (this.ConnectHandler != null)
            {
                ConnectHandler(this);
            }
        }
    }

    // 发送数据
    public bool Send(Int32 route, Int32? seq, byte[]? data)
    {
        try
        {
            byte[] buffer = this.Packer.PackMessage(route, seq, data);
            return this.SendBuffer(buffer);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return false;
    }

    // 发送数据
    public bool Send(Int32 route, Int32? seq, object? data)
    {
        try
        {
            byte[] buffer = this.Packer.PackMessage(route, seq, data);
            return this.SendBuffer(buffer);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return false;
    }

    // 发送字节数据（加锁，保证与心跳包发送互斥）
    private bool SendBuffer(byte[] buffer)
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

    // 异步接收消息
    private void ReceiveAsync()
    {
        this.ReceiveSize();
    }

    // 接收状态（缓冲区 + 已读偏移）
    private class ReceiveState
    {
        public byte[] Buffer = Array.Empty<byte>();
        public int Offset;
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
        if (this.Socket != null)
        {
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

                int size = Packer.DEFAULT_SIZE_BYTES + this.Packer.UnpackSize(state.Buffer);

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
    }

    // 处理接收到的包体数据
    private void HandleReceivedBody(IAsyncResult ar)
    {
        if (this.Socket != null)
        {
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

                Packet packet = this.Packer.Unpack(state.Buffer);

                if (packet.IsHeartbeat)
                {
                    if (this.HeartbeatHandler != null)
                    {
                        HeartbeatHandler(this, packet.Millisecond);
                    }
                }
                else
                {
                    if (this.ReceiveHandler != null && packet.Message != null)
                    {
                        ReceiveHandler(this, (Message)packet.Message);
                    }
                }

                this.ReceiveAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

    // 检测客户端是否已连接
    public bool IsConnected()
    {
        return this.Socket != null && this.Socket.Connected;
    }

    // 断开连接
    public void Disconnect()
    {
        if (this.Socket != null)
        {
            try
            {
                this.Socket.Shutdown(SocketShutdown.Both);
                this.Socket.Close();
                this.Timer?.Stop();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return;
            }

            if (this.DisconnectHandler != null)
            {
                DisconnectHandler(this);
            }
        }
    }

    // 发送心跳包
    private void Heartbeat()
    {
        if (this.HeartbeatBuffer != null)
        {
            this.SendBuffer(this.HeartbeatBuffer);
        }
    }
}