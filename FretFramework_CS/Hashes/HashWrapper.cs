using Framework.FlatMaps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Hashes
{
    public unsafe struct SHA1Wrapper : IComparable<SHA1Wrapper>, IEquatable<SHA1Wrapper>
    {
        private fixed byte _hash[20];
        public SHA1Wrapper(byte[] hash)
        {
            fixed(byte* src = hash, dst = _hash)
            {
                Copier.MemCpy(dst, src, 20);
            }
        }

        public byte[] ToArray()
        {
            byte[] bytes = new byte[20];
            fixed (byte* src = _hash, dst = bytes)
            {
                Copier.MemCpy(dst, src, 20);
            }
            return bytes;
        }

        public int CompareTo(SHA1Wrapper other)
        {
            for (int i = 0; i < 20; ++i)
                if (_hash[i] < other._hash[i])
                    return -1;
                else if (_hash[i] > other._hash[i])
                    return 1;
            return 0;
        }

        public bool Equals(SHA1Wrapper other)
        {
            for (int i = 0; i < 20; ++i)
                if (other._hash[i] != _hash[i]) return false;
            return true;
        }
    }

    public unsafe struct MD5Wrapper : IComparable<MD5Wrapper>, IEquatable<MD5Wrapper>
    {
        private fixed byte _hash[16];
        public MD5Wrapper(byte[] hash)
        {
            fixed (byte* src = hash, dst = _hash)
            {
                Copier.MemCpy(dst, src, 16);
            }
        }

        public byte[] ToArray()
        {
            byte[] bytes = new byte[16];
            fixed (byte* src = _hash, dst = bytes)
            {
                Copier.MemCpy(dst, src, 20);
            }
            return bytes;
        }

        public int CompareTo(MD5Wrapper other)
        {
            for (int i = 0; i < 16; ++i)
                if (_hash[i] < other._hash[i])
                    return -1;
                else if (_hash[i] > other._hash[i])
                    return 1;
            return 0;
        }

        public bool Equals(MD5Wrapper other)
        {
            for (int i = 0; i < 16; ++i)
                if (other._hash[i] != _hash[i]) return false;
            return true;
        }
    }
}
