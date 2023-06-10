using Framework.Serialization;
using Framework.Song.Tracks.Instrument.DrumTrack;
using Framework.Song.Tracks.Instrument;
using Framework.Song.Tracks.Notes.Drums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.SongEntry.TrackScan.Instrument.Drums
{
    public class Midi_Drum4Pro_Scanner : Midi_Drum_Scanner_Base
    {
        public override bool IsNote() { return 60 <= note.value && note.value <= 100; }

        public override bool ParseLaneColor()
        {
            uint noteValue = note.value - 60;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficulties[diffIndex])
            {
                uint lane = LANEVALUES[noteValue];
                if (lane < 6)
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
                if (lane < 6)
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
