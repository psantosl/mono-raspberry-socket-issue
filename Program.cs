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

                client.Connect(host, 7075);

                client.ReceiveTimeout = 25000;

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

                listener.Bind(new IPEndPoint(IPAddress.Any, 7075));

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

                byte[] intBuffer = new byte[4];

                ReadBytes(client, intBuffer, 4);

                int clientId = BitConverter.ToInt32(intBuffer, 0);

                while (true)
                {
                    ReadBytes(client, intBuffer, 4);

                    int sizeToSend = BitConverter.ToInt32(intBuffer, 0);

                    // just do this to put memory pressure on GC
                    // otherwise it is totally stupid to do this way
                    byte[] buffer = new byte[sizeToSend];

                    int ini = Environment.TickCount;

                    SendBytes(client, intBuffer, 4);

                    SendBytes(client, buffer, sizeToSend);

                    total += sizeToSend;

                    Console.WriteLine(
                        "[{0}] - Sent {1} bytes in {2} ms. Total {3} MB.",
                        clientId,
                        sizeToSend,
                        Environment.TickCount - ini,
                        total / 1024f / 1024f);
                }
            }

            static void ReadBytes(Socket socket, byte[] buffer, int size)
            {
                int result = 0;

                while (result < size)
                {
                    result += socket.Receive(buffer, result, size - result, SocketFlags.None);
                }
            }

            static void SendBytes(Socket socket, byte[] buffer, int size)
            {
                int sent = 0;

                while (sent < size)
                {
                    sent += socket.Send(buffer, sent, size - sent, SocketFlags.None);
                }
            }
        }
    }
}
