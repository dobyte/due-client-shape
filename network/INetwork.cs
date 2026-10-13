namespace Due.Network;

using System;

// 网络抽象：屏蔽 TCP / WebSocket 的收发差异
public interface INetwork
{
    // 连接成功回调
    Action? OnConnected { get; set; }
    // 连接断开回调
    Action? OnDisconnected { get; set; }
    // 收到完整数据包（含 4 字节 size 前缀）回调
    Action<byte[]>? OnPacket { get; set; }

    // 连接服务器
    bool Connect(string addr);

    // 当前是否已连接
    bool IsConnected();

    // 发送完整数据包
    bool Send(byte[] buffer);

    // 断开连接
    void Disconnect();
}
