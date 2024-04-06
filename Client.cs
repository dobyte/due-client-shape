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
        if (this.Socket != null && this.IsConnected())
        {
            try
            {
                byte[] buffer = this.Packer.PackMessage(route, seq, data);
                this.Socket.Send(buffer);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        return false;
    }

    // 发送数据
    public bool Send(Int32 route, Int32? seq, object? data)
    {
        if (this.Socket != null && this.IsConnected())
        {
            try
            {
                byte[] buffer = this.Packer.PackMessage(route, seq, data);
                this.Socket.Send(buffer);
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

    // 接收包大小消息
    private void ReceiveSize()
    {
        if (this.Socket != null)
        {
            try
            {
                byte[] bytes = new byte[Packer.DEFAULT_SIZE_BYTES];
                this.Socket.BeginReceive(bytes, 0, bytes.Length, SocketFlags.None, HandleReceivedSize, bytes);
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
                int length = this.Socket.EndReceive(ar);
                if (length == Packer.DEFAULT_SIZE_BYTES && ar.AsyncState is byte[] bytes)
                {
                    int size = Packer.DEFAULT_SIZE_BYTES + this.Packer.UnpackSize(bytes);

                    byte[] buff = new byte[size];

                    Array.Copy(bytes, buff, Packer.DEFAULT_SIZE_BYTES);

                    this.Socket.BeginReceive(buff, 4, size - Packer.DEFAULT_SIZE_BYTES, SocketFlags.None, this.HandleReceivedBody, buff);
                }
                else
                {
                    this.Disconnect();
                }
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
                int length = this.Socket.EndReceive(ar);
                if (ar.AsyncState is byte[] buff && buff.Length - Packer.DEFAULT_SIZE_BYTES == length)
                {
                    Packet packet = this.Packer.Unpack(buff);

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
                else
                {
                    this.Disconnect();
                }
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
        if (this.Socket != null && this.IsConnected())
        {
            try
            {
                if (this.HeartbeatBuffer != null)
                {
                    this.Socket.Send(this.HeartbeatBuffer);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}