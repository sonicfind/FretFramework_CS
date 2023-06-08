using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Types
{
    public struct TimeSig
    {
        public byte Numerator { get; set; }
        public byte Denominator { get; set; } = 255;
        public byte Metronome { get; set; }
        public byte Num32nds { get; set; }
        public TimeSig() { }
        public TimeSig(byte numerator, byte denominator, byte metronome, byte num32nds)
        {
            Numerator = numerator;
            Denominator = denominator;
            Metronome = metronome;
            Num32nds = num32nds;
        }
    };
}
