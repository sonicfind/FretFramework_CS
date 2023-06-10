using Framework.Serialization;
using Framework.Song.Tracks.Instrument;
using Framework.Song.Tracks.Notes.Drums;
using Framework.Song.Tracks.Notes.Interfaces;
using Framework.Song.Tracks;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace Framework.SongEntry.TrackScan.Instrument.Drums
{
    public abstract class Midi_Drum_Scanner_Base : Midi_Instrument_Scanner
    {
        internal static readonly uint[] LANEVALUES = new uint[] {
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
        };

        protected readonly bool[,] notes = new bool[4, 7];

        public override bool IsFullyScanned() { return value.subTracks == 31; }

        public override bool ProcessSpecialNote()
        {
            if (note.value != 95)
                return false;

            notes[3, 1] = true;
            return true;
        }

        public override bool ProcessSpecialNote_Off()
        {
            if (note.value != 95)
                return false;

            if (notes[3, 1])
                value.subTracks |= 24;
            return true;
        }
    }
}
