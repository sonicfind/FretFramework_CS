using Framework.Song.Tracks.Notes.Keys;
using Framework.Types;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Serialization
{
    public unsafe class TxtFileReader : TxtReader_Base
    {
        internal static readonly byte[] BOM = { 0xEF, 0xBB, 0xBF };

        private TxtFileReader(FrameworkFile file, bool disposeFile) : base(file, disposeFile)
        {
            if (new ReadOnlySpan<byte>(file.ptr, 3).SequenceEqual(BOM))
                _position += 3;

            SkipWhiteSpace();
            SetNextPointer();
            if (file.ptr[_position] == '\n')
                GotoNextLine();
        }

        public TxtFileReader(FrameworkFile file) : this(file, false) { }

        public TxtFileReader(byte[] data) : this(new FrameworkFile_Handle(data), true) { }

        public TxtFileReader(string path) : this(new FrameworkFile_Alloc(path), true) { }

        public TxtFileReader(PointerHandler pointer, bool dispose = false) : this(new FrameworkFile_Pointer(pointer, dispose), true) { }

        public override void SkipWhiteSpace()
        {
            int length = file.Length;
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
                if (_position >= file.Length)
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
            while (_next < file.Length && file.ptr[_next] != '\n')
                ++_next;
        }

        public ReadOnlySpan<byte> ExtractTextSpan(bool checkForQuotes = true)
        {
            (int, int) boundaries = new(_position, _next);
            if (boundaries.Item2 == file.Length)
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
            return Encoding.UTF8.GetString(name).ToLower();
        }
    }
}
