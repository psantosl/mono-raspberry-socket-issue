// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Class: BinaryReader
** 
** <OWNER>gpaperin</OWNER>
**
**
** Purpose: Wraps a stream and provides convenient read functionality
** for strings and primitive types.
**
**
============================================================*/
#if JAVA
namespace Codice.CM.Common.Serialization
{
    using System;
    using System.IO;
    using System.Runtime;
    using System.Text;
    using System.Globalization;
    using System.Security;

    public class PlasticBinaryReader: BinaryReader
    {
        public static PlasticBinaryReader CreateWithAnInitialReadCount(Stream input, int readCount)
        {
            PlasticBinaryReader result = new PlasticBinaryReader(input);

            return result;
        }

        public PlasticBinaryReader(Stream input)
            : this(input, new UTF8Encoding())
        {
        }

        public PlasticBinaryReader(Stream input, Encoding encoding): base(input, encoding)
        {
        }

        public int GetReadBytes()
        {
            return 0;
        }

        //FIXME: This method should be implemented to make javacm to work with plasticprotocol
        public void AddPeekByte(byte b)
        {
            return;
        }
    }
}
#else
namespace bananapi_socket_test
{

    using System;
    using System.IO;
    using System.Runtime;
    using System.Text;
    using System.Globalization;
    using System.Security;

    public class PlasticBinaryReader
    {
        private const int MaxCharBytesSize = 128;

        private Stream m_stream;
        private byte[] m_buffer;
        private Decoder m_decoder;
        private byte[] m_charBytes;
        private char[] m_singleChar;
        private char[] m_charBuffer;
        private int m_maxCharsSize;  // From MaxCharBytesSize & Encoding

        // Performance optimization for Read() w/ Unicode.  Speeds us up by ~40% 
        private bool m_2BytesPerChar;

        public PlasticBinaryReader(Stream input)
            : this(input, new UTF8Encoding())
        {
        }

        public static PlasticBinaryReader CreateWithAnInitialReadCount(Stream input, int readCount)
        {
            PlasticBinaryReader result = new PlasticBinaryReader(input);
            result.mBytesRead = readCount;

            return result;
        }

        public PlasticBinaryReader(Stream input, Encoding encoding)
        {
            if (input == null)
            {
                throw new ArgumentNullException("input");
            }
            if (encoding == null)
            {
                throw new ArgumentNullException("encoding");
            }
            if (!input.CanRead)
                throw new ArgumentException("Argument_StreamNotReadable");

            m_stream = input;
            m_decoder = encoding.GetDecoder();
            m_maxCharsSize = encoding.GetMaxCharCount(MaxCharBytesSize);
            int minBufferSize = encoding.GetMaxByteCount(1);  // max bytes per one char
            if (minBufferSize < 16)
                minBufferSize = 16;
            m_buffer = new byte[minBufferSize];
            // m_charBuffer and m_charBytes will be left null.

            // For Encodings that always use 2 bytes per char (or more), 
            // special case them here to make Read() & Peek() faster.
            m_2BytesPerChar = encoding is UnicodeEncoding;
        }

        public virtual void Close()
        {
            m_stream.Close();
            Dispose(true);
        }

        void Dispose(bool disposing)
        {
            m_stream = null;
            m_buffer = null;
            m_decoder = null;
            m_charBytes = null;
            m_singleChar = null;
            m_charBuffer = null;
        }

        void CheckStream()
        {
            if (m_stream == null)
                throw new IOException("Stream is null");
        }

        public long Seek(long offset, SeekOrigin origin)
        {
            return m_stream.Seek(offset, origin);
        }

        public int PeekChar()
        {
            CheckStream();

            if (!m_stream.CanSeek)
                return -1;
            long origPos = m_stream.Position;
            int ch = Read();
            m_stream.Position = origPos;
            return ch;
        }

        int Read()
        {
            CheckStream();

            return InternalReadOneChar();
        }

        public bool ReadBoolean()
        {
            FillBuffer(1);
            return (m_buffer[0] != 0);
        }

        public void AddPeekByte(byte b)
        {
            mPeekByte = b;
            mByteCachedByPeek = true;
        }

        bool mByteCachedByPeek = false;
        byte mPeekByte;

        public byte ReadByte()
        {
            if (mByteCachedByPeek)
            {
                mByteCachedByPeek = false;
                return mPeekByte;
            }

            // Inlined to avoid some method call overhead with FillBuffer.
            CheckStream();

            int b = m_stream.ReadByte();
            if (b == -1)
                ThrowEndOfFile();

            ++mBytesRead;

            return (byte)b;
        }

        public sbyte ReadSByte()
        {
            FillBuffer(1);
            return (sbyte)(m_buffer[0]);
        }

