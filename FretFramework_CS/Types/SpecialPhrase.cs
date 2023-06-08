using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Types
{
    public enum SpecialPhraseType
    {
        FaceOff_Player1 = 0,
        FaceOff_Player2 = 1,
        StarPower = 2,
        Solo = 3,
        LyricLine = 4,
        RangeShift = 5,
        HarmonyLine = 6,
        Arpeggio = 7,
        StarPower_Diff = 8,
        BRE = 64,
        Tremolo = 65,
        Trill = 66,
        LyricShift = 67,
        Chord_Numbering = 68,
    }
    public struct SpecialPhrase
    {
        private ulong _duration;
        public ulong Duration
        { 
            get { return _duration; } 
            set{
                if (value == 0)
                    value = 1;
                _duration = value;
            }
        }
        public SpecialPhraseType Type { get; set; }
        public uint Velocity { get; set; }
        public SpecialPhrase(SpecialPhraseType type, ulong duration, uint velocity = 100)
        {
            Type = type;
            Velocity = velocity;
            Duration = duration;
        }
    }
}
