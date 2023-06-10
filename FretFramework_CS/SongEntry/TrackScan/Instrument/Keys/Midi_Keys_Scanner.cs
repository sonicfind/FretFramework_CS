using Framework.Serialization;
using Framework.Song.Tracks.Instrument;
using Framework.Song.Tracks;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.SongEntry.TrackScan.Instrument.Keys
{
    public class Midi_Keys_Scanner : Midi_Instrument_Scanner
    {
        private readonly bool[,] notes = new bool[4, 5];

        private static readonly uint[] LANEVALUES = new uint[] {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
        };

        public override bool ParseLaneColor()
        {
            uint noteValue = note.value - 60;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficulties[diffIndex])
            {
                uint lane = LANEVALUES[noteValue];
                if (lane < 5)
                    notes[diffIndex, lane] = true;
            }
            return false;
        }

        public override bool ParseLaneColor_Off()
        {
            if (note.value < 60 || 100 < note.value)
                return false;

            uint noteValue = note.value - 60;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficulties[diffIndex])
            {
                uint lane = LANEVALUES[noteValue];
                if (lane < 5)
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
