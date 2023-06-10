using Framework.Serialization;
using Framework.Song.Tracks.Instrument;
using Framework.Song.Tracks.Notes.Guitar;
using Framework.Song.Tracks;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Framework.SongEntry.TrackScan.Instrument.Guitar
{
    public unsafe class Midi_FiveFret_Scanner : Midi_Instrument_Scanner
    {
        internal static readonly byte[][] ENHANCED_STRINGS = new byte[][] { Encoding.ASCII.GetBytes("[ENHANCED_OPENS]"), Encoding.ASCII.GetBytes("ENHANCED_OPENS") };

        private readonly bool[,] notes = new bool[4, 6];
        private readonly uint[] lanes = new uint[] {
            13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
        };

        public override bool IsNote() { return 59 <= note.value && note.value <= 107; }

        public override bool ParseLaneColor()
        {
            uint noteValue = note.value - 59;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficulties[diffIndex])
            {
                uint lane = lanes[noteValue];
                if (lane < 6)
                    notes[diffIndex, lane] = true;
            }
            return false;
        }

        public override bool ParseLaneColor_Off()
        {
            if (note.value < 59 || 107 < note.value)
                return false;

            uint noteValue = note.value - 59;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficulties[diffIndex])
            {
                uint lane = lanes[noteValue];
                if (lane < 6 && notes[diffIndex, lane])
                {
                    value.Set(diffIndex);
                    difficulties[diffIndex] = true;
                    return IsFullyScanned();
                }
            }
            return false;
        }

        public override void ParseSysEx(ReadOnlySpan<byte> str)
        {
            if (str.StartsWith(SYSEXTAG) && str[5] == 1)
            {
                uint status = str[6] == 0 ? (uint)1 : 0;
                if (str[4] == (char)0xFF)
                {
                    for (int diff = 0; diff < 4; ++diff)
                        lanes[12 * diff + 1] = status;
                }
                else
                    lanes[12 * str[4] + 1] = status;
            }
        }

        public override void ParseText(ReadOnlySpan<byte> str)
        {
            if (str.SequenceEqual(ENHANCED_STRINGS[0]) || str.SequenceEqual(ENHANCED_STRINGS[1]))
                for (int diff = 0; diff < 4; ++diff)
                    lanes[12 * diff] = 0;
        }
    }
}
