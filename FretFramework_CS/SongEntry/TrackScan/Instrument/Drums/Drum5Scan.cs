using Framework.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.SongEntry.TrackScan.Instrument.Drums
{
    public class Midi_Drum5_Scanner : Midi_Drum_Scanner_Base
    {
        public override bool IsNote() { return 60 <= note.value && note.value <= 101; }

        public override bool ParseLaneColor()
        {
            uint noteValue = note.value - 60;
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
            if (note.value < 60 || 101 < note.value)
                return false;

            uint noteValue = note.value - 60;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficulties[diffIndex])
            {
                uint lane = LANEVALUES[noteValue];
                if (lane < 7)
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
