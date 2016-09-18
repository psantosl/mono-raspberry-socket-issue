/*============================================================
**
** Purpose: Provides a way to write primitives types in 
** binary from a Stream, while also supporting writing Strings
** in a particular encoding.
**
===========================================================*/
using System;
using System.IO;
using System.Runtime;
using System.Runtime.Serialization;
using System.Text;


#if JAVA
namespace Codice.CM.Common.Serialization
{
    public class PlasticBinaryWriter : BinaryWriter
    {
        public PlasticBinaryWriter(Stream output) : base(output)
        {
        }

        public PlasticBinaryWriter(Stream output, Encoding encoding): base(output, encoding)
        {
        }

        public void WriteBool(bool value)
        {
            base.Write(value);
        }

        public void WriteByte(byte value)
        {
            base.Write(value);
        }

        public void WriteBytes(byte[] buffer)
        {
            base.Write(buffer);
        }

        // Writes a section of a byte array to this stream.
        public void WriteBytes(byte[] buffer, int index, int count)
        {
            base.Write(buffer, index, count);
        }

        public void WriteInt16(short value)
        {
            base.Write(value);
        }

        public void WriteUInt16(ushort value)
        {
            base.Write(value);
        }

        public void WriteInt32(int value)
        {
            base.Write(value);
        }

        public void WriteUInt32(uint value)
        {
            base.Write(value);
        }

        public void WriteInt64(long value)
        {
            base.Write(value);
        }

        public void WriteUInt64(ulong value)
        {
            base.Write(value);
        }

        public void WriteString(String value)
        {
            base.Write(value);
        }

        public int GetWrittenBytes()
        {
            return 0;
        }

        public void CloseFile()
        {
            Close();
            base.OutStream.Close();
        }

        public override void Close()
        {
            if (base.OutStream.CanWrite)
                base.OutStream.Flush();
        }
    }
}
#else

namespace bananapi_socket_test
{
    // This class represents a writer that can write
    // primitives to an arbitrary stream. A subclass can override methods to
    // give unique encodings.
    //
    public class PlasticBinaryWriter
    {
        Stream mOutStream;
        byte[] _buffer;    // temp space for writing primitives to.
        Encoding _encoding;
        Encoder _encoder;

        // Perf optimization stuff
        byte[] _largeByteBuffer;  // temp space for writing chars.
        int _maxChars;   // max # of chars we can put in _largeByteBuffer
        // Size should be around the max number of chars/string * Encoding's max bytes/char
        const int LargeByteBufferSize = 256;

        public PlasticBinaryWriter(Stream output)
            : this(output, new UTF8Encoding(false, true))
        {
        }

        public PlasticBinaryWriter(Stream output, Encoding encoding)
        {
            if (output == null)
                throw new ArgumentNullException("output");
            if (encoding == null)
                throw new ArgumentNullException("encoding");
            if (!output.CanWrite)
                throw new ArgumentException("Argument_StreamNotWritable");

            mOutStream = output;
            _buffer = new byte[16];
            _encoding = encoding;
            _encoder = _encoding.GetEncoder();
        }

        // Closes this writer and releases any system resources associated with the
        // writer. Following a call to Close, any operations on the writer
        // may raise exceptions. 
        public virtual void Close()
        {
            if (mOutStream.CanWrite)
                mOutStream.Flush();
        }

        public void CloseFile()
        {
            Close();
            mOutStream.Close();
        }

        public virtual void Flush()
        {
            if (mOutStream.CanWrite)
                mOutStream.Flush();
            else
            {
                Console.WriteLine("BinaryWriter.Flush() => FAILED!!!!");
            }
        }

        public long Seek(long offset, SeekOrigin origin)
        {
            return mOutStream.Seek(offset, origin);
        }

        // Writes a boolean to this stream. A single byte is written to the stream
        // with the value 0 representing false or the value 1 representing true.
        // 
        public void WriteBool(bool value)
        {
            _buffer[0] = (byte)(value ? 1 : 0);
            WriteToStream(_buffer, 0, 1);
        }

        // Writes a byte to this stream. The current position of the stream is
        // advanced by one.
        // 
        public void WriteByte(byte value)
        {
            mOutStream.WriteByte(value);
            mBytesWritten += 1;
        }

        // Writes a byte array to this stream.
        // 
        // This default implementation calls the Write(Object, int, int)
        // method to write the byte array.
        // 
        public void WriteBytes(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            WriteToStream(buffer, 0, buffer.Length);
        }

        // Writes a section of a byte array to this stream.
        //
        // This default implementation calls the Write(Object, int, int)
        // method to write the byte array.
        // 
        public void WriteBytes(byte[] buffer, int index, int count)
        {
            WriteToStream(buffer, index, count);
        }

        // Writes a two-byte signed integer to this stream. The current position of
        // the stream is advanced by two.
        // 
        public void WriteInt16(short value)
        {
            _buffer[0] = (byte)value;
            _buffer[1] = (byte)(value >> 8);
            WriteToStream(_buffer, 0, 2);
        }

