using Framework.Serialization;
using Framework.Song.Tracks.Notes.Guitar;
using Framework.Song.Tracks.Notes.Keys;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Instrument.KeysTrack
{
    public class Midi_Keys_Loader : Midi_Instrument_Loader<Keys>
    {
        private readonly ulong[,] notes = new ulong[4, 5] {
            { ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue },
            { ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue },
            { ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue },
            { ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue },
        };

        private readonly uint[] lanes = new uint[] {
            13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
        };

        static Midi_Keys_Loader() { }

        public Midi_Keys_Loader(byte multiplierNote) : base(
            new(new (byte[], Midi_Phrase)[] {
                new(SOLO, new(SpecialPhraseType.Solo)),
                new(new byte[]{ multiplierNote }, new(SpecialPhraseType.StarPower)),
                new(TRILL, new(SpecialPhraseType.Tremolo)),
                new(TREMOLO, new(SpecialPhraseType.Trill))
            }))
        { }

        public override void ParseLaneColor(MidiNote note, ref InstrumentTrack<Keys> track)
        {
            uint noteValue = note.value - 59;
            uint lane = lanes[noteValue];
            if (lane < 5)
            {
                int diffIndex = DIFFVALUES[noteValue];
                notes[diffIndex, lane] = currEvent.position;
                if (!track[diffIndex].notes.ValidateLastKey(currEvent.position))
                    track[diffIndex].notes.Add_Back_NoReturn(currEvent.position);
            }
        }

        public override void ParseLaneColor_Off(MidiNote note, ref InstrumentTrack<Keys> track)
        {
            uint noteValue = note.value - 59;
            uint lane = lanes[noteValue];
            if (lane < 5)
            {
                int diffIndex = DIFFVALUES[noteValue];
                ulong colorPosition = notes[diffIndex, lane];
                if (colorPosition != ulong.MaxValue)
                {
                    track[diffIndex].notes.Traverse_Backwards_Until(colorPosition)[lane].Duration = currEvent.position - colorPosition;
                    notes[diffIndex, lane] = ulong.MaxValue;
                }
            }
        }
    }
}
