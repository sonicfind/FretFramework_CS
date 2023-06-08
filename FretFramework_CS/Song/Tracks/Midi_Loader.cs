using Framework.FlatMaps;
using Framework.Serialization;
using Framework.Song.Tracks.Instrument;
using Framework.Song.Tracks.Notes;
using Framework.Song.Tracks.Vocals;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song.Tracks
{
    public struct Midi_Phrase
    {
        public SpecialPhraseType Type { get; init; }
        public ulong Position { get; set; } = ulong.MaxValue;
        public uint Velocity { get; set; } = 0;
        public Midi_Phrase(SpecialPhraseType type) { Type = type; }
    }

    public class Midi_PhraseList
    {
        private readonly (byte[], Midi_Phrase)[] _phrases;
        public Midi_PhraseList((byte[], Midi_Phrase)[] phrases) { _phrases = phrases; }

        public bool AddPhrase(ref TimedFlatMap<List<SpecialPhrase>> phrases, ulong position, MidiNote note)
		{
            for (int i = 0; i < _phrases.Length; ++i)
            {
                foreach (byte val in _phrases[i].Item1)
                {
					if (val == note.value)
					{
                        phrases.Get_Or_Add_Back(position);
                        _phrases[i].Item2.Position = position;
                        _phrases[i].Item2.Velocity = note.velocity;
						return true;
					}
				}
            }
			return false;
		}

        public bool AddPhrase_Off(ref TimedFlatMap<List<SpecialPhrase>> phrases, ulong position, MidiNote note)
        {
            for (int i = 0; i < _phrases.Length; ++i)
            {
                foreach (byte val in _phrases[i].Item1)
                {
                    if (val == note.value)
                    {
                        ref var phr = ref _phrases[i].Item2;
                        if (phr.Position != ulong.MaxValue)
                        {
                            phrases.Traverse_Backwards_Until(phr.Position).Add(new(phr.Type, position - phr.Position, phr.Velocity));
                            phr.Position = ulong.MaxValue;
                        }
                        return true;
                    }
                }
            }
            return false;
        }

        public bool AddPhrase(ref TimedFlatMap<List<SpecialPhrase>> phrases, ulong position, SpecialPhraseType type, byte velocity)
        {
            for (int i = 0; i < _phrases.Length; ++i)
            {
                ref var phr = ref _phrases[i].Item2;
                if (phr.Type == type)
                {
                    phrases.Get_Or_Add_Back(position);
                    _phrases[i].Item2.Position = position;
                    _phrases[i].Item2.Velocity = velocity;
                    return true;
                }
            }
            return false;
        }

        public bool AddPhrase_Off(ref TimedFlatMap<List<SpecialPhrase>> phrases, ulong position, SpecialPhraseType type)
        {
            for (int i = 0; i < _phrases.Length; ++i)
            {
                ref var phr = ref _phrases[i].Item2;
                if (phr.Type == type)
                {
                    if (phr.Position != ulong.MaxValue)
                    {
                        phrases.Traverse_Backwards_Until(phr.Position).Add(new(phr.Type, position - phr.Position, phr.Velocity));
                        phr.Position = ulong.MaxValue;
                    }
                    return true;
                }
            }
            return false;
        }
    }

    public static class Midi_Loader
    {
        public static bool Load<TrackType>(Midi_Loader_Base<TrackType> loader, TrackType track, ref MidiFileReader reader)
            where TrackType : Track, new()
        {
            if (track.IsOccupied())
                return false;

            while (reader.TryParseEvent())
            {
                MidiEvent ev = loader.currEvent = reader.GetEvent();
                if (ev.type == MidiEventType.Note_On)
                {
                    MidiNote note = reader.ExtractMidiNote();
                    if (note.velocity > 0)
                        ParseNote(note, ref loader, ref track);
                    else
                        ParseNote_Off(note, ref loader, ref track);

                }
                else if (ev.type == MidiEventType.Note_Off)
                    ParseNote_Off(reader.ExtractMidiNote(), ref loader, ref track);
                else if (ev.type == MidiEventType.SysEx || ev.type == MidiEventType.SysEx_End)
                    loader.ParseSysEx(reader.ExtractTextOrSysEx(), ref track);
                else if (ev.type <= MidiEventType.Text_EnumLimit)
                    loader.ParseText(reader.ExtractTextOrSysEx(), ref track);
            }

            track.TrimExcess();
            return true;
        }

        public static bool Load(Midi_Vocals loader, uint index, VocalTrack track, ref MidiFileReader reader)
        {
            if (!track[index].IsEmpty())
                return false;

            while (reader.TryParseEvent())
            {
                MidiEvent ev = reader.GetEvent();
                loader.position = ev.position;
                if (ev.type == MidiEventType.Note_On)
                {
                    MidiNote note = reader.ExtractMidiNote();
                    if (note.velocity > 0)
                        ParseVocal(index, note, reader.GetEncoding(), ref loader, ref track);
                    else
                        ParseVocal_Off(index, note, reader.GetEncoding(), ref loader, ref track);

                }
                else if (ev.type == MidiEventType.Note_Off)
                    ParseVocal_Off(index, reader.ExtractMidiNote(), reader.GetEncoding(), ref loader, ref track);
                else if (ev.type <= MidiEventType.Text_EnumLimit)
                    loader.ParseText(index, reader.ExtractTextOrSysEx(), reader.GetEncoding(), ref track);
            }

            track.TrimExcess();
            return true;
        }

        internal static void ParseNote<TrackType>(MidiNote note, ref Midi_Loader_Base<TrackType> loader, ref TrackType track)
            where TrackType : Track, new()
        {
            loader.NormalizeNoteOnPosition();
            if (loader.ProcessSpecialNote(note, ref track))
                return;

            if (loader.IsNote(note.value))
                loader.ParseLaneColor(note, ref track);
            else if (!loader.AddPhrase(ref track.specialPhrases, note))
            {
                if (120 <= note.value && note.value <= 124)
                    loader.ParseBRE(note.value, ref track);
                else
                    loader.ToggleExtraValues(note, ref track);
            }
        }

        internal static void ParseNote_Off<TrackType>(MidiNote note, ref Midi_Loader_Base<TrackType> loader, ref TrackType track)
            where TrackType : Track, new()
        {

            if (loader.ProcessSpecialNote(note, ref track))
                return;

            if (loader.IsNote(note.value))
                loader.ParseLaneColor_Off(note, ref track);
            else if (!loader.AddPhrase_Off(ref track.specialPhrases, note))
            {
                if (120 <= note.value && note.value <= 124)
                    loader.ParseBRE_Off(note.value, ref track);
                else
                    loader.ToggleExtraValues_Off(note, ref track);
            }
        }

        internal static void ParseVocal(uint index, MidiNote note, Encoding encoding, ref Midi_Vocals loader, ref VocalTrack track)
        {
            if (loader.IsNote(note.value))
                loader.ParseVocal(index, note.value, encoding, ref track);
            else if (index == 0)
            {
                if (note.value == 96 || note.value == 97)
                    loader.AddPercussion();
                else
                    loader.AddPhrase(ref track.specialPhrases, note);
            }
            else if (index == 1)
            {
                if (note.value == 105 || note.value == 106)
                    loader.AddHarmonyLine(ref track.specialPhrases);
            }
        }

        internal static void ParseVocal_Off(uint index, MidiNote note, Encoding encoding, ref Midi_Vocals loader, ref VocalTrack track)
        {
            if (loader.IsNote(note.value))
                loader.ParseVocal(index, note.value, encoding, ref track);
            else if (index == 0)
            {
                if (note.value == 96)
                    loader.AddPercussion_Off(true, ref track);
                else if (note.value == 97)
                    loader.AddPercussion_Off(false, ref track);
                else
                    loader.AddPhrase_Off(ref track.specialPhrases, note);
            }
            else if (index == 1)
            {
                if (note.value == 105 || note.value == 106)
                    loader.AddHarmonyLine_Off(ref track.specialPhrases);
            }
        }
    }
}