        public char ReadChar()
        {
            int value = Read();
            if (value == -1)
            {
                ThrowEndOfFile();
            }
            return (char)value;
        }

        public short ReadInt16()
        {
            FillBuffer(2);
            return (short)(m_buffer[0] | m_buffer[1] << 8);
        }

        public ushort ReadUInt16()
        {
            FillBuffer(2);
            return (ushort)(m_buffer[0] | m_buffer[1] << 8);
        }

        public int ReadInt32()
        {
            /* THIS CODE is probably better and more optimal when using a MemStream
             * if (m_isMemoryStream)
            {
                if (m_stream == null) __Error.FileNotOpen();
                // read directly from MemoryStream buffer
                MemoryStream mStream = m_stream as MemoryStream;
                Contract.Assert(mStream != null, "m_stream as MemoryStream != null");

                return mStream.InternalReadInt32();
            }
            else*/
            {
                FillBuffer(4);
                return (int)(m_buffer[0] | m_buffer[1] << 8 | m_buffer[2] << 16 | m_buffer[3] << 24);
            }
        }

        public uint ReadUInt32()
        {
            FillBuffer(4);
            return (uint)(m_buffer[0] | m_buffer[1] << 8 | m_buffer[2] << 16 | m_buffer[3] << 24);
        }

        public long ReadInt64()
        {
            FillBuffer(8);
            uint lo = (uint)(m_buffer[0] | m_buffer[1] << 8 |
                             m_buffer[2] << 16 | m_buffer[3] << 24);
            uint hi = (uint)(m_buffer[4] | m_buffer[5] << 8 |
                             m_buffer[6] << 16 | m_buffer[7] << 24);
            return (long)((ulong)hi) << 32 | lo;
        }

        public ulong ReadUInt64()
        {
            FillBuffer(8);
            uint lo = (uint)(m_buffer[0] | m_buffer[1] << 8 |
                             m_buffer[2] << 16 | m_buffer[3] << 24);
            uint hi = (uint)(m_buffer[4] | m_buffer[5] << 8 |
                             m_buffer[6] << 16 | m_buffer[7] << 24);
            return ((ulong)hi) << 32 | lo;
        }

        public unsafe float ReadSingle()
        {
            FillBuffer(4);
            uint tmpBuffer = (uint)(m_buffer[0] | m_buffer[1] << 8 | m_buffer[2] << 16 | m_buffer[3] << 24);
            return *((float*)&tmpBuffer);
        }

        public unsafe double ReadDouble()
        {
            FillBuffer(8);
            uint lo = (uint)(m_buffer[0] | m_buffer[1] << 8 |
                m_buffer[2] << 16 | m_buffer[3] << 24);
            uint hi = (uint)(m_buffer[4] | m_buffer[5] << 8 |
                m_buffer[6] << 16 | m_buffer[7] << 24);

            ulong tmpBuffer = ((ulong)hi) << 32 | lo;
            return *((double*)&tmpBuffer);
        }

        public String ReadString()
        {
            CheckStream();

            int currPos = 0;
            int n;
            int stringLength;
            int readLength;
            int charsRead;

            // Length of the string in bytes, not chars
            stringLength = Read7BitEncodedInt();
            if (stringLength < 0)
            {
                throw new IOException("IO.IO_InvalidStringLen_Len", stringLength);
            }

            if (stringLength == 0)
            {
                return String.Empty;
            }

            if (m_charBytes == null)
            {
                m_charBytes = new byte[MaxCharBytesSize];
            }

            if (m_charBuffer == null)
            {
                m_charBuffer = new char[m_maxCharsSize];
            }

            StringBuilder sb = null;
            do
            {
                readLength = ((stringLength - currPos) > MaxCharBytesSize) ? MaxCharBytesSize : (stringLength - currPos);

                n = m_stream.Read(m_charBytes, 0, readLength);
                if (n == 0)
                {
                    ThrowEndOfFile();
                }

                mBytesRead += n;

                charsRead = m_decoder.GetChars(m_charBytes, 0, n, m_charBuffer, 0);

                if (currPos == 0 && n == stringLength)
                    return new String(m_charBuffer, 0, charsRead);

                if (sb == null)
                    sb = new StringBuilder(stringLength); // Actual string length in chars may be smaller.
                sb.Append(m_charBuffer, 0, charsRead);
                currPos += n;

            } while (currPos < stringLength);

            return sb.ToString();
        }

        public int Read(char[] buffer, int index, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer", "ArgumentNull_Buffer");
            }
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException("index", "ArgumentOutOfRange_NeedNonNegNum");
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count", "ArgumentOutOfRange_NeedNonNegNum");
            }
            if (buffer.Length - index < count)
            {
                throw new ArgumentException("Argument_InvalidOffLen");
            }

