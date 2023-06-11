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

    public class Midi_Loader
    {
        public static Encoding encoding = Encoding.UTF8;
    }
}
