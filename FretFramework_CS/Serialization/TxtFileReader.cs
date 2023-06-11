using Framework.Song.Tracks.Notes.Keys;
using Framework.Types;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Serialization
{
    public enum ModifierNodeType
    {
        STRING,
        STRING_NOCASE,
        STRING_CHART,
        STRING_CHART_NOCASE,
        UINT64,
        INT64,
        UINT32,
        INT32,
        UINT16,
        INT16,
        BOOL,
        FLOAT,
        FLOATARRAY
    }

    public class ModifierNode
    {
        public readonly ReadOnlyMemory<byte> name;
        public readonly ModifierNodeType type;

        public ModifierNode(ReadOnlyMemory<byte> name, ModifierNodeType type)
        {
            this.name = name;
            this.type = type;
        }
	};

    public unsafe class TxtFileReader
    {
        private readonly FrameworkFile file;
        private readonly int length;
        public byte* Ptr
        {
            get { return file.ptr; }
        }

        public int Length { get { return file.Length; } }

        private int _position;
        public int Position
        {
            get { return _position; }
            set
            {
                if (value < _position)
                    throw new ArgumentOutOfRangeException("Position");
                _position = value;
            }
        }
        private int _next;
        public int Next { get { return _next; } }

        public byte PeekByte()
        {
            return file.ptr[_position];
        }

        public byte* CurrentPtr { get { return file.ptr + _position; } }

        internal static readonly byte[] BOM = { 0xEF, 0xBB, 0xBF };
        public TxtFileReader(FrameworkFile file)
        {
            this.file = file;
            length = file.Length;
            if (new ReadOnlySpan<byte>(file.ptr, 3).SequenceEqual(BOM))
                _position += 3;

            SkipWhiteSpace();
            SetNextPointer();
            if (file.ptr[_position] == '\n')
                GotoNextLine();
        }
        public TxtFileReader(byte[] data) : this(new FrameworkFile(data)) { }
        public TxtFileReader(string path) : this(File.ReadAllBytes(path)) { }

        public bool IsEndOfFile()
        {
            return _position == file.Length;
        }

        public void SkipWhiteSpace()
        {
            while (_position < length)
            {
                byte ch = file.ptr[_position];
                if (ch <= 32)
                {
                    if (ch == '\n')
                        break;
                }
                else if (ch != '=')
                    break;
                ++_position;
            }
        }

        public void GotoNextLine()
        {
            do
            {
                _position = _next;
                if (_position == length)
                    break;

                _position++;
                SkipWhiteSpace();

                if (file.ptr[_position] == '{')
                {
                    _position++;
                    SkipWhiteSpace();
                }

                SetNextPointer();
            } while (file.ptr[_position] == '\n' || (file.ptr[_position] == '/' && file.ptr[_position + 1] == '/'));
        }

        public void SetNextPointer()
        {
            _next = _position;
            while (_next < length && file.ptr[_next] != '\n')
                ++_next;
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

                if ('0' < b || b > '9')
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
                if ('0' < b || b > '9')
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

                if ('0' < b || b > '9')
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
                if ('0' < b || b > '9')
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

                if ('0' < b || b > '9')
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
                if ('0' < b || b > '9')
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

        public ReadOnlySpan<byte> ExtractTextSpan(bool checkForQuotes = true)
        {
            (int, int) boundaries = new(_position, _next);
            if (boundaries.Item2 == length)
                --boundaries.Item2;

            if (checkForQuotes && file.ptr[_position] == '\"')
            {
                int end = boundaries.Item2 - 1;
                while (_position + 1 < end && file.ptr[end] <= 32)
                    --end;

                if (_position < end && file.ptr[end] == '\"' && file.ptr[end - 1] != '\\')
                {
                    ++boundaries.Item1;
                    boundaries.Item2 = end;
                }
            }

            if (boundaries.Item2 < boundaries.Item1)
                return new();

            while (boundaries.Item2 > boundaries.Item1 && file.ptr[boundaries.Item2 - 1] <= 32)
                --boundaries.Item2;

            _position = _next;
            return new(file.ptr + boundaries.Item1, boundaries.Item2 - boundaries.Item1);
        }

        public string ExtractUTF8String(bool checkForQuotes = true)
        {
            return Encoding.UTF8.GetString(ExtractTextSpan(checkForQuotes));
        }

        public string ExtractModifierName()
        {
            int curr = _position;
            while (true)
            {
                byte b = file.ptr[curr];
                if (b <= 32 || b == '=')
                    break;
                ++curr;
            }

            ReadOnlySpan<byte> name = new(file.ptr + _position, curr - _position);
            _position = curr;
            SkipWhiteSpace();
            return Encoding.UTF8.GetString(name);
        }

        public ModifierNode? FindNode(ReadOnlySpan<byte> name, (byte[], ModifierNode)[] list)
        {
            int lo = 0;
            int hi = list.Length - 1;
            unsafe
            {
                fixed((byte[], ModifierNode)* nodes = list)
                {
                    while (lo <= hi)
                    {
                        int curr = lo + ((hi - lo) >> 1);
                        int order = new ReadOnlySpan<byte>(nodes[curr].Item1).SequenceCompareTo(name);
                        if (order == 0)
                            return nodes[curr].Item2;

                        if (order < 0)
                            lo = curr + 1;
                        else
                            hi = curr - 1;
                    }
                }
                
            }

            return null;
        }

        //Modifiers::Modifier CreateModifier(ModifierNode node)
        //{
	       // try
	       // {
		      //  switch (node.type)
		      //  {
		      //  case ModifierNodeType.STRING:              return { node.name, UnicodeString(extractText(false)) };
		      //  case ModifierNodeType.STRING_CHART:        return { node.name, UnicodeString(extractText()) };
		      //  case ModifierNodeType.STRING_NOCASE:       return { node.name, UnicodeString::strToU32(extractText(false)) };
		      //  case ModifierNodeType.STRING_CHART_NOCASE: return { node.name, UnicodeString::strToU32(extractText()) };
		      //  case ModifierNodeType.UINT64:              return { node.name, extract<uint64_t>() };
		      //  case ModifierNodeType.INT64:               return { node.name, extract<int64_t>() };
		      //  case ModifierNodeType.UINT32:              return { node.name, extract<uint32_t>() };
		      //  case ModifierNodeType.INT32:               return { node.name, extract<int32_t>() };
		      //  case ModifierNodeType.UINT16:              return { node.name, extract<uint16_t>() };
		      //  case ModifierNodeType.INT16:               return { node.name, extract<int16_t>() };
		      //  case ModifierNodeType.BOOL:                return { node.name, extract<bool>() };
		      //  case ModifierNodeType.FLOAT:               return { node.name, extract<float>() };
		      //  case ModifierNodeType.FLOATARRAY:
		      //  {
			     //   float flt1 = ReadFloat();
			     //   float flt2 = ReadFloat();
			     //   return { node.name, flt1, flt2 };
		      //  }
		      //  }
	       // }
	       // catch (...)
	       // {
		      //  switch (node.type)
		      //  {
		      //  case ModifierNodeType.UINT64:     return { node.name, uint64_t(0) };
		      //  case ModifierNodeType.INT64:      return { node.name, int64_t(0) };
		      //  case ModifierNodeType.UINT32:     return { node.name, uint32_t(0) };
		      //  case ModifierNodeType.INT32:      return { node.name, int32_t(0) };
		      //  case ModifierNodeType.UINT16:     return { node.name, uint16_t(0) };
		      //  case ModifierNodeType.BOOL:       return { node.name, false };
		      //  case ModifierNodeType.FLOAT:      return { node.name, .0f };
		      //  case ModifierNodeType.FLOATARRAY: return { node.name, 0, 0 };
		      //  }
	       // }
	       // throw std::runtime_error("How in the fu-");
        //}
    }
}