            CheckStream();

            // SafeCritical: index and count have already been verified to be a valid range for the buffer
            return InternalReadChars(buffer, index, count);
        }

        int InternalReadChars(char[] buffer, int index, int count)
        {
            int numBytes = 0;
            int charsRemaining = count;

            if (m_charBytes == null)
            {
                m_charBytes = new byte[MaxCharBytesSize];
            }

            while (charsRemaining > 0)
            {
                int charsRead = 0;
                // We really want to know what the minimum number of bytes per char
                // is for our encoding.  Otherwise for UnicodeEncoding we'd have to
                // do ~1+log(n) reads to read n characters.
                numBytes = charsRemaining;

                // special case for DecoderNLS subclasses when there is a hanging byte from the previous loop
                /*DecoderNLS decoder = m_decoder as DecoderNLS;
                if (decoder != null && decoder.HasState && numBytes > 1)
                {
                    numBytes -= 1;
                }*/

                if (m_2BytesPerChar)
                    numBytes <<= 1;
                if (numBytes > MaxCharBytesSize)
                    numBytes = MaxCharBytesSize;

                int position = 0;
                byte[] byteBuffer = null;
/*                if (m_isMemoryStream)
                {
                    MemoryStream mStream = m_stream as MemoryStream;
                    Contract.Assert(mStream != null, "m_stream as MemoryStream != null");

                    position = mStream.InternalGetPosition();
                    numBytes = mStream.InternalEmulateRead(numBytes);
                    byteBuffer = mStream.InternalGetBuffer();
                }
                else*/
                {
                    numBytes = m_stream.Read(m_charBytes, 0, numBytes);
                    byteBuffer = m_charBytes;
                }

                if (numBytes == 0)
                {
                    return (count - charsRemaining);
                }

                unsafe
                {
                    fixed (byte* pBytes = byteBuffer)
                    fixed (char* pChars = buffer)
                    {
                        charsRead = m_decoder.GetChars(pBytes + position, numBytes, pChars + index, charsRemaining, false);
                    }
                }

                charsRemaining -= charsRead;
                index += charsRead;
            }

            // we may have read fewer than the number of characters requested if end of stream reached 
            // or if the encoding makes the char count too big for the buffer (e.g. fallback sequence)

            mBytesRead += (count - charsRemaining);

            return (count - charsRemaining);
        }

        int InternalReadOneChar()
        {
            // I know having a separate InternalReadOneChar method seems a little 
            // redundant, but this makes a scenario like the security parser code
            // 20% faster, in addition to the optimizations for UnicodeEncoding I
            // put in InternalReadChars.   
            int charsRead = 0;
            int numBytes = 0;
            long posSav = posSav = 0;

            if (m_stream.CanSeek)
                posSav = m_stream.Position;

            if (m_charBytes == null)
            {
                m_charBytes = new byte[MaxCharBytesSize]; //
            }
            if (m_singleChar == null)
            {
                m_singleChar = new char[1];
            }

            while (charsRead == 0)
            {
                // We really want to know what the minimum number of bytes per char
                // is for our encoding.  Otherwise for UnicodeEncoding we'd have to
                // do ~1+log(n) reads to read n characters.
                // Assume 1 byte can be 1 char unless m_2BytesPerChar is true.
                numBytes = m_2BytesPerChar ? 2 : 1;

                int r = m_stream.ReadByte();
                m_charBytes[0] = (byte)r;
                if (r == -1)
                    numBytes = 0;
                if (numBytes == 2)
                {
                    r = m_stream.ReadByte();
                    m_charBytes[1] = (byte)r;
                    if (r == -1)
                        numBytes = 1;
                }

                if (numBytes == 0)
                {
                    // Console.WriteLine("Found no bytes.  We're outta here.");
                    return -1;
                }

                try
                {

                    charsRead = m_decoder.GetChars(m_charBytes, 0, numBytes, m_singleChar, 0);
                }
                catch
                {
                    // Handle surrogate char 

                    if (m_stream.CanSeek)
                        m_stream.Seek((posSav - m_stream.Position), SeekOrigin.Current);
                    // else - we can't do much here

                    throw;
                }
            }
            if (charsRead == 0)
                return -1;

            mBytesRead += charsRead;

            return m_singleChar[0];
        }

        public char[] ReadChars(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count", "ArgumentOutOfRange_NeedNonNegNum");
            }

            CheckStream();

            if (count == 0)
            {
                return new char[0];
            }

            // SafeCritical: we own the chars buffer, and therefore can guarantee that the index and count are valid
            char[] chars = new char[count];
            int n = InternalReadChars(chars, 0, count);
            if (n != count)
            {
                char[] copy = new char[n];
                Buffer.BlockCopy(chars, 0, copy, 0, 2 * n); // sizeof(char)
                chars = copy;
            }

            return chars;
        }

        public int Read(byte[] buffer, int index, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer", "ArgumentNull_Buffer");
            if (index < 0)
                throw new ArgumentOutOfRangeException("index", "ArgumentOutOfRange_NeedNonNegNum");
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", "ArgumentOutOfRange_NeedNonNegNum");
            if (buffer.Length - index < count)
                throw new ArgumentException("Argument_InvalidOffLen");

            CheckStream();

            int result = 0;

            while (result < count)
            {
                result += m_stream.Read(buffer, index + result, count - result);
            }

            mBytesRead += result;

            return result;
        }

        public int SpecialRead(byte[] buffer, int index, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer", "ArgumentNull_Buffer");
            if (index < 0)
                throw new ArgumentOutOfRangeException("index", "ArgumentOutOfRange_NeedNonNegNum");
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", "ArgumentOutOfRange_NeedNonNegNum");
            if (buffer.Length - index < count)
                throw new ArgumentException("Argument_InvalidOffLen");

            CheckStream();

            int result = 0;


            while (result < count)
            {
                result += m_stream.Read(buffer, index + result, count - result);

            }

            mBytesRead += result;

            return result;
        }

        public void ReadBytes(byte[] buffer, int index, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer", "ArgumentNull_Buffer");
            if (index < 0)
                throw new ArgumentOutOfRangeException("index", "ArgumentOutOfRange_NeedNonNegNum");
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", "ArgumentOutOfRange_NeedNonNegNum");
            if (buffer.Length - index < count)
                throw new ArgumentException("Argument_InvalidOffLen");

            CheckStream();

            int numRead = 0;
            do
            {
                int n = m_stream.Read(buffer, index + numRead, count);
                if (n == 0)
                    ThrowEndOfFile();
                numRead += n;
                count -= n;
            } while (count > 0);

            mBytesRead += numRead;
        }

        public byte[] ReadBytes(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException("count", "ArgumentOutOfRange_NeedNonNegNum");

            CheckStream();

            if (count == 0)
            {
                return new byte[0];
            }

            byte[] result = new byte[count];

            int numRead = 0;
            do
            {
                int n = m_stream.Read(result, numRead, count);
                if (n == 0)
                    break;
                numRead += n;
                count -= n;
            } while (count > 0);

            if (numRead != result.Length)
            {
                // Trim array.  This should happen on EOF & possibly net streams.
                byte[] copy = new byte[numRead];
                Buffer.BlockCopy(result, 0, copy, 0, numRead);
                result = copy;
            }

            mBytesRead += numRead;

            return result;
        }

        void FillBuffer(int numBytes)
        {
            if (m_buffer != null && (numBytes < 0 || numBytes > m_buffer.Length))
            {
                throw new ArgumentOutOfRangeException("numBytes", "ArgumentOutOfRange_BinaryReaderFillBuffer");
            }
            int bytesRead = 0;
            int n = 0;

            CheckStream();

            // Need to find a good threshold for calling ReadByte() repeatedly
            // vs. calling Read(byte[], int, int) for both buffered & unbuffered
            // streams.
            if (numBytes == 1)
            {
                n = m_stream.ReadByte();
                if (n == -1)
                    ThrowEndOfFile();
                m_buffer[0] = (byte)n;

                ++mBytesRead;

                return;
            }

            do
            {
                n = m_stream.Read(m_buffer, bytesRead, numBytes - bytesRead);
                if (n == 0)
                {
                    ThrowEndOfFile();
                }
                bytesRead += n;
            } while (bytesRead < numBytes);

            mBytesRead += bytesRead;
        }

        public int GetReadBytes()
        {
            return mBytesRead;
        }

        internal int Read7BitEncodedInt()
        {
            // Read out an Int32 7 bits at a time.  The high bit
            // of the byte when on means to continue reading more bytes.
            int count = 0;
            int shift = 0;
            byte b;
            do
            {
                // Check for a corrupted stream.  Read a max of 5 bytes.
                // In a future version, add a DataFormatException.
                if (shift == 5 * 7)  // 5 bytes max per Int32, shift += 7
                    throw new FormatException("Format_Bad7BitInt32");

                // ReadByte handles end of stream cases for us.
                b = ReadByte();
                count |= (b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);
            return count;
        }

        void ThrowEndOfFile()
        {
            throw new IOException("End of file");
        }

        int mBytesRead = 0;
    }
}
#endif