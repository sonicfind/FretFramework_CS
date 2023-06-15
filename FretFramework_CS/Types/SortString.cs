using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Types
{
    public struct SortString
    {
        private string _str = string.Empty;
        private string _sortStr = string.Empty;

        public string Str
        {
            get { return _str; }
            set {
                _str = value;
                _sortStr = value.ToLower();
            }
        }

        public int Length { get { return _str.Length; } }
        
        public readonly string SortStr { get { return _sortStr; } }
        public SortString() { }

        public SortString(string str)
        {
            Str = str;
        }

        public static implicit operator SortString(string str) => new(str);
    }
}
