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
    public unsafe class TxtFileReader : TxtReader_Base, IDisposable
    {
        internal static readonly byte[] BOM = { 0xEF, 0xBB, 0xBF };
        internal static readonly UTF8Encoding UTF8 = new(true, true);
        static TxtFileReader() { }

        private readonly bool disposeFile;

        public TxtFileReader(FrameworkFile file, bool disposeFile = false) : base(file)
        {
            this.disposeFile = disposeFile;
            if (new ReadOnlySpan<byte>(file.ptr, 3).SequenceEqual(BOM))
                _position += 3;

            SkipWhiteSpace();
            SetNextPointer();
            if (file.ptr[_position] == '\n')
                GotoNextLine();
        }

        public TxtFileReader(byte[] data) : this(new FrameworkFile_Handle(data), true) { }

        public TxtFileReader(string path) : this(new FrameworkFile_Alloc(path), true) { }

        public TxtFileReader(PointerHandler pointer, bool dispose = false) : this(new FrameworkFile_Pointer(pointer, dispose), true) { }

        public void Dispose()
        {
            if (disposeFile)
                file.Dispose();
            GC.SuppressFinalize(this);
        }

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

        public string ExtractEncodedString(bool checkForQuotes = true)
        {
            var span = ExtractTextSpan(checkForQuotes);
            try
            {
                return UTF8.GetString(span);
            }
            catch
            {
                char[] str = new char[span.Length];
                for (int i = 0; i < span.Length; ++i)
                    str[i] = (char)span[i];
                return new(str);
            }
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