        // Writes a two-byte unsigned integer to this stream. The current position
        // of the stream is advanced by two.
        // 
        public virtual void WriteUInt16(ushort value)
        {
            _buffer[0] = (byte)value;
            _buffer[1] = (byte)(value >> 8);
            WriteToStream(_buffer, 0, 2);
        }

        // Writes a four-byte signed integer to this stream. The current position
        // of the stream is advanced by four.
        // 
        public void WriteInt32(int value)
        {
            _buffer[0] = (byte)value;
            _buffer[1] = (byte)(value >> 8);
            _buffer[2] = (byte)(value >> 16);
            _buffer[3] = (byte)(value >> 24);
            WriteToStream(_buffer, 0, 4);
        }

        // Writes a four-byte unsigned integer to this stream. The current position
        // of the stream is advanced by four.
        // 
        public void WriteUInt32(uint value)
        {
            _buffer[0] = (byte)value;
            _buffer[1] = (byte)(value >> 8);
            _buffer[2] = (byte)(value >> 16);
            _buffer[3] = (byte)(value >> 24);
            WriteToStream(_buffer, 0, 4);
        }

        // Writes an eight-byte signed integer to this stream. The current position
        // of the stream is advanced by eight.
        // 
        public void WriteInt64(long value)
        {
            _buffer[0] = (byte)value;
            _buffer[1] = (byte)(value >> 8);
            _buffer[2] = (byte)(value >> 16);
            _buffer[3] = (byte)(value >> 24);
            _buffer[4] = (byte)(value >> 32);
            _buffer[5] = (byte)(value >> 40);
            _buffer[6] = (byte)(value >> 48);
            _buffer[7] = (byte)(value >> 56);
            WriteToStream(_buffer, 0, 8);
        }

        // Writes an eight-byte unsigned integer to this stream. The current 
        // position of the stream is advanced by eight.
        // 
        public void WriteUInt64(ulong value)
        {
            _buffer[0] = (byte)value;
            _buffer[1] = (byte)(value >> 8);
            _buffer[2] = (byte)(value >> 16);
            _buffer[3] = (byte)(value >> 24);
            _buffer[4] = (byte)(value >> 32);
            _buffer[5] = (byte)(value >> 40);
            _buffer[6] = (byte)(value >> 48);
            _buffer[7] = (byte)(value >> 56);
            WriteToStream(_buffer, 0, 8);
        }

        // Writes a length-prefixed string to this stream in the PlasticBinaryWriter's
        // current Encoding. This method first writes the length of the string as 
        // a four-byte unsigned integer, and then writes that many characters 
        // to the stream.
        // 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public unsafe void WriteString(String value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            int len = _encoding.GetByteCount(value);
            Write7BitEncodedInt(len);

            if (_largeByteBuffer == null)
            {
                _largeByteBuffer = new byte[LargeByteBufferSize];
                _maxChars = LargeByteBufferSize / _encoding.GetMaxByteCount(1);
            }

            if (len <= LargeByteBufferSize)
            {
                _encoding.GetBytes(value, 0, value.Length, _largeByteBuffer, 0);
                WriteToStream(_largeByteBuffer, 0, len);
            }
            else
            {
                // Aggressively try to not allocate memory in this loop for
                // runtime performance reasons.  Use an Encoder to write out 
                // the string correctly (handling surrogates crossing buffer
                // boundaries properly).  
                int charStart = 0;
                int numLeft = value.Length;
#if _DEBUG
                int totalBytes = 0;
#endif
                while (numLeft > 0)
                {
                    // Figure out how many chars to process this round.
                    int charCount = (numLeft > _maxChars) ? _maxChars : numLeft;
                    int byteLen;
                    fixed (char* pChars = value)
                    {
                        fixed (byte* pBytes = _largeByteBuffer)
                        {
                            byteLen = _encoder.GetBytes(pChars + charStart, charCount, pBytes, LargeByteBufferSize, charCount == numLeft);
                        }
                    }
#if _DEBUG
                    totalBytes += byteLen;
                    Contract.Assert (totalBytes <= len && byteLen <= LargeByteBufferSize, "PlasticBinaryWriter::Write(String) - More bytes encoded than expected!");
#endif
                    WriteToStream(_largeByteBuffer, 0, byteLen);
                    charStart += charCount;
                    numLeft -= charCount;
                }
#if _DEBUG
                Contract.Assert(totalBytes == len, "PlasticBinaryWriter::Write(String) - Didn't write out all the bytes!");
#endif
            }
        }

        public int GetWrittenBytes()
        {
            return mBytesWritten;
        }

        void Write7BitEncodedInt(int value)
        {
            // Write out an int 7 bits at a time.  The high bit of the byte,
            // when on, tells reader to continue reading more bytes.
            uint v = (uint)value;   // support negative numbers
            while (v >= 0x80)
            {
                WriteByte((byte)(v | 0x80));
                v >>= 7;
            }
            WriteByte((byte)v);
        }

        void WriteToStream(byte[] buffer, int offset, int count)
        {
            if (count == 0)
                return;

            mOutStream.Write(buffer, offset, count);

            mBytesWritten += count;
        }

        int mBytesWritten = 0;
    }
}
#endif