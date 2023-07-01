using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Types
{
    public struct SortString : IComparable<SortString>, IEquatable<SortString>
    {
        private string _str = string.Empty;
        private string _sortStr = string.Empty;
        private int _hashCode;

        public string Str
        {
            get { return _str; }
            set {
                _str = value;
                _sortStr = value.ToLower();
                _hashCode = _sortStr.GetHashCode();
            }
        }

        public int Length { get { return _str.Length; } }
        
        public readonly string SortStr { get { return _sortStr; } }
        public SortString() { }

        public SortString(string str)
        {
            Str = str;
        }

        public int CompareTo(SortString other)
        {
            return _sortStr.CompareTo(other._sortStr);
        }

        public override int GetHashCode()
        {
            return _hashCode;  
        }

        public bool Equals(SortString other)
        {
            return _hashCode == other._hashCode;
        }

        public static implicit operator SortString(string str) => new(str);
    }
}
