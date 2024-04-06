using Due;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;

class Hello
{
    static void Main()
    {
        // 创建客户端
        Client client = new();

        // 监听连接
        client.OnConnect(ConnectHandler);

        // 监听断开连接
        client.OnDisconnect(DisconnectHandler);

        // 监听接收消息
        client.OnReceive(ReceiveHandler);

        // 监听心跳消息
        client.OnHeartbeat(HeartbeatHandler);

        // 开始连接
        client.Connect("127.0.0.1:3553");

        Console.ReadLine();
    }

    // 连接成功
    public static void ConnectHandler(Client client)
    {
        Console.WriteLine("connect");

        while (true)
        {
            client.Send(1, 1, new Language("C#", "Microsoft"));

            Thread.Sleep(1000);
        }
    }

    // 断开连接成功
    public static void DisconnectHandler(Client client)
    {
        Console.WriteLine("disconnect");
    }

    // 接收消息
    public static void ReceiveHandler(Client client, Message message)
    {
        var data = message.Data?.GetObject<Language>();

        if (data != null)
        {
            Language language = (Language)(data);

            Console.WriteLine(String.Format("receive msg from server; route: {0}, seq: {1}, language name: {2}, language company: {3}", message.Route, message.Seq, language.Name, language.Company));
        }
        else
        {
            Console.WriteLine(String.Format("receive msg from server; route: {0}, seq: {1}", message.Route, message.Seq));
        }
    }

    // 心跳消息处理器
    public static void HeartbeatHandler(Client client, Int64? millisecond)
    {
        if (millisecond != null)
        {
            Console.WriteLine(String.Format("server time: {0}ms", millisecond));
        }
    }
}

public struct Language(string name, string company)
{
    // 语言名称
    public string Name { get; set; } = name;

    // 所属公司
    public string Company { get; set; } = company;
}