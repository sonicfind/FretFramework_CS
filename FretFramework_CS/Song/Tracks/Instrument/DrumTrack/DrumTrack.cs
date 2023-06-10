using Framework.Serialization;
using Framework.Song.Tracks.Notes.Drums;
using Framework.Song.Tracks.Notes.Interfaces;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Instrument.DrumTrack
{
    public abstract class Midi_Drum_Loader_Base<T> : Midi_Instrument_Loader<T>
        where T : struct, IDrumNote, INote
    {
        internal static readonly uint[] LANEVALUES = new uint[] {
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
        };
        internal static readonly byte[] DYNAMICS_STRING = Encoding.ASCII.GetBytes("[ENABLE_CHART_DYNAMICS]");

        protected struct Drum4_MidiDiff
        {
            public bool Flam { get; set; }
            public readonly ulong[] notes = new ulong[7] { ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue };
            public Drum4_MidiDiff() { }
        }

        protected readonly Drum4_MidiDiff[] difficulties = new Drum4_MidiDiff[4] { new(), new(), new(), new(), };
        protected bool enableDynamics = false;
        protected readonly bool[] toms = new bool[3];
        protected Midi_Drum_Loader_Base(byte multiplierNote) : base(
            new(new (byte[], Midi_Phrase)[] {
                new(SOLO, new(SpecialPhraseType.Solo)),
                new(new byte[]{ multiplierNote }, new(SpecialPhraseType.StarPower)),
                new(TRILL, new(SpecialPhraseType.Tremolo)),
                new(TREMOLO, new(SpecialPhraseType.Trill))
            }))
        { }

        protected override bool ProcessSpecialNote(ref InstrumentTrack<T> track)
        {
            if (note.value != 95)
                return false;

            difficulties[3].notes[1] = currEvent.position;
            if (!track[3].notes.ValidateLastKey(currEvent.position))
                track[3].notes.Add_Back_NoReturn(currEvent.position);
            return true;
        }

        protected override bool ProcessSpecialNote_Off(ref InstrumentTrack<T> track)
        {
            if (note.value != 95)
                return false;

            ulong colorPosition = difficulties[3].notes[1];
            if (colorPosition != ulong.MaxValue)
            {
                track[3].notes.Traverse_Backwards_Until(colorPosition).Set(1, currEvent.position - colorPosition);
                difficulties[3].notes[1] = ulong.MaxValue;
            }
            return true;
        }

        protected override void ParseText(ReadOnlySpan<byte> str, ref InstrumentTrack<T> track)
        {
            if (!enableDynamics && str.SequenceEqual(DYNAMICS_STRING))
                enableDynamics = true;
            else
                track.events.Get_Or_Add_Back(currEvent.position).Add(str.ToArray());
        }
    }
}
