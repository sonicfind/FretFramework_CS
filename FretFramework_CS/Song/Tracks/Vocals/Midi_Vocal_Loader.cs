using Framework.FlatMaps;
using Framework.Serialization;
using Framework.Song.Tracks.Notes.Vocal;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks.Vocals
{
    public class Midi_Vocal_Loader : Midi_Loader
    {
        internal static readonly byte[] LYRICLINE = { 105, 106 };
        internal static readonly byte[] HARMONYLINE = { 0xFF };
        internal static readonly byte[] RANGESHIFT = { 0 };
        internal static readonly byte[] LYRICSHIFT = { 1 };

        public ulong position;
        private ulong percussion = ulong.MaxValue;
        private ulong vocal = ulong.MaxValue;
        private readonly Midi_PhraseList phrases;
        private (ulong, byte[]) lyric = new(ulong.MaxValue, Array.Empty<byte>());
        private readonly int index;

        public Midi_Vocal_Loader(byte multiplierNote, int index)
        {
            phrases = new(new (byte[], Midi_Phrase)[] {
                new(LYRICLINE, new(SpecialPhraseType.LyricLine)),
                new(new byte[]{ multiplierNote }, new(SpecialPhraseType.StarPower)),
                new(RANGESHIFT, new(SpecialPhraseType.RangeShift)),
                new(LYRICSHIFT, new(SpecialPhraseType.LyricShift)),
                new(HARMONYLINE, new(SpecialPhraseType.HarmonyLine)),
            });
            this.index = index;
        }

        public bool Load(VocalTrack track, MidiFileReader reader)
        {
            if (!track[index].IsEmpty())
                return false;

            while (reader.TryParseEvent())
            {
                MidiEvent ev = reader.GetEvent();
                position = ev.position;
                if (ev.type == MidiEventType.Note_On)
                {
                    MidiNote note = reader.ExtractMidiNote();
                    if (note.velocity > 0)
                        ParseNote(note, ref track);
                    else
                        ParseNote_Off(note, ref track);

                }
                else if (ev.type == MidiEventType.Note_Off)
                    ParseNote_Off(reader.ExtractMidiNote(), ref track);
                else if (ev.type <= MidiEventType.Text_EnumLimit)
                    ParseText(reader.ExtractTextOrSysEx(), ref track);
            }

            track.TrimExcess();
            return true;
        }

        private void ParseNote(MidiNote note, ref VocalTrack track)
        {
            if (IsNote(note.value))
                ParseVocal(note.value, ref track);
            else if (index == 0)
            {
                if (note.value == 96 || note.value == 97)
                    AddPercussion();
                else
                    AddPhrase(ref track.specialPhrases, note);
            }
            else if (index == 1)
            {
                if (note.value == 105 || note.value == 106)
                    AddHarmonyLine(ref track.specialPhrases);
            }
        }

        private void ParseNote_Off(MidiNote note, ref VocalTrack track)
        {
            if (IsNote(note.value))
                ParseVocal_Off(note.value, ref track);
            else if (index == 0)
            {
                if (note.value == 96)
                    AddPercussion_Off(true, ref track);
                else if (note.value == 97)
                    AddPercussion_Off(false, ref track);
                else
                    AddPhrase_Off(ref track.specialPhrases, note);
            }
            else if (index == 1)
            {
                if (note.value == 105 || note.value == 106)
                    AddHarmonyLine_Off(ref track.specialPhrases);
            }
        }

        private void ParseText(ReadOnlySpan<byte> str, ref VocalTrack track)
        {
            if (str.Length == 0)
                return;

            if (str[0] != '[')
            {
                if (lyric.Item1 != ulong.MaxValue)
                    AddVocal(lyric.Item1, ref track);
                lyric.Item1 = vocal != ulong.MaxValue ? vocal : position;
                lyric.Item2 = str.ToArray();
            }
            else if (index == 0)
                track.events.Get_Or_Add_Back(position).Add(encoding.GetString(str));
        }

        private void ParseVocal(uint pitch, ref VocalTrack track)
        {
            if (vocal != ulong.MaxValue && lyric.Item1 != ulong.MaxValue)
            {
                ulong duration = position - vocal;
                if (duration > 240)
                    duration -= 120;
                else
                    duration /= 2;

                ref Vocal note = ref AddVocal(vocal, ref track);
                note.pitch.Binary = pitch;
                note.duration = duration;
                lyric.Item1 = ulong.MaxValue;
                lyric.Item2 = Array.Empty<byte>();
            }

            vocal = position;
            if (lyric.Item1 != ulong.MaxValue)
                lyric.Item1 = position;
        }

        private void ParseVocal_Off(uint pitch, ref VocalTrack track)
        {
            if (vocal != ulong.MaxValue && lyric.Item1 != ulong.MaxValue)
            {
                ref Vocal note = ref AddVocal(vocal, ref track);
                note.pitch.Binary = pitch;
                note.duration = position - vocal;
                lyric.Item1 = ulong.MaxValue;
                lyric.Item2 = Array.Empty<byte>();
            }
            vocal = ulong.MaxValue;
        }

        private ref Vocal AddVocal(ulong vocalPos, ref VocalTrack track)
        {
            var vocals = track[index];
            if (vocals.Capacity == 0)
                vocals.Capacity = 500;

            return ref vocals.Add_Back(vocalPos, new(encoding.GetString(lyric.Item2)));
        }

        public void AddPercussion()
        {
            percussion = position;
        }

        private void AddPercussion_Off(bool playable, ref VocalTrack track)
        {
            if (percussion != ulong.MaxValue)
            {
                track.percussion.Get_Or_Add_Back(percussion).IsPlayable = playable;
                percussion = ulong.MaxValue;
            }
        }

        private void AddPhrase(ref TimedFlatMap<List<SpecialPhrase>> phrases, MidiNote note)
        {
            this.phrases.AddPhrase(ref phrases, position, note);
        }

        private void AddPhrase_Off(ref TimedFlatMap<List<SpecialPhrase>> phrases, MidiNote note)
        {
            this.phrases.AddPhrase_Off(ref phrases, position, note);
        }

        private void AddHarmonyLine(ref TimedFlatMap<List<SpecialPhrase>> phrases)
        {
            this.phrases.AddPhrase(ref phrases, position, SpecialPhraseType.HarmonyLine, 100);
        }

        private void AddHarmonyLine_Off(ref TimedFlatMap<List<SpecialPhrase>> phrases)
        {
            this.phrases.AddPhrase_Off(ref phrases, position, SpecialPhraseType.HarmonyLine);
        }

        private static bool IsNote(uint value) { return 36 <= value && value <= 84; }
    }
}
