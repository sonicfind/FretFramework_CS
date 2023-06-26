using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains;
using Framework.Types;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Framework.Serialization.XboxSTFS
{
    public unsafe class DTAFileReader : TxtReader_Base
    {
        private readonly List<int> nodeEnds = new();

        public DTAFileReader(FrameworkFile file, bool disposeFile = false) : base(file, disposeFile) { SkipWhiteSpace(); }

        public DTAFileReader(byte[] data) : this(new FrameworkFile_Handle(data), true) { }

        public DTAFileReader(string path) : this(new FrameworkFile_Alloc(path), true) { }

        public DTAFileReader(PointerHandler pointer, bool dispose = false) : this(new FrameworkFile_Pointer(pointer, dispose), true) { }

        public DTAFileReader Clone()
        {
            return new(file, false)
            {
                _position = _position,
                _next = _next,
                nodeEnds = { nodeEnds[0] }
            };
        }

        public override void SkipWhiteSpace()
        {
            int length = file.Length;
            while (_position < length)
            {
                byte ch = file.ptr[_position];
                if (ch <= 32)
                    ++_position;
                else if (ch == ';')
                {
                    ++_position;
                    while (_position < length)
                    {
                        ++_position;
                        if (file.ptr[_position - 1] == '\n')
                            break;
                    }
                }
                else
                    break;
            }
        }

        public string GetNameOfNode()
        {
            byte ch = file.ptr[_position];
            if (ch == '(')
                return string.Empty;

            bool hasApostrophe = true;
            if (ch != '\'')
            {
                if (file.ptr[_position - 1] != '(')
                    throw new Exception("Invalid name call");
                hasApostrophe = false;
            }
            else
                ch = file.ptr[++_position];

            int start = _position;
            while (ch != '\'')
            {
                if (ch <= 32)
                {
                    if (hasApostrophe)
                        throw new Exception("Invalid name format");
                    break;
                }
                ch = file.ptr[++_position];
            }
            int end = _position++;
            SkipWhiteSpace();
            return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(file.ptr + start, end - start));
        }

        public string ExtractText()
        {
            byte ch = file.ptr[_position];
            bool inSquirley = ch == '{';
            bool inQuotes = !inSquirley && ch == '\"';
            bool inApostrophes = !inQuotes && ch == '\'';

            if (inSquirley || inQuotes || inApostrophes)
                ++_position;

            int start = _position++;
            while (_position < _next)
            {
                ch = file.ptr[_position];
                if (ch == '{')
                    throw new Exception("Text error - no { braces allowed");

                if (ch == '}')
                {
                    if (inSquirley)
                        break;
                    throw new Exception("Text error - no \'}\' allowed");
                }
                else if (ch == '\"')
                {
                    if (inQuotes)
                        break;
                    if (!inSquirley)
                        throw new Exception("Text error - no quotes allowed");
                }
                else if (ch == '\'')
                {
                    if (inApostrophes)
                        break;
                    if (!inSquirley && !inQuotes)
                        throw new Exception("Text error - no apostrophes allowed");
                }
                else if (ch <= 32)
                {
                    if (inApostrophes)
                        throw new Exception("Text error - no whitespace allowed");
                    if (!inSquirley && !inQuotes)
                        break;
                }
                ++_position;
            }

            int end = _position;
            if (_position != _next)
            {
                ++_position;
                SkipWhiteSpace();
            }
            else if (inSquirley || inQuotes || inApostrophes)
                throw new Exception("Improper end to text");

            return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(file.ptr + start, end - start));
        }

        public List<short> ExtractList_Int16()
        {
            List<short> values = new();
            while (*CurrentPtr != ')')
                values.Add(ReadInt16());
            return values;
        }

        public List<ushort> ExtractList_UInt16()
        {
            List<ushort> values = new();
            while (*CurrentPtr != ')')
                values.Add(ReadUInt16());
            return values;
        }

        public List<float> ExtractList_Float()
        {
            List<float> values = new();
            while (*CurrentPtr != ')')
                values.Add(ReadFloat());
            return values;
        }

        public List<string> ExtractList_String()
        {
            List<string> strings = new();
            while (*CurrentPtr != ')')
                strings.Add(ExtractText());
            return strings;
        }

        public bool StartNode()
        {
            byte ch = file.ptr[_position];
            if (ch != '(')
                return false;

            ++_position;
            SkipWhiteSpace();

            int scopeLevel = 1;
            bool inApostropes = false;
            bool inQuotes = false;
            bool inComment = false;
            int pos = _position;
            int length = file.Length;
            while (scopeLevel >= 1 && pos < length)
            {
                ch = file.ptr[pos];
                if (inComment)
                {
                    if (ch == '\n')
                        inComment = false;
                }
                else if (ch == '\"')
                {
                    if (inApostropes)
                        throw new Exception("Ah hell nah wtf");
                    inQuotes = !inQuotes;
                }
                else if (!inQuotes)
                {
                    if (!inApostropes)
                    {
                        if (ch == '(')
                            ++scopeLevel;
                        else if (ch == ')')
                            --scopeLevel;
                        else if (ch == '\'')
                            inApostropes = true;
                        else if (ch == ';')
                            inComment = true;
                    }
                    else if (ch == '\'')
                        inApostropes = false;
                }
                ++pos;
            }
            nodeEnds.Add(pos - 1);
            _next = pos - 1;
            return true;
        }

        public void EndNode()
        {
            int index = nodeEnds.Count - 1;
            _position = nodeEnds[index] + 1;
            nodeEnds.RemoveAt(index);
            if (index > 0)
                _next = nodeEnds[--index];
            SkipWhiteSpace();
        }
    };
}
