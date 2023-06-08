using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Types
{
    public struct Tempo
    {
        public const uint BPM_FACTOR = 60000000;
        public const uint DEFAULT_BPM = 120;
        public const uint MICROS_AT_120BPM = BPM_FACTOR / DEFAULT_BPM;

        private uint _micros = MICROS_AT_120BPM;
        public uint Micros
        {
            get { return _micros; }
            set { _micros = value; }
        }

        public float BPM
        {
            get { return _micros != 0 ? (float)BPM_FACTOR / _micros : 0; }
            set { _micros = value != 0 ? (uint)(BPM_FACTOR / value) : 0; }
        }

        public ulong Anchor { get; set; } = 0;
        public Tempo() {}
    }
}
