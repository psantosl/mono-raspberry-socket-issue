using System;

using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace bananapi_socket_test
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args[0] == "server")
            {
                Server.Run();
                return;
            }

            Client.Run(args[0]);
        }

        static class Client
        {
            internal static void Run(string host)
            {
                Socket client = new Socket(SocketType.Stream, ProtocolType.Tcp);

                client.Connect(host, 7074);

                KeepAlive(client);

                byte[] buffer = new byte[5 * 1024 * 1024];

                Random rnd = new Random();

                long total = 0;

                using (NetworkStream ns = new NetworkStream(client))
                using (BinaryWriter writer = new BinaryWriter(ns))
                using (BinaryReader reader = new BinaryReader(ns))
                {
                    while (true)
                    {
                        int bytesToReceive = rnd.Next(2 * 1024 * 1024, 4 * 1024 * 1024);

                        int ini = Environment.TickCount;

                        Console.Write("Going to request {0} bytes. ", bytesToReceive);

                        writer.Write(bytesToReceive);

                        writer.Flush();

                        reader.ReadInt32();

                        int result = 0;

                        while (result < bytesToReceive)
                        {
                            result += ns.Read(buffer, result, bytesToReceive - result);

                            Console.Write(".");
                        }

                        total += bytesToReceive;

                        Console.WriteLine(". Received in {0} ms. Total {1} MB",
                            Environment.TickCount - ini,
                            total / 1024 / 1024);
                    }
                }
            }

            static void KeepAlive(Socket socket)
            {
                byte[] keepAliveOptions = GetKeepAliveOptions(true, 30 * 1000, 15 * 1000);
                byte[] result = BitConverter.GetBytes(0);
                socket.IOControl(IOControlCode.KeepAliveValues, keepAliveOptions, result);
            }

            static byte[] GetKeepAliveOptions(bool enabled, int keepAliveTime, int keepAliveInterval)
            {
                const int BYTES_PER_INT = 4;
                byte[] options = new byte[3 * BYTES_PER_INT];

                BitConverter.GetBytes((uint)(enabled ? 1 : 0)).CopyTo(options, BYTES_PER_INT * 0);
                BitConverter.GetBytes((uint)keepAliveTime).CopyTo(options, BYTES_PER_INT * 1);
                BitConverter.GetBytes((uint)keepAliveInterval).CopyTo(options, BYTES_PER_INT * 2);

                return options;
            }
        }

        static class Server
        {

            internal static void Run()
            {
                Socket listener = new Socket(SocketType.Stream, ProtocolType.Tcp);

                listener.Bind(new IPEndPoint(IPAddress.Any, 7074));

                listener.Listen(5);

                Socket client = listener.Accept();

                ConfigureSocketParams(client);

                long total = 0;

                while (true)
                {
                    byte[] buffer = new byte[5 * 1024 * 1024];

                    using (NetworkStream ns = new NetworkStream(client))
                    using (BufferedStream buffered = new BufferedStream(ns))
                    {
                        var reader = new PlasticBinaryReader(buffered);
                        var writer = new PlasticBinaryWriter(buffered);

                        int sizeToSend = reader.ReadInt32();

                        int ini = Environment.TickCount;

                        writer.WriteInt32(sizeToSend);

                        writer.WriteBytes(buffer, 0, sizeToSend);

                        writer.Flush();

                        total += sizeToSend;

                        Console.WriteLine(
                            "Sent {0} bytes in {1} ms. Total {2} MB. GC collections: 0:{3} - 1:{4} - 2:{5}",
                            sizeToSend,
                            Environment.TickCount - ini,
                            total / 1024f / 1024f,
                            GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
                    }
                }
            }

            static void ConfigureSocketParams(Socket socket)
            {
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.Debug, 1);
                LingerOption optionValue = new LingerOption(true, 0);

                socket.SetSocketOption(SocketOptionLevel.Socket,
                    SocketOptionName.Linger, optionValue);

                /*SetSocketOption.Set(socket, SetSocketOption.Option.SendTimeout,
                    mChannelProperties.SocketConfigParams.SendTimeout);

                SetSocketOption.Set(socket, SetSocketOption.Option.ReceiveTimeout,
                    mChannelProperties.SocketConfigParams.ReceiveTimeout);

                SetSocketOption.Set(socket, SetSocketOption.Option.SendBufferSize,
                    mChannelProperties.SocketConfigParams.SendBufferSize);

                SetSocketOption.Set(socket, SetSocketOption.Option.ReceiveBufferSize,
                    mChannelProperties.SocketConfigParams.ReceiveBufferSize);*/
            }


            static class SetSocketOption
            {
                internal enum Option { SendTimeout, ReceiveTimeout, SendBufferSize, ReceiveBufferSize };

                internal static void Set(Socket socket, Option option, int value)
                {
                    if (value == -1)
                        return;

                    int newValue = -1;

                    switch (option)
                    {
                        case Option.SendTimeout:
                            socket.SendTimeout = value;
                            newValue = socket.SendTimeout;
                            break;

                        case Option.ReceiveTimeout:
                            socket.ReceiveTimeout = value;
                            newValue = socket.ReceiveTimeout;
                            break;

                        case Option.SendBufferSize:
                            socket.SendBufferSize = value;
                            newValue = socket.SendBufferSize;
                            break;

                        case Option.ReceiveBufferSize:
                            socket.ReceiveBufferSize = value;
                            newValue = socket.ReceiveBufferSize;
                            break;
                    }

                    if (newValue != value)
                    {
                        Console.WriteLine("Error setting {0} to socket. Tried to set {1} but value is {2}",
                            option.ToString(), value, newValue);
                    }
                }
            }
        }
    }
}
