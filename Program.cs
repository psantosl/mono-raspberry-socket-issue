using System;

using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

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

            int clients = 1;

            if (!int.TryParse(args[1], out clients))
                clients = 1;

            for (int i = 0; i < clients - 1; ++i)
            {
                Thread t = new Thread(new ParameterizedThreadStart(Client.Run));
                t.Start(args[0]);
            }

            Client.Run(args[0]);
        }

        static int ThCount = 0;

        static class Client
        {
            internal static void Run(object h)
            {
                string host = h as string;

                int thId = Interlocked.Increment(ref ThCount);

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
                    writer.Write(thId);

                    while (true)
                    {
                        int bytesToReceive = rnd.Next(512 * 1024, 4 * 1024 * 1024);

                        int ini = Environment.TickCount;

                        //Console.Write("Going to request {0} bytes. ", bytesToReceive);

                        writer.Write(bytesToReceive);

                        writer.Flush();

                        reader.ReadInt32();

                        int result = 0;

                        while (result < bytesToReceive)
                        {
                            result += ns.Read(buffer, result, bytesToReceive - result);

                            //Console.Write(".");
                        }

                        total += bytesToReceive;

                        Console.WriteLine("Th: {0}. Received {1} bytes in {2} ms. Total {3} MB",
                            thId,
                            Environment.TickCount - ini,
                            bytesToReceive,
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

                while (true)
                {
                    Socket client = listener.Accept();

                    ConfigureSocketParams(client);

                    Thread t = new Thread(new ParameterizedThreadStart(AttendClient));
                    t.Start(client);
                }
            }

            static void AttendClient(object s)
            {
                Socket client = s as Socket;

                long total = 0;

                using (NetworkStream ns = new NetworkStream(client))
                using (BufferedStream buffered = new BufferedStream(ns))
                {
                    var reader = new PlasticBinaryReader(buffered);
                    var writer = new PlasticBinaryWriter(buffered);

                    int clientId = reader.ReadInt32();

                    while (true)
                    {

                        int sizeToSend = reader.ReadInt32();

                        // just do this to put memory pressure on GC
                        // otherwise it is totally stupid to do this way
                        byte[] buffer = new byte[sizeToSend];

                        int ini = Environment.TickCount;

                        writer.WriteInt32(sizeToSend);

                        writer.WriteBytes(buffer, 0, sizeToSend);

                        writer.Flush();

                        total += sizeToSend;

                        Console.WriteLine(
                            "[{0}] - Sent {1} bytes in {2} ms. Total {3} MB. GC collections: 0:{4} - 1:{5} - 2:{6}",
                            clientId,
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
