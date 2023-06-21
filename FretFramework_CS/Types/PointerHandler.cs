using Framework.FlatMaps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Types
{
    public unsafe class PointerHandler : IDisposable
    {
        private byte* data = null;
        private bool disposedValue;
        public readonly int length;

        public PointerHandler(int length)
        {
            this.length = length;
            data = (byte*)Marshal.AllocHGlobal(length);
        }
        public PointerHandler(PointerHandler handler)
        {
            this.length = handler.length;
            data = (byte*)Marshal.AllocHGlobal(length);
            Copier.MemCpy(data, handler.data, (nuint)length);
        }

        public byte* GetData() { return data; }

        public ReadOnlySpan<byte> AsReadOnlySpan() { return new(data, length); }
        public Span<byte> AsSpan() { return new(data, length); }

        public byte[] CalcMD5() { return MD5.HashData(AsReadOnlySpan()); }
        public byte[] CalcSHA1() { return SHA1.HashData(AsReadOnlySpan()); }

        public byte* Release()
        {
            byte* ptr = data;
            data = null;
            return ptr;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                Marshal.FreeHGlobal((IntPtr)data);
                disposedValue = true;
            }
        }

        ~PointerHandler()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
