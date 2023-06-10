using Framework.Serialization;
using Framework.Song.Tracks.Notes.Guitar_Pro;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.SongEntry.TrackScan.Instrument.ProGuitar
{
    public abstract class Midi_ProGuitar_Scanner_Base : Midi_Instrument_Scanner_Base
    {
        internal static readonly int[] DIFFVALUES = {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        };
        internal static readonly uint[] LANEVALUES = {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
        };

        protected readonly bool[] difficulties = new bool[4];
        protected readonly bool[,] notes = new bool[4, 6];

        public override bool IsNote() { return 24 <= note.value && note.value <= 106; }

        public override bool ParseLaneColor_Off()
        {
            if (note.value < 24 || 106 < note.value)
                return false;

            uint noteValue = note.value - 24;
            int diffIndex = DIFFVALUES[noteValue];
            if (!value[diffIndex])
            {
                uint lane = LANEVALUES[noteValue];
                if (lane < 6 && currEvent.channel != 1)
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
