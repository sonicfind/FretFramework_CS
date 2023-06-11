using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Types
{
    public struct SortString
    {
        private string _str;
        private string sortStr;

        public string Str
        {
            get { return _str; }
            set {
                _str = value;
                sortStr = value;
            }
        }
        
        public readonly string SortStr { get { return sortStr; } }
        public SortString()
        {
            _str = string.Empty;
            sortStr = string.Empty;
        }

        public SortString(string str)
        {
            _str = str;
            sortStr = str.ToLower();
        }
    }
}
