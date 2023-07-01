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
    public unsafe abstract class FrameworkFile : IDisposable
    {
        public byte* ptr;
        protected bool disposedValue;

        public int Length { get; init; }

        public byte[] CalcMD5() { return MD5.HashData(new ReadOnlySpan<byte>(ptr, Length)); }
        public byte[] CalcSHA1() { return SHA1.HashData(new ReadOnlySpan<byte>(ptr, Length)); }

        protected abstract void Dispose(bool disposing);

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public unsafe class FrameworkFile_Handle : FrameworkFile
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

        ~FrameworkFile_Handle() { Dispose(false); }

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                handle.Free();
                disposedValue = true;
            }
        }
    }

    public unsafe class FrameworkFile_Alloc : FrameworkFile
    {
        public FrameworkFile_Alloc(string path)
        {
            using FileStream fs = File.OpenRead(path);
            int length = (int)fs.Length;
            Length = length;
            ptr = (byte*)Marshal.AllocHGlobal(length);
            fs.Read(new Span<byte>(ptr, length));
        }

        ~FrameworkFile_Alloc() { Dispose(false); }

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                Marshal.FreeHGlobal((IntPtr)ptr);
                disposedValue = true;
            }
        }
    }

    public unsafe class FrameworkFile_Pointer : FrameworkFile
    {
        private readonly PointerHandler handler;
        public FrameworkFile_Pointer(PointerHandler handler, bool dispose = false)
        {
            this.handler = handler;
            ptr = handler.Data;
            Length = handler.Length;
            disposedValue= !dispose;
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                handler.Dispose();
                disposedValue = true;
            }
        }
    }
}
