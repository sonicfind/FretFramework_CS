using Framework.FlatMaps;
using Framework.Serialization;
using Framework.Song.Tracks.Notes;
using Framework.Song.Tracks.Notes.Drums;
using Framework.Song.Tracks.Notes.Guitar_Pro;
using Framework.Song.Tracks.Notes.Guitar;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Instrument.ProGuitarTrack
{
    public enum ChordPhrase
    {
	    Force_Numbering,
	    Slash,
	    Hide,
	    Accidental_Switch
    };

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class ProGuitarTrack<FretType> : InstrumentTrack_Base<ProGuitarDifficulty<FretType>>
        where FretType : struct, IFretted
    {
        public readonly TimedFlatMap<PitchName> roots = new();
        public readonly TimedFlatMap<FretType> handPositions = new();
        public readonly TimedFlatMap<List<ChordPhrase>> chordPhrases = new();

        public override bool IsOccupied()
        {
            return !roots.IsEmpty() || !handPositions.IsEmpty() || !chordPhrases.IsEmpty() || base.IsOccupied();
        }
        public override void Clear()
        {
            base.Clear();
            roots.Clear();
            handPositions.Clear();
            chordPhrases.Clear();
        }
        private new string GetDebuggerDisplay()
        {
            string str = string.Empty;
            if (difficulties[3].IsOccupied())
                str += "Expert: " + difficulties[3].GetDebuggerDisplay_Short();
            if (difficulties[2].IsOccupied())
                str += "Hard: " + difficulties[2].GetDebuggerDisplay_Short();
            if (difficulties[1].IsOccupied())
                str += "Medium: " + difficulties[1].GetDebuggerDisplay_Short();
            if (difficulties[0].IsOccupied())
                str += "Easy: " + difficulties[0].GetDebuggerDisplay_Short();
            return str;
        }
    }

    public class Midi_ProGuitar_Loader<FretType> : Midi_Loader_Base<ProGuitarTrack<FretType>>
        where FretType : struct, IFretted
    {
        internal static readonly uint[] DIFFVALUES = {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		    1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,	1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
		    2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,	2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
		    3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,	3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        };
        internal static readonly uint[] LANEVALUES = {
		    0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
		    0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
		    0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
		    0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
	    };

        internal static readonly byte[] SOLO = { 115 };
        internal static readonly byte[] TREMOLO = { 126 };
        internal static readonly byte[] TRILL = { 127 };

        protected struct ProGuitar_MidiDiff
        {
            public bool Hopo { get; set; }
            public readonly ulong[] notes = new ulong[6] { ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue };
            public ulong Arpeggio { get; set; }
            public ProSlide Slide { get; set; }
            public EmphasisType Emphasis { get; set; }
            public ProGuitar_MidiDiff() { }
        }

        protected readonly ProGuitar_MidiDiff[] difficulties = { new(), new(), new(), new(), };

        public Midi_ProGuitar_Loader(byte multiplierNote) : base(
            new(new (byte[], Midi_Phrase)[] {
                new(SOLO, new(SpecialPhraseType.Solo)),
                new(new byte[]{ multiplierNote }, new(SpecialPhraseType.StarPower)),
                new(TRILL, new(SpecialPhraseType.Tremolo)),
                new(TREMOLO, new(SpecialPhraseType.Trill))
            }))
        { }

        static Midi_ProGuitar_Loader() { }

        public override bool IsNote(uint value) { return 24 <= value && value <= 106; }

        public override void ParseLaneColor(MidiNote note, ref ProGuitarTrack<FretType> track)
        {
            uint noteValue = note.value - 24;
            uint diffIndex = DIFFVALUES[noteValue];
            uint lane = LANEVALUES[noteValue];
            ref var midiDiff = ref difficulties[diffIndex];
            ref var diffTrack = ref track[diffIndex];
            if (lane < 6)
            {
                if (currEvent.channel == 1)
                    diffTrack.arpeggios.Get_Or_Add_Back(currEvent.position)[lane].Value = (int)note.velocity - 100;
                else
                {
                    if (!track[diffIndex].notes.ValidateLastKey(currEvent.position))
                    {
                        ref var guitar = ref track[diffIndex].notes.Add_Back(currEvent.position);
                        guitar.HOPO = midiDiff.Hopo;
                        guitar.Slide = midiDiff.Slide;
                        guitar.Emphasis = midiDiff.Emphasis;
                    }

                    ref var proString = ref track[diffIndex].notes.Last()[lane];
                    switch (currEvent.channel)
                    {
                        case 2: proString.Mode = StringMode.Bend; break;
                        case 3: proString.Mode = StringMode.Muted; break;
                        case 4: proString.Mode = StringMode.Tapped; break;
                        case 5: proString.Mode = StringMode.Harmonics; break;
                        case 6: proString.Mode = StringMode.Pinch_Harmonics; break;
                    }
                       
                    proString.fret.Value = (int)note.velocity - 100;
                    midiDiff.notes[lane] = currEvent.position;
                }
            }
            else if (lane == 6)
            {
                midiDiff.Hopo = true;
                if (true && diffTrack.notes.ValidateLastKey(currEvent.position))
                    diffTrack.notes.Last().HOPO = true;
            }
            else if (lane == 7)
            {
                midiDiff.Slide = currEvent.channel == 11 ? ProSlide.Reversed : ProSlide.Normal;
                if (diffTrack.notes.ValidateLastKey(currEvent.position))
                    diffTrack.notes.Last().Slide = midiDiff.Slide;
            }
            else if (lane == 8)
            {
                diffTrack.arpeggios.Get_Or_Add_Back(currEvent.position);
                midiDiff.Arpeggio = currEvent.position;
            }
            else if (lane == 9)
            {
                switch (currEvent.channel)
                {
                    case 13: midiDiff.Emphasis = EmphasisType.High; break;
                    case 14: midiDiff.Emphasis = EmphasisType.Middle; break;
                    case 15: midiDiff.Emphasis = EmphasisType.Low; break;
                    default: return;
                }

                if (diffTrack.notes.ValidateLastKey(currEvent.position))
                    diffTrack.notes.Last().Emphasis = midiDiff.Emphasis;
            }
        }

        public override void ParseLaneColor_Off(MidiNote note, ref ProGuitarTrack<FretType> track)
        {
            uint noteValue = note.value - 24;
            uint diffIndex = DIFFVALUES[noteValue];
            uint lane = LANEVALUES[noteValue];
            if (lane < 6)
            {
                if (currEvent.channel != 1)
                {
                    ulong colorPosition = difficulties[diffIndex].notes[lane];
                    if (colorPosition != ulong.MaxValue)
                    {
                        track[diffIndex].notes.Traverse_Backwards_Until(colorPosition)[lane]!.Duration = currEvent.position - colorPosition;
                        difficulties[diffIndex].notes[lane] = ulong.MaxValue;
                    }
                }
            }
            else if (lane == 6)
                difficulties[diffIndex].Hopo = false;
            else if (lane == 7)
                difficulties[diffIndex].Slide = ProSlide.None;
            else if (lane == 8)
            {
                ulong arpeggioPosition = difficulties[diffIndex].Arpeggio;
                if (arpeggioPosition != ulong.MaxValue)
                {
                    track[diffIndex].arpeggios.Last().duration = currEvent.position - arpeggioPosition;
                    difficulties[diffIndex].Arpeggio = ulong.MaxValue;
                }
            }
            else if (lane == 9)
                difficulties[diffIndex].Emphasis = EmphasisType.None;
        }

        internal static readonly PitchName[] s_ROOTS = { PitchName.E, PitchName.F, PitchName.F_Sharp_Gb, PitchName.G, PitchName.G_Sharp_Ab, PitchName.A, PitchName.A_Sharp_Bb, PitchName.B, PitchName.C, PitchName.C_Sharp_Db, PitchName.D, PitchName.D_Sharp_Eb};
        public override void ToggleExtraValues(MidiNote note, ref ProGuitarTrack<FretType> track)
        {
            if (4 <= note.value && note.value <= 15)
            {
                track.roots.Add_Back(currEvent.position, s_ROOTS[note.value - 4]);
                return;
            }

            switch (note.value)
            {
                case 16: track.chordPhrases.Get_Or_Add_Back(currEvent.position).Add(ChordPhrase.Slash); break;
                case 17: track.chordPhrases.Get_Or_Add_Back(currEvent.position).Add(ChordPhrase.Hide); break;
                case 18: track.chordPhrases.Get_Or_Add_Back(currEvent.position).Add(ChordPhrase.Accidental_Switch); break;
                case 107: track.chordPhrases.Get_Or_Add_Back(currEvent.position).Add(ChordPhrase.Force_Numbering); break;
                case 108: track.handPositions.Add_Back(currEvent.position).Value = (int)note.velocity - 100; break;
            }
        }
    }
}
