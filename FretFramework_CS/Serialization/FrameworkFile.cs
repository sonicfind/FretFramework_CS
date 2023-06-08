﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Serialization
{
    public unsafe class FrameworkFile
    {
        public readonly byte[] buffer;
        public readonly GCHandle handle;
        public readonly byte* ptr;

        public FrameworkFile(byte[] data)
        {
            buffer = data;
            handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            ptr = (byte*)handle.AddrOfPinnedObject();
        }

        public FrameworkFile(string path) : this(File.ReadAllBytes(path)) { }
        ~FrameworkFile() { handle.Free(); }
        public int Length { get { return buffer.Length; } }
    }
}
