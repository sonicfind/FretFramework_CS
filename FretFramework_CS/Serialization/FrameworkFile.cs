using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Serialization
{
    public unsafe class FrameworkFile : IDisposable
    {
        private readonly byte[] buffer;
        private readonly GCHandle handle;
        public readonly byte* ptr;
        private bool disposed;

        public FrameworkFile(byte[] data)
        {
            buffer = data;
            handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            ptr = (byte*)handle.AddrOfPinnedObject();
        }

        public FrameworkFile(string path) : this(File.ReadAllBytes(path)) { }
        ~FrameworkFile() { handle.Free(); }
        public int Length { get { return buffer.Length; } }

        public byte[] HASH_SHA1 { get { return SHA1.HashData(buffer); } }
        public byte[] HASH_MD5 { get { return MD5.HashData(buffer); } }

        public void Dispose()
        {
            if (disposed) return;
            handle.Free();
            disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
