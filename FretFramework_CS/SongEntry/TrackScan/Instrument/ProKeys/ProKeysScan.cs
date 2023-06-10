using Framework.Serialization;
using Framework.Song.Tracks.Instrument.ProKeysTrack;
using Framework.Song.Tracks.Instrument;
using Framework.Song.Tracks;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.SongEntry.TrackScan.Instrument.ProKeys
{
    public class Midi_ProKeys_Scanner : Midi_Instrument_Scanner_Base
    {
        private readonly bool[] lanes = new bool[25];
        public readonly int difficulty;
        public Midi_ProKeys_Scanner(int difficulty) { this.difficulty = difficulty; }

        public override bool IsNote() { return 48 <= note.value && note.value <= 72; }

        public override bool ParseLaneColor()
        {
            lanes[note.value - 48] = true;
            return false;
        }

        public override bool ParseLaneColor_Off()
        {
            if (note.value < 48 || 72 < note.value || !lanes[note.value - 48])
                return false;

            value.Set(difficulty);
            return true;
        }
    }
}
