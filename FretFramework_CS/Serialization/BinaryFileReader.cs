using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Framework.Serialization
{
    public enum Endianness
    {
        LittleEndian = 0,
        BigEndian = 1,
    };

    public unsafe class BinaryFileReader : IDisposable
    {
        private readonly FrameworkFile file;

        private int boundaryIndex = 0;
        private readonly int* boundaries;
        private int currentBoundary;
        private bool disposed = false;

        private int _position;

        public int Position
        {
            get { return _position; }
            set
            {
                if (value > boundaries[boundaryIndex])
                    throw new ArgumentOutOfRangeException("value");
                _position = value;
            }
        }
        public int Boundary { get { return currentBoundary; } }

        public BinaryFileReader(FrameworkFile file)
        {
            this.file = file;
            boundaries = (int*)Marshal.AllocHGlobal(sizeof(int) * 8);
            currentBoundary = boundaries[0] = file.Length;
        }
        public BinaryFileReader(byte[] data) : this(new FrameworkFile_Handle(data)) {}
        public BinaryFileReader(string path) : this(new FrameworkFile_Alloc(path)) {}

        private void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    if (file is FrameworkFile_Handle handle)
                        handle.Dispose();
                    else if (file is FrameworkFile_Alloc alloc)
                        alloc.Dispose();
                }
                Marshal.FreeHGlobal((IntPtr)boundaries);
            }
            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void ExitSection()
        {
            _position = currentBoundary;
            if (boundaryIndex == 0)
                throw new Exception("ayo wtf bro");
            currentBoundary = boundaries[--boundaryIndex];
        }

        public void EnterSection(int length)
        {
            int boundary = _position + length;
            if (boundary > boundaries[boundaryIndex])
                throw new Exception("Invalid length for section");
            if (boundaryIndex == 7)
                throw new Exception("Nested Buffer limit reached");
            currentBoundary = boundaries[++boundaryIndex] = boundary;
        }

        public bool CompareTag(byte[] tag)
        {
            Debug.Assert(tag.Length == 4);
            if (tag[0] != file.ptr[_position] ||
                tag[1] != file.ptr[_position + 1] ||
                tag[2] != file.ptr[_position + 2] ||
                tag[3] != file.ptr[_position + 3])
                return false;

            _position += 4;
            return true;
        }

        public bool Move(int amount)
        {
            if (_position + amount > currentBoundary)
                return false;

            _position += amount;
            return true;
        }

        public void Move_Unsafe(int amount)
        {
            _position += amount;
        }

        public byte PeekByte()
        {
            return file.ptr[_position];
        }

        public bool ReadByte(ref byte value)
        {
            if (_position >= currentBoundary)
                return false;

            value = file.ptr[_position++];
            return true;
        }

        public bool ReadSByte(ref sbyte value)
        {
            if (_position >= currentBoundary)
                return false;

            value = (sbyte)file.ptr[_position++];
            return true;
        }
        
        public bool ReadInt16(ref short value, Endianness endianness = Endianness.LittleEndian)
        {
            if (_position + 2 > currentBoundary)
                return false;

            Span<byte> span = new(file.ptr + _position, 2);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadInt16LittleEndian(span);
            else
                value = BinaryPrimitives.ReadInt16BigEndian(span);
            _position += 2;
            return true;
        }
        
        public bool ReadUInt16(ref ushort value, Endianness endianness = Endianness.LittleEndian)
        {
            if (_position + 2 > currentBoundary)
                return false;

            Span<byte> span = new(file.ptr + _position, 2);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadUInt16LittleEndian(span);
            else
                value = BinaryPrimitives.ReadUInt16BigEndian(span);
            _position += 2;
            return true;
        }
        
        public bool ReadInt32(ref int value, Endianness endianness = Endianness.LittleEndian)
        {
            if (_position + 4 > currentBoundary)
                return false;

            Span<byte> span = new(file.ptr + _position, 4);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadInt32LittleEndian(span);
            else
                value = BinaryPrimitives.ReadInt32BigEndian(span);
            _position += 4;
            return true;
        }
        
        public bool ReadUInt32(ref uint value, Endianness endianness = Endianness.LittleEndian)
        {
            if (_position + 4 > currentBoundary)
                return false;

            Span<byte> span = new(file.ptr + _position, 4);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadUInt32LittleEndian(span);
            else
                value = BinaryPrimitives.ReadUInt32BigEndian(span);
            _position += 4;
            return true;
        }
        
        public bool ReadInt64(ref long value, Endianness endianness = Endianness.LittleEndian)
        {
            if (_position + 8 > currentBoundary)
                return false;

            Span<byte> span = new(file.ptr + _position, 8);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadInt64LittleEndian(span);
            else
                value = BinaryPrimitives.ReadInt64BigEndian(span);
            Position += 8;
            return true;
        }
        
        public bool ReadUInt64(ref ulong value, Endianness endianness = Endianness.LittleEndian)
        {
            if (_position + 8 > currentBoundary)
                return false;

            Span<byte> span = new(file.ptr + _position, 8);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadUInt64LittleEndian(span);
            else
                value = BinaryPrimitives.ReadUInt64BigEndian(span);
            _position += 8;
            return true;
        }
        public byte ReadByte()
        {
            if (_position >= currentBoundary)
                throw new Exception("Failed to parse data");

            return file.ptr[_position++];
        }

        public ref byte ReadByte_Ref()
        {
            if (_position >= currentBoundary)
                throw new Exception("Failed to parse data");
            return ref file.ptr[_position++];
        }

        public sbyte ReadSByte()
        {
            if (_position >= currentBoundary)
                throw new Exception("Failed to parse data");

            return (sbyte)file.ptr[_position++];
        }
        public short ReadInt16(Endianness endianness = Endianness.LittleEndian)
        {
            short value = default;
            if (!ReadInt16(ref value, endianness))
                throw new Exception("Failed to parse data");
            return value;
        }
        public ushort ReadUInt16(Endianness endianness = Endianness.LittleEndian)
        {
            ushort value = default;
            if (!ReadUInt16(ref value, endianness))
                throw new Exception("Failed to parse data");
            return value;
        }
        public int ReadInt32(Endianness endianness = Endianness.LittleEndian)
        {
            int value = default;
            if (!ReadInt32(ref value, endianness))
                throw new Exception("Failed to parse data");
            return value;
        }
        public uint ReadUInt32(Endianness endianness = Endianness.LittleEndian)
        {
            uint value = default;
            if (!ReadUInt32(ref value, endianness))
                throw new Exception("Failed to parse data");
            return value;
        }
        public long ReadInt64(Endianness endianness = Endianness.LittleEndian)
        {
            long value = default;
            if (!ReadInt64(ref value, endianness))
                throw new Exception("Failed to parse data");
            return value;
        }
        public ulong ReadUInt64(Endianness endianness = Endianness.LittleEndian)
        {
            ulong value = default;
            if (!ReadUInt64(ref value, endianness))
                throw new Exception("Failed to parse data");
            return value;
        }
        public bool ReadBytes(byte[] bytes)
        {
            if (_position + bytes.Length > currentBoundary)
                return false;

            Marshal.Copy((IntPtr)(file.ptr + _position), bytes, 0, bytes.Length);
            _position += bytes.Length;
            return true;
        }

        public byte[] ReadBytes(int length)
        {
            byte[] bytes = new byte[length];
            if (!ReadBytes(bytes))
                throw new Exception("Failed to parse data");
            return bytes;
        }

        public uint ReadVLQ()
        {
            uint value = 0;
            uint i = 0;
            while (true)
            {
                if (_position >= currentBoundary)
                    throw new Exception("Failed to parse data");

                uint b = file.ptr[_position++];
                value |= b & 127;
                if (b < 128)
                    return value;

                if (i == 3)
                    throw new Exception("Invalid variable length quantity");

                value <<= 7;
                ++i;
            }     
        }

        public ReadOnlySpan<byte> ReadSpan(int length)
        {
            if (_position + length > currentBoundary)
                throw new Exception("Failed to parse data");
            ReadOnlySpan<byte> span = new(file.ptr + _position, length);
            _position += length;
            return span;
        }
    };
}
