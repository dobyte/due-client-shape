namespace Due.Network;

using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

// WebSocket 传输层实现（基于 ClientWebSocket）
public class Ws : INetwork
{
    // 默认接收缓冲区大小
    private const int RECEIVE_BUFFER_SIZE = 4096;

    // WebSocket 连接
    private ClientWebSocket? webSocket;
    // 接收任务取消源
    private CancellationTokenSource? receiveCts;
    // 发送信号量，避免并发发送
    private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);

    public Action? OnConnected { get; set; }
    public Action? OnDisconnected { get; set; }
    public Action<byte[]>? OnPacket { get; set; }

    public Ws(Packer packer)
    {
        // WebSocket 以完整消息帧传输，包长度前缀由帧自身承载，无需解析 size 前缀
        // 保留参数与 Tcp 构造函数签名保持一致
    }

    // 连接服务器
    public bool Connect(string addr)
    {
        try
        {
            this.webSocket = new ClientWebSocket();
            this.receiveCts = new CancellationTokenSource();

            Uri uri = new Uri(addr);
            this.webSocket.ConnectAsync(uri, CancellationToken.None)
                .ContinueWith(this.HandleConnected, TaskScheduler.Default);

            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }

    // 处理连接成功
    private void HandleConnected(Task connectTask)
    {
        if (this.webSocket == null)
        {
            return;
        }

        if (connectTask.IsFaulted || connectTask.IsCanceled)
        {
            Console.WriteLine(connectTask.Exception);
            return;
        }

        this.OnConnected?.Invoke();

        // 启动后台接收循环
        _ = Task.Run(this.ReceiveLoop);
    }

    // 当前是否已连接
    public bool IsConnected()
    {
        return this.webSocket != null && this.webSocket.State == WebSocketState.Open;
    }

    // 发送完整数据包
    public bool Send(byte[] buffer)
    {
        if (!this.IsConnected())
        {
            return false;
        }

        try
        {
            this.sendLock.Wait();
            try
            {
                this.webSocket!.SendAsync(
                    new ArraySegment<byte>(buffer),
                    WebSocketMessageType.Binary,
                    true,
                    CancellationToken.None
                ).Wait();
            }
            finally
            {
                this.sendLock.Release();
            }

            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }

    // 后台接收循环：读取完整 WebSocket 消息并回调数据包
    private async Task ReceiveLoop()
    {
        if (this.webSocket == null || this.receiveCts == null)
        {
            return;
        }

        byte[] buffer = new byte[RECEIVE_BUFFER_SIZE];

        try
        {
            while (this.webSocket.State == WebSocketState.Open && !this.receiveCts.IsCancellationRequested)
            {
                using var ms = new System.IO.MemoryStream();
                WebSocketReceiveResult result;

                // 循环拼接直到收到完整消息
                do
                {
                    result = await this.webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        this.receiveCts.Token
                    );

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await this.webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            string.Empty,
                            CancellationToken.None
                        );
                        this.OnDisconnected?.Invoke();
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    this.OnPacket?.Invoke(ms.ToArray());
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 主动断开
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            this.OnDisconnected?.Invoke();
        }
    }

    // 断开连接
    public void Disconnect()
    {
        if (this.webSocket == null)
        {
            return;
        }

        try
        {
            this.receiveCts?.Cancel();

            if (this.webSocket.State == WebSocketState.Open)
            {
                this.webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    string.Empty,
                    CancellationToken.None
                ).Wait(TimeSpan.FromSeconds(3));
            }

            this.webSocket.Dispose();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}
