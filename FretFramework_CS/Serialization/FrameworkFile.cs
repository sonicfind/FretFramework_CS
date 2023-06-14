using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Serialization
{
    public unsafe abstract class FrameworkFile : IDisposable
    {
        public byte* ptr;
        public int Length { get; init; }
        protected bool disposed;

        public abstract void Dispose();
    }

    public unsafe class FrameworkFile_Handle : FrameworkFile, IDisposable
    {
        private readonly byte[] buffer;
        private readonly GCHandle handle;

        public FrameworkFile_Handle(byte[] data)
        {
            buffer = data;
            handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            ptr = (byte*)handle.AddrOfPinnedObject();
            Length = data.Length;
        }

        ~FrameworkFile_Handle() { handle.Free(); }

        public override void Dispose()
        {
            if (disposed) return;
            handle.Free();
            disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public unsafe class FrameworkFile_Alloc : FrameworkFile, IDisposable
    {
        public FrameworkFile_Alloc(string path)
        {
            FileStream fs = File.OpenRead(path);
            int length = (int)fs.Length;
            this.Length = length;
            ptr = (byte*)Marshal.AllocHGlobal(length);
            fs.Read(new Span<byte>(ptr, length));
        }
        ~FrameworkFile_Alloc() { Marshal.FreeHGlobal((IntPtr)ptr); }

        public override void Dispose()
        {
            if (disposed) return;
            Marshal.FreeHGlobal((IntPtr)ptr);
            disposed = true;
            GC.SuppressFinalize(this);
        }

        public byte[] GetMD5() { return MD5.HashData(new ReadOnlySpan<byte>(ptr, Length)); }
        public byte[] CalcSHA1() { return SHA1.HashData(new ReadOnlySpan<byte>(ptr, Length)); }
    }
}
