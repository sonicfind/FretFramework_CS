using Framework.FlatMaps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Types
{
    public unsafe class PointerHandler : IDisposable
    {
        private byte* data = null;
        public readonly int length;
        private bool disposed;

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

        public ReadOnlySpan<byte> AsSpan() { return new(data, length); }

        public byte* Release()
        {
            byte* ptr = data;
            data = null;
            return ptr;
        }

        ~PointerHandler() { if (!disposed && data != null) Marshal.FreeHGlobal((IntPtr)data); }
        public void Dispose()
        {
            if (!disposed)
                return;
            Marshal.FreeHGlobal((IntPtr)data);
            disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
