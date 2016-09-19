using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Sockets;

namespace bananapi_socket_test
{
    class MyNetworkStream : Stream
    {
        internal MyNetworkStream(Socket socket)
        {
            mSocket = socket;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return mSocket.Receive(buffer, offset, count, SocketFlags.None);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            int sent = 0;

            while (sent < count)
            {
                sent += mSocket.Send(buffer, offset + sent, count - sent, SocketFlags.None);
            }
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override void Flush()
        {
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        Socket mSocket;
    }
}
