using Framework.FlatMaps;
using Framework.Types;
using Iced.Intel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Hashes
{
    public abstract class HashWrapper : IComparable<HashWrapper>
    {
        protected readonly int[] hash;
        private readonly int hashCode;
        private HashWrapper(int length) { hash = new int[length]; }

        protected HashWrapper(byte[] hash, int length) : this(length)
        {
            int byteSize = sizeof(int) * length;
            if (hash.Length != byteSize)
                throw new Exception("Hash incompatible");

            unsafe
            {
                fixed (byte* src = hash)
                fixed (int* dst = this.hash)
                    Copier.MemCpy(dst, src, (nuint)byteSize);
            }

            for (int i = 0; i < hash.Length; i++)
                hashCode ^= hash[i];
        }

        protected HashWrapper(BinaryReader reader, int length) : this(length)
        {
            for (int i = 0; i < hash.Length; i++)
                hashCode ^= hash[i] = reader.ReadInt32();
        }

        public void Write(BinaryWriter writer)
        {
            for (int i = 0; i < hash.Length; i++)
                writer.Write(hash[i]);
        }

        public byte[] ToArray()
        {
            int byteCount = sizeof(int) * hash.Length;
            byte[] bytes = new byte[byteCount];
            unsafe
            {
                fixed (int* src = hash)
                fixed (byte* dst = bytes)
                    Copier.MemCpy(dst, src, (nuint)byteCount);
            }
            return bytes;
        }

        public override bool Equals(object? o)
        {
            if (o is not HashWrapper other)
                return false;

            for (int i = 0; i < hash.Length; ++i)
                if (other.hash[i] != hash[i])
                    return false;
            return true;
        }

        public int CompareTo(HashWrapper? other)
        {
            for (int i = 0; i < hash.Length; ++i)
                if (hash[i] < other!.hash[i])
                    return -1;
                else if (hash[i] > other.hash[i])
                    return 1;
            return 0;
        }

        public static bool operator==(HashWrapper lhs, HashWrapper rhs)
        {
            return lhs.Equals(rhs);
        }

        public static bool operator!=(HashWrapper lhs, HashWrapper rhs)
        {
            return !lhs.Equals(rhs);
        }

        public override int GetHashCode()
        {
            return hashCode;
        }
    }

    public class SHA1Wrapper : HashWrapper
    {
        public SHA1Wrapper(byte[] hash) : base(hash, 5) { }
        public SHA1Wrapper(BinaryReader reader) : base(reader, 5) { }
    }

    public class MD5Wrapper : HashWrapper
    {
        public MD5Wrapper(byte[] hash) : base(hash, 4) { }

        public MD5Wrapper(BinaryReader reader) : base(reader, 4) { }
    }
}
