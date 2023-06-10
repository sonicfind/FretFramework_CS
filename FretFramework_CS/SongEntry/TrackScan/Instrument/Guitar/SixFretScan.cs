using Framework.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.SongEntry.TrackScan.Instrument.Guitar
{
    public class Midi_SixFret_Scanner : Midi_Instrument_Scanner
    {
        private readonly bool[,] notes = new bool[4, 7];
        private static readonly uint[] LANEVALUES = new uint[] {
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
        };

        public override bool IsNote() { return 58 <= note.value && note.value <= 103; }

        public override bool ParseLaneColor()
        {
            uint noteValue = note.value - 58;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficulties[diffIndex])
            {
                uint lane = LANEVALUES[noteValue];
                if (lane < 7)
                    notes[diffIndex, lane] = true;
            }
            return false;
        }

        public override bool ParseLaneColor_Off()
        {
            if (note.value < 58 || 103 < note.value)
                return false;

            uint noteValue = note.value - 59;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficulties[diffIndex])
            {
                uint lane = LANEVALUES[noteValue];
                if (lane < 7 && notes[diffIndex, lane])
                {
                    value.Set(diffIndex);
                    difficulties[diffIndex] = true;
                    return IsFullyScanned();
                }
            }
            return false;
        }
    }
}
