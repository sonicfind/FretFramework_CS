using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Serialization
{
    public unsafe class FrameworkFile
    {
        public byte* ptr;
        public int Length { get; init; }

        protected FrameworkFile() { }

        public FrameworkFile(PointerHandler ptr)
        {
            this.ptr = ptr.GetData();
            Length = ptr.length;
        }

        public byte[] GetMD5() { return MD5.HashData(new ReadOnlySpan<byte>(ptr, Length)); }
        public byte[] CalcSHA1() { return SHA1.HashData(new ReadOnlySpan<byte>(ptr, Length)); }
    }

    public unsafe class FrameworkFile_Handle : FrameworkFile, IDisposable
    {
        private readonly byte[] buffer;
        private readonly GCHandle handle;
        private bool disposed;

        public FrameworkFile_Handle(byte[] data)
        {
            buffer = data;
            handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            ptr = (byte*)handle.AddrOfPinnedObject();
            Length = data.Length;
        }

        ~FrameworkFile_Handle() { handle.Free(); }

        public void Dispose()
        {
            if (disposed) return;
            handle.Free();
            disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public unsafe class FrameworkFile_Alloc : FrameworkFile, IDisposable
    {
        private bool disposed;
        public FrameworkFile_Alloc(string path)
        {
            FileStream fs = File.OpenRead(path);
            int length = (int)fs.Length;
            this.Length = length;
            ptr = (byte*)Marshal.AllocHGlobal(length);
            fs.Read(new Span<byte>(ptr, length));
        }

        ~FrameworkFile_Alloc() { Marshal.FreeHGlobal((IntPtr)ptr); }

        public void Dispose()
        {
            if (disposed) return;
            Marshal.FreeHGlobal((IntPtr)ptr);
            disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
