using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Types
{
    public struct NormalizedDuration
    {
        private ulong _duration = 1;
        public ulong Duration
        {
            get { return _duration; }
            set
            {
                if (value == 0)
                    value = 1;
                _duration = value;
            }
        }
        public NormalizedDuration() { }
        public NormalizedDuration(ulong duration) { Duration = duration; }

        static public implicit operator ulong(NormalizedDuration dur) => dur._duration;
        static public implicit operator NormalizedDuration(ulong dur) => new(dur);
    }
}
