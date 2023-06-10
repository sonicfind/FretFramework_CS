using Framework.Serialization;
using Framework.Song.Tracks.Notes.Guitar_Pro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.SongEntry.TrackScan.Instrument.ProGuitar
{
    public class Midi_ProGuitar17_Scanner : Midi_ProGuitar_Scanner_Base
    {
        public override bool ParseLaneColor()
        {
            uint noteValue = note.value - 24;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficulties[diffIndex])
            {
                uint lane = LANEVALUES[noteValue];
                if (lane < 6 && currEvent.channel != 1 && 100 <= note.velocity && note.velocity <= 117)
                    notes[diffIndex, lane] = true;
            }
            return false;
        }
    }
}
