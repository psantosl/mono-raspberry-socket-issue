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
                RunServer();
                return;
            }

            RunClient(args[0]);
        }

        static void RunClient(string host)
        {
            Socket client = new Socket(SocketType.Stream, ProtocolType.Tcp);

            client.Connect(host, 7074);

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

        static void RunServer()
        {
            Socket listener = new Socket(SocketType.Stream, ProtocolType.Tcp);

            listener.Bind(new IPEndPoint(IPAddress.Any, 7074));

            listener.Listen(5);

            Socket client = listener.Accept();

            long total = 0;

            while (true)
            {
                byte[] buffer = new byte[5 * 1024 * 1024];

                using (NetworkStream ns = new NetworkStream(client))
                using (BufferedStream buffered = new BufferedStream(ns))
                using (BinaryReader reader = new BinaryReader(buffered))
                using (BinaryWriter writer = new BinaryWriter(buffered))
                {
                    int sizeToSend = reader.ReadInt32();

                    int ini = Environment.TickCount;

                    writer.Write(sizeToSend);

                    writer.Write(buffer, 0, sizeToSend);

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
    }
}
