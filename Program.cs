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

                client.ReceiveTimeout = 15000;

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
                            bytesToReceive,
                            Environment.TickCount - ini,
                            total / 1024 / 1024);
                    }
                }
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
                    var reader = new BinaryReader(buffered);
                    var writer = new BinaryWriter(buffered);

                    int clientId = reader.ReadInt32();

                    while (true)
                    {

                        int sizeToSend = reader.ReadInt32();

                        // just do this to put memory pressure on GC
                        // otherwise it is totally stupid to do this way
                        byte[] buffer = new byte[sizeToSend];

                        int ini = Environment.TickCount;

                        writer.Write(sizeToSend);

                        writer.Write(buffer, 0, sizeToSend);

                        writer.Flush();

                        total += sizeToSend;

                        Console.WriteLine(
                            "[{0}] - Sent {1} bytes in {2} ms. Total {3} MB.",
                            clientId,
                            sizeToSend,
                            Environment.TickCount - ini,
                            total / 1024f / 1024f);
                    }
                }
            }
        }
    }
}
