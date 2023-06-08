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
    public class Midi_Vocal_Loader
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


        public Midi_Vocal_Loader(byte multiplierNote, uint numTracks)
        {
            phrases = new(new (byte[], Midi_Phrase)[] {
                new(LYRICLINE, new(SpecialPhraseType.LyricLine)),
                new(new byte[]{ multiplierNote }, new(SpecialPhraseType.StarPower)),
                new(RANGESHIFT, new(SpecialPhraseType.RangeShift)),
                new(LYRICSHIFT, new(SpecialPhraseType.LyricShift)),
                new(HARMONYLINE, new(SpecialPhraseType.HarmonyLine)),
            });
        }

        public void ParseVocal(uint index, uint pitch, Encoding encoding, ref VocalTrack track)
        {
            if (vocal != ulong.MaxValue)
            {
                ulong duration = position - vocal;
                if (duration > 240)
                    duration -= 120;
                else
                    duration /= 2;

                ref Vocal note = ref AddVocal(index, vocal, encoding, ref track);
                note.pitch.Binary = pitch;
                note.duration = duration;
                lyric.Item1 = ulong.MaxValue;
                lyric.Item2 = Array.Empty<byte>();
            }

            vocal = position;
            if (lyric.Item1 != ulong.MaxValue)
                lyric.Item1 = position;
        }

        public void ParseVocal_Off(uint index, uint pitch, Encoding encoding, ref VocalTrack track)
        {
            if (vocal != ulong.MaxValue)
            {
                ref Vocal note = ref AddVocal(index, vocal, encoding, ref track);
                note.pitch.Binary = pitch;
                note.duration = position - vocal;
                lyric.Item1 = ulong.MaxValue;
                lyric.Item2 = Array.Empty<byte>();
            }
            vocal = ulong.MaxValue;
        }

        public void ParseText(uint index, ReadOnlySpan<byte> str, Encoding encoding, ref VocalTrack track)
        {
            if (str.Length == 0)
                return;

            if (str[0] != '[')
            {
                if (lyric.Item1 != ulong.MaxValue)
                    AddVocal(index, lyric.Item1, encoding, ref track);
                lyric.Item1 = vocal != ulong.MaxValue ? vocal : position;
                lyric.Item2 = str.ToArray();
            }
            else if (index == 0)
                track.events.Get_Or_Add_Back(position).Add(str.ToArray());
        }

        private ref Vocal AddVocal(uint index, ulong vocalPos, Encoding encoding, ref VocalTrack track)
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

        public void AddPercussion_Off(bool playable, ref VocalTrack track)
        {
            if (percussion != ulong.MaxValue)
            {
                track.percussion.Get_Or_Add_Back(percussion).IsPlayable = playable;
                percussion = ulong.MaxValue;
            }
        }

        public void AddPhrase(ref TimedFlatMap<List<SpecialPhrase>> phrases, MidiNote note)
        {
            this.phrases.AddPhrase(ref phrases, position, note);
        }

        public void AddPhrase_Off(ref TimedFlatMap<List<SpecialPhrase>> phrases, MidiNote note)
        {
            this.phrases.AddPhrase_Off(ref phrases, position, note);
        }

        public void AddHarmonyLine(ref TimedFlatMap<List<SpecialPhrase>> phrases)
        {
            this.phrases.AddPhrase(ref phrases, position, SpecialPhraseType.HarmonyLine, 100);
        }

        public void AddHarmonyLine_Off(ref TimedFlatMap<List<SpecialPhrase>> phrases)
        {
            this.phrases.AddPhrase_Off(ref phrases, position, SpecialPhraseType.HarmonyLine);
        }

        public bool IsNote(uint value) { return 36 <= value && value <= 84; }
    }
}
