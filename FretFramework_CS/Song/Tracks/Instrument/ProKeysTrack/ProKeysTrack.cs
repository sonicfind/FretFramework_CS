using Framework.Serialization;
using Framework.Song.Tracks.Instrument.ProGuitarTrack;
using Framework.Song.Tracks.Notes.Guitar_Pro;
using Framework.Song.Tracks.Notes.Keys_Pro;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Instrument.ProKeysTrack
{
    public class Midi_ProKeys_Loader : Midi_Loader_Base<ProKeysDifficulty>
    {
        internal static readonly byte[] SOLO = { 115 };
        internal static readonly byte[] BRE = { 120 };
        internal static readonly byte[] TREMOLO = { 126 };
        internal static readonly byte[] TRILL = { 127 };
        
        private readonly ulong[] lanes = new ulong[25]
	    {
		    ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue,
		    ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue,
		    ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue,
		    ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue,
		    ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue,
	    };

        public Midi_ProKeys_Loader(byte multiplierNote) : base(
            new(new (byte[], Midi_Phrase)[] {
                new(SOLO, new(SpecialPhraseType.Solo)),
                new(new byte[]{ multiplierNote }, new(SpecialPhraseType.StarPower)),
                new(BRE, new(SpecialPhraseType.BRE)),
                new(TRILL, new(SpecialPhraseType.Tremolo)),
                new(TREMOLO, new(SpecialPhraseType.Trill))
            }))
        { }

        protected override bool IsNote() { return 48 <= note.value && note.value <= 72; }

        protected override void ParseLaneColor(ref ProKeysDifficulty track)
        {
            if (!track.notes.ValidateLastKey(currEvent.position))
                track.notes.Add_Back_NoReturn(currEvent.position);
            lanes[note.value - 48] = currEvent.position;
        }

        protected override void ParseLaneColor_Off(ref ProKeysDifficulty track)
        {
            ulong colorPosition = lanes[note.value - 48];
            if (colorPosition != ulong.MaxValue)
            {
                track.notes.Traverse_Backwards_Until(colorPosition)!.Add(note.value, currEvent.position - colorPosition);
                lanes[note.value - 48] = ulong.MaxValue;
            }
        }

        protected override void ToggleExtraValues(ref ProKeysDifficulty track)
        {
            switch(note.value)
            {
                case 0: track.ranges.Get_Or_Add_Back(currEvent.position) = ProKey_Ranges.C1_E2; break;
                case 2: track.ranges.Get_Or_Add_Back(currEvent.position) = ProKey_Ranges.D1_F2; break;
                case 4: track.ranges.Get_Or_Add_Back(currEvent.position) = ProKey_Ranges.E1_G2; break;
                case 5: track.ranges.Get_Or_Add_Back(currEvent.position) = ProKey_Ranges.F1_A2; break;
                case 7: track.ranges.Get_Or_Add_Back(currEvent.position) = ProKey_Ranges.G1_B2; break;
                case 9: track.ranges.Get_Or_Add_Back(currEvent.position) = ProKey_Ranges.A1_C3; break;
            };
        }
    }
}
