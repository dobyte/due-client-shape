namespace Due;

using System;
using System.Timers;
using Due.Network;

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
    // 网络层
    private INetwork Network { get; set; }
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

    public Client() : this(new Packer())
    {
    }

    public Client(string addr) : this(addr, new Packer())
    {
    }

    public Client(Packer packer) : this(new Tcp(packer), packer, DEFAULT_HEARTBEAT_INTERVAL)
    {
    }

    public Client(string addr, Packer packer) : this(addr, new Tcp(packer), packer, DEFAULT_HEARTBEAT_INTERVAL)
    {
    }

    public Client(string addr, int heartbeatInterval) : this(addr, new Tcp(new Packer()), new Packer(), heartbeatInterval)
    {
    }

    public Client(string addr, Packer packer, int heartbeatInterval) : this(addr, new Tcp(packer), packer, heartbeatInterval)
    {
    }

    public Client(INetwork network, Packer packer) : this(network, packer, DEFAULT_HEARTBEAT_INTERVAL)
    {
    }

    public Client(INetwork network, Packer packer, int heartbeatInterval)
    {
        this.Network = network;
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

        this.BindNetworkEvents();
    }

    public Client(string addr, INetwork network, Packer packer, int heartbeatInterval)
    {
        this.Addr = addr;
        this.Network = network;
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

        this.BindNetworkEvents();
    }

    // 绑定网络层事件
    private void BindNetworkEvents()
    {
        this.Network.OnConnected = () =>
        {
            this.Heartbeat();
            this.Timer?.Start();

            if (this.ConnectHandler != null)
            {
                this.ConnectHandler(this);
            }
        };

        this.Network.OnDisconnected = () =>
        {
            this.Timer?.Stop();

            if (this.DisconnectHandler != null)
            {
                this.DisconnectHandler(this);
            }
        };

        this.Network.OnPacket = (byte[] buff) =>
        {
            try
            {
                Packet packet = this.Packer.Unpack(buff);

                if (packet.IsHeartbeat)
                {
                    if (this.HeartbeatHandler != null)
                    {
                        this.HeartbeatHandler(this, packet.Millisecond);
                    }
                }
                else
                {
                    if (this.ReceiveHandler != null && packet.Message != null)
                    {
                        this.ReceiveHandler(this, (Message)packet.Message);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        };
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
        return this.Network.Connect(addr);
    }

    // 发送数据
    public bool Send(Int32 route, Int32? seq, byte[]? data)
    {
        try
        {
            byte[] buffer = this.Packer.PackMessage(route, seq, data);
            return this.Network.Send(buffer);
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
            return this.Network.Send(buffer);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return false;
    }

    // 检测客户端是否已连接
    public bool IsConnected()
    {
        return this.Network.IsConnected();
    }

    // 断开连接
    public void Disconnect()
    {
        this.Network.Disconnect();
    }

    // 发送心跳包
    private void Heartbeat()
    {
        if (this.HeartbeatBuffer != null)
        {
            this.Network.Send(this.HeartbeatBuffer);
        }
    }
}
