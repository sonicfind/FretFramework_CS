using Framework.FlatMaps;
using Framework.Serialization;
using Framework.Song.Tracks.Notes;
using Framework.Song.Tracks.Notes.Guitar;
using Framework.Song.Tracks.Notes.Interfaces;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Instrument
{
    public abstract class Midi_Loader_Base<TrackType> : Midi_Loader
        where TrackType : Track, new()
    {
        internal static readonly byte[] SYSEXTAG = Encoding.ASCII.GetBytes("PS");
        public MidiEvent currEvent;
        private ulong lastOn = 0;
        private readonly ulong[] notes_BRE = { ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue };
        private bool doBRE = false;
        protected MidiNote note;
        protected readonly Midi_PhraseList phrases;
        protected Midi_Loader_Base(Midi_PhraseList phrases) { this.phrases = phrases; }

        public bool Load(TrackType track, MidiFileReader reader)
        {
            if (track.IsOccupied())
                return false;

            while (reader.TryParseEvent())
            {
                MidiEvent ev = currEvent = reader.GetEvent();
                if (ev.type == MidiEventType.Note_On)
                {
                    reader.ExtractMidiNote(ref note);
                    if (note.velocity > 0)
                        ParseNote(ref track);
                    else
                        ParseNote_Off(ref track);

                }
                else if (ev.type == MidiEventType.Note_Off)
                {
                    reader.ExtractMidiNote(ref note);
                    ParseNote_Off(ref track);
                }
                else if (ev.type == MidiEventType.SysEx || ev.type == MidiEventType.SysEx_End)
                    ParseSysEx(reader.ExtractTextOrSysEx(), ref track);
                else if (ev.type <= MidiEventType.Text_EnumLimit)
                    ParseText(reader.ExtractTextOrSysEx(), ref track);
            }

            track.TrimExcess();
            return true;
        }

        private void ParseNote(ref TrackType track)
        {
            NormalizeNoteOnPosition();
            if (ProcessSpecialNote(ref track))
                return;

            if (IsNote())
                ParseLaneColor(ref track);
            else if (!AddPhrase(ref track.specialPhrases, note))
            {
                if (120 <= note.value && note.value <= 124)
                    ParseBRE(note.value);
                else
                    ToggleExtraValues(ref track);
            }
        }

        protected void NormalizeNoteOnPosition()
        {
            if (currEvent.position < lastOn + 16)
                currEvent.position = lastOn;
            else
                lastOn = currEvent.position;
        }

        private void ParseNote_Off(ref TrackType track)
        {
            if (ProcessSpecialNote_Off(ref track))
                return;

            if (IsNote())
                ParseLaneColor_Off(ref track);
            else if (!AddPhrase_Off(ref track.specialPhrases, note))
            {
                if (120 <= note.value && note.value <= 124)
                    ParseBRE_Off(note.value, ref track);
                else
                    ToggleExtraValues_Off(ref track);
            }
        }

        protected abstract void ParseLaneColor(ref TrackType track);

        protected abstract void ParseLaneColor_Off(ref TrackType track);

        protected virtual void ParseText(ReadOnlySpan<byte> str, ref TrackType track)
        {
            track.events.Get_Or_Add_Back(currEvent.position).Add(encoding.GetString(str));
        }

        protected virtual bool IsNote() { return 60 <= note.value && note.value <= 100; }

        protected virtual bool ProcessSpecialNote(ref TrackType track) { return false; }

        protected virtual void ToggleExtraValues(ref TrackType track) {}

        protected virtual bool ProcessSpecialNote_Off(ref TrackType track) { return false; }

        protected virtual void ToggleExtraValues_Off(ref TrackType track) { }

        protected virtual void ParseSysEx(ReadOnlySpan<byte> str, ref TrackType track) { }

        private bool AddPhrase(ref TimedFlatMap<List<SpecialPhrase>> phrases, MidiNote note)
        {
            return this.phrases.AddPhrase(ref phrases, currEvent.position, note);
        }

        private bool AddPhrase_Off(ref TimedFlatMap<List<SpecialPhrase>> phrases, MidiNote note)
        {
            return this.phrases.AddPhrase_Off(ref phrases, currEvent.position, note);
        }

        private void ParseBRE(uint midiValue)
        {
            notes_BRE[midiValue - 120] = currEvent.position;
            doBRE = notes_BRE[0] == notes_BRE[1] && notes_BRE[1] == notes_BRE[2] && notes_BRE[2] == notes_BRE[3];
        }

        private void ParseBRE_Off(uint midiValue, ref TrackType track)
        {
            if (doBRE)
            {
                ref var phrasesList = ref track.specialPhrases[notes_BRE[0]];
                phrasesList.Add(new(SpecialPhraseType.BRE, currEvent.position - notes_BRE[0]));

                for (int i = 0; i < 5; i++)
                    notes_BRE[0] = ulong.MaxValue;
                doBRE = false;
            }
        }
    }

    public abstract class Midi_Instrument_Loader<T> : Midi_Loader_Base<InstrumentTrack<T>>
        where T : unmanaged, INote
    {
        internal static readonly byte[] SOLO = { 103 };
        internal static readonly byte[] TREMOLO = { 126 };
        internal static readonly byte[] TRILL = { 127 };
        internal static readonly int[] DIFFVALUES = {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3
        };
        protected Midi_Instrument_Loader(Midi_PhraseList phrases) : base(phrases) { }
    }
}
