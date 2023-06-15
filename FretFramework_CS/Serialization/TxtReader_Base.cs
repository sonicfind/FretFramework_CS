using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Serialization
{
    public unsafe abstract class TxtReader_Base : IDisposable
    {
        public readonly FrameworkFile file;

        protected int _position;

        public int Position
        {
            get { return _position; }
            set
            {
                if (value < _position && _position <= file.Length)
                    throw new ArgumentOutOfRangeException("Position");
                _position = value;
            }
        }

        protected int _next;
        public int Next { get { return _next; } }

        public byte PeekByte()
        {
            return file.ptr[_position];
        }

        public byte* CurrentPtr { get { return file.ptr + _position; } }

        public TxtReader_Base(FrameworkFile file)
        {
            this.file = file;
        }
        public TxtReader_Base(byte[] data) : this(new FrameworkFile_Handle(data)) { }
        public TxtReader_Base(string path) : this(new FrameworkFile_Alloc(path)) { }

        public void Dispose()
        {
            if (file is FrameworkFile_Handle handle)
                handle.Dispose();
            else if (file is FrameworkFile_Alloc alloc)
                alloc.Dispose();
            GC.SuppressFinalize(this);
        }

        public abstract void SkipWhiteSpace();

        public bool IsEndOfFile()
        {
            return _position >= file.Length;
        }

        public bool ReadBoolean(ref bool value)
        {
            value = file.ptr[_position] switch
            {
                (byte)'0' => false,
                (byte)'1' => true,
                _ => _position + 4 <= _next &&
                                    (file.ptr[_position] == 't' || file.ptr[_position] == 'T') &&
                                    (file.ptr[_position + 1] == 'r' || file.ptr[_position + 1] == 'R') &&
                                    (file.ptr[_position + 2] == 'u' || file.ptr[_position + 2] == 'U') &&
                                    (file.ptr[_position + 3] == 'e' || file.ptr[_position + 3] == 'E'),
            };
            return true;
        }

        public bool ReadByte(ref byte value)
        {
            if (_position >= _next)
                return false;

            value = file.ptr[_position++];
            SkipWhiteSpace();
            return true;
        }

        public bool ReadSByte(ref sbyte value)
        {
            if (_position >= _next)
                return false;

            value = (sbyte)file.ptr[_position++];
            SkipWhiteSpace();
            return true;
        }

        public bool ReadInt16(ref short value)
        {
            if (_position >= _next)
                return false;

            short b = file.ptr[_position];
            if (b != '-')
            {
                if (b == '+')
                {
                    ++_position;
                    if (_position == _next)
                        return false;
                    b = file.ptr[_position];
                }

                if (b < '0' || b > '9')
                    return false;

                while (true)
                {
                    _position++;
                    short val = (short)(value + b - '0');
                    if (val >= value)
                    {
                        value = val;
                        if (_position == _next)
                            break;

                        b = file.ptr[_position];
                        if (b < '0' || b > '9')
                            break;

                        val *= 10;
                        if (val >= value)
                            value = val;
                        else
                            value = short.MaxValue;
                    }
                    else
                        value = short.MaxValue;
                }
            }
            else
            {
                ++_position;
                if (_position == _next)
                    return false;

                b = file.ptr[_position];
                if (b < '0' || b > '9')
                    return false;

                while (true)
                {
                    _position++;
                    short val = (short)(value - (b - '0'));
                    if (val <= value)
                    {
                        value = val;
                        if (_position == _next)
                            break;

                        b = file.ptr[_position];
                        if (b < '0' || b > '9')
                            break;

                        val *= 10;
                        if (val <= value)
                            value = val;
                        else
                            value = short.MinValue;
                    }
                    else
                        value = short.MinValue;
                }
            }

            SkipWhiteSpace();
            return true;
        }

        public bool ReadUInt16(ref ushort value)
        {
            if (_position >= _next)
                return false;

            ushort b = file.ptr[_position];
            if (b == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                b = file.ptr[_position];
            }

            if (b < '0' || b > '9')
                return false;

            while (true)
            {
                _position++;
                ushort val = (ushort)(value + b - '0');
                if (val >= value)
                {
                    value = val;
                    if (_position == _next)
                        break;

                    b = file.ptr[_position];
                    if (b < '0' || b > '9')
                        break;

                    val *= 10;
                    if (val >= value)
                        value = val;
                    else
                        value = ushort.MaxValue;
                }
                else
                    value = ushort.MaxValue;
            }
            SkipWhiteSpace();
            return true;
        }

        public bool ReadInt32(ref int value)
        {
            if (_position >= _next)
                return false;

            int b = file.ptr[_position];
            if (b != '-')
            {
                if (b == '+')
                {
                    ++_position;
                    if (_position == _next)
                        return false;
                    b = file.ptr[_position];
                }

                if (b < '0' || b > '9')
                    return false;

                while (true)
                {
                    _position++;
                    int val = value + b - '0';
                    if (val >= value)
                    {
                        value = val;
                        if (_position == _next)
                            break;

                        b = file.ptr[_position];
                        if (b < '0' || b > '9')
                            break;

                        val *= 10;
                        if (val >= value)
                            value = val;
                        else
                            value = int.MaxValue;
                    }
                    else
                        value = int.MaxValue;
                }
            }
            else
            {
                ++_position;
                if (_position == _next)
                    return false;

                b = file.ptr[_position];
                if (b < '0' || b > '9')
                    return false;

                while (true)
                {
                    _position++;
                    int val = value - (b - '0');
                    if (val <= value)
                    {
                        value = val;
                        if (_position == _next)
                            break;

                        b = file.ptr[_position];
                        if (b < '0' || b > '9')
                            break;

                        val *= 10;
                        if (val <= value)
                            value = val;
                        else
                            value = int.MinValue;
                    }
                    else
                        value = int.MinValue;
                }
            }
            SkipWhiteSpace();
            return true;
        }

        public bool ReadUInt32(ref uint value)
        {
            if (_position >= _next)
                return false;

            uint b = file.ptr[_position];
            if (b == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                b = file.ptr[_position];
            }

            if (b < '0' || b > '9')
                return false;

            while (true)
            {
                _position++;
                uint val = value + b - '0';
                if (val >= value)
                {
                    value = val;
                    if (_position == _next)
                        break;

                    b = file.ptr[_position];
                    if (b < '0' || b > '9')
                        break;

                    val *= 10;
                    if (val >= value)
                        value = val;
                    else
                        value = uint.MaxValue;
                }
                else
                    value = uint.MaxValue;
            }
            SkipWhiteSpace();
            return true;
        }

        public bool ReadInt64(ref long value)
        {
            if (_position >= _next)
                return false;

            long b = file.ptr[_position];
            if (b != '-')
            {
                if (b == '+')
                {
                    ++_position;
                    if (_position == _next)
                        return false;
                    b = file.ptr[_position];
                }

                if (b < '0' || b > '9')
                    return false;

                while (true)
                {
                    _position++;
                    long val = value + b - '0';
                    if (val >= value)
                    {
                        value = val;
                        if (_position == _next)
                            break;

                        b = file.ptr[_position];
                        if (b < '0' || b > '9')
                            break;

                        val *= 10;
                        if (val >= value)
                            value = val;
                        else
                            value = long.MaxValue;
                    }
                    else
                        value = long.MaxValue;
                }
            }
            else
            {
                ++_position;
                if (_position == _next)
                    return false;

                b = file.ptr[_position];
                if (b < '0' || b > '9')
                    return false;

                while (true)
                {
                    _position++;
                    long val = value - (b - '0');
                    if (val <= value)
                    {
                        value = val;
                        if (_position == _next)
                            break;

                        b = file.ptr[_position];
                        if (b < '0' || b > '9')
                            break;

                        val *= 10;
                        if (val <= value)
                            value = val;
                        else
                            value = long.MinValue;
                    }
                    else
                        value = long.MinValue;
                }
            }
            SkipWhiteSpace();
            return true;
        }

        public bool ReadUInt64(ref ulong value)
        {
            if (_position >= _next)
                return false;

            ulong b = file.ptr[_position];
            if (b == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                b = file.ptr[_position];
            }

            if (b < '0' || b > '9')
                return false;

            while (true)
            {
                _position++;
                ulong val = value + b - '0';
                if (val >= value)
                {
                    value = val;
                    if (_position == _next)
                        break;

                    b = file.ptr[_position];
                    if (b < '0' || b > '9')
                        break;

                    val *= 10;
                    if (val >= value)
                        value = val;
                    else
                        value = ulong.MaxValue;
                }
                else
                    value = ulong.MaxValue;
            }
            SkipWhiteSpace();
            return true;
        }

        public bool ReadNUint(ref nuint value)
        {
            if (_position >= _next)
                return false;

            nuint b = file.ptr[_position];
            if (b == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                b = file.ptr[_position];
            }

            if (b < '0' || b > '9')
                return false;

            while (true)
            {
                _position++;
                nuint val = value + b - '0';
                if (val >= value)
                {
                    value = val;
                    if (_position == _next)
                        break;

                    b = file.ptr[_position];
                    if (b < '0' || b > '9')
                        break;

                    val *= 10;
                    if (val >= value)
                        value = val;
                    else
                        value = nuint.MaxValue;
                }
                else
                    value = nuint.MaxValue;
            }
            SkipWhiteSpace();
            return true;
        }

        public bool ReadFloat(ref float value)
        {
            if (_position >= _next)
                return false;

            byte b = file.ptr[_position];
            bool isNegative = false;

            if (b == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                b = file.ptr[_position];
            }
            else if (b == '-')
            {
                ++_position;
                if (_position == _next)
                    return false;
                b = file.ptr[_position];
                isNegative = true;
            }

            if (b > '9')
                return false;

            if (b < '0' && b != '.')
                return false;

            if (b != '.')
            {
                while (true)
                {
                    value += b - '0';
                    ++_position;
                    if (_position == _next)
                        break;

                    b = file.ptr[_position];
                    if (b < '0' || b > '9')
                        break;

                    value *= 10;
                }
            }

            if (b == '.')
            {
                ++_position;

                float dec = 0;
                int count = 0;
                if (_position < _next)
                {
                    b = file.ptr[_position];
                    if ('0' <= b && '9' <= b)
                    {
                        while (true)
                        {
                            dec += b - '0';
                            ++_position;
                            if (_position == _next)
                                break;

                            b = file.ptr[_position];
                            if (b < '0' || b > '9')
                                break;
                            dec *= 10;
                            ++count;
                        }
                    }
                }

                for (int i = 0; i < count; ++i)
                    dec /= 10;

                value += dec;
            }

            if (isNegative)
                value = -value;

            SkipWhiteSpace();
            return true;
        }

        public bool ReadDouble(ref double value)
        {
            if (_position >= _next)
                return false;

            byte b = file.ptr[_position];
            bool isNegative = false;

            if (b == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
            }
            else if (b == '-')
            {
                ++_position;
                if (_position == _next)
                    return false;
                isNegative = true;
            }

            if (b > '9')
                return false;

            if (b < '0' && b != '.')
                return false;

            if (b != '.')
            {
                while (true)
                {
                    ++_position;
                    value += b - '0';
                    if (_position == _next)
                        break;

                    b = file.ptr[_position];
                    if (b < '0' || b > '9')
                        break;

                    value *= 10;
                }
            }

            if (b == '.')
            {
                ++_position;

                double dec = 0;
                int count = 0;
                if (_position < _next)
                {
                    b = file.ptr[_position];
                    if ('0' <= b && '9' <= b)
                    {
                        while (true)
                        {
                            dec += b - '0';
                            ++_position;
                            if (_position == _next)
                                break;

                            b = file.ptr[_position];
                            if (b < '0' || b > '9')
                                break;
                            dec *= 10;
                            ++count;
                        }
                    }
                }

                for (int i = 0; i < count; ++i)
                    dec /= 10;

                value += dec;
            }

            if (isNegative)
                value = -value;

            SkipWhiteSpace();
            return true;
        }

        public bool ReadBoolean()
        {
            bool value = default;
            if (!ReadBoolean(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }

        public byte ReadByte()
        {
            byte value = default;
            if (!ReadByte(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }

        public ref byte ReadByte_Ref()
        {
            if (_position + 1 > _next)
                throw new Exception("Failed to parse data");
            return ref file.ptr[_position++];
        }

        public sbyte ReadSByte()
        {
            sbyte value = default;
            if (!ReadSByte(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }
        public short ReadInt16()
        {
            short value = default;
            if (!ReadInt16(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }
        public ushort ReadUInt16()
        {
            ushort value = default;
            if (!ReadUInt16(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }
        public int ReadInt32()
        {
            int value = default;
            if (!ReadInt32(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }
        public uint ReadUInt32()
        {
            uint value = default;
            if (!ReadUInt32(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }
        public long ReadInt64()
        {
            long value = default;
            if (!ReadInt64(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }
        public ulong ReadUInt64()
        {
            ulong value = default;
            if (!ReadUInt64(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }

        public nuint ReadNUint()
        {
            nuint value = default;
            if (!ReadNUint(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }

        public float ReadFloat()
        {
            float value = default;
            if (!ReadFloat(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }
        public double ReadDouble()
        {
            double value = default;
            if (!ReadDouble(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }

        public ReadOnlySpan<byte> ExtractBasicSpan(int length)
        {
            return new ReadOnlySpan<byte>(file.ptr + _position, length);
        }
    }
}
