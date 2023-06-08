using Framework.Serialization;
using Framework.Song.Tracks.Instrument.DrumTrack;
using Framework.Song.Tracks.Instrument.GuitarTrack;
using Framework.Song.Tracks.Instrument.KeysTrack;
using Framework.Song.Tracks.Instrument.ProGuitarTrack;
using Framework.Song.Tracks.Instrument.ProKeysTrack;
using Framework.Song.Tracks.Instrument;
using Framework.Song.Tracks.Notes.Drums;
using Framework.Song.Tracks.Notes.Guitar;
using Framework.Song.Tracks.Notes.Guitar_Pro;
using Framework.Song.Tracks.Notes.Keys;
using Framework.Song.Tracks.Vocals;
using Framework.Song.Tracks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Song
{
    public readonly struct NoteTracks
    {
        public readonly InstrumentTrack<FiveFret>               lead_5 = new();
        public readonly InstrumentTrack<SixFret>                lead_6 = new();
        public readonly InstrumentTrack<FiveFret>               bass_5 = new();
        public readonly InstrumentTrack<SixFret>                bass_6 = new();
        public readonly InstrumentTrack<FiveFret>               rhythm = new();
        public readonly InstrumentTrack<FiveFret>               coop = new();
        public readonly InstrumentTrack<Keys>                   keys = new();
        public readonly InstrumentTrack<Drum_4Pro>              drums_4pro = new();
        public readonly InstrumentTrack<Drum_5>                 drums5 = new();
        public readonly ProGuitarTrack<Fret_17>                 proguitar_17 = new();
        public readonly ProGuitarTrack<Fret_22>                 proguitar_22 = new();
        public readonly ProGuitarTrack<Fret_17>                 probass_17 = new();
        public readonly ProGuitarTrack<Fret_22>                 probass_22 = new();
        public readonly InstrumentTrack_Base<ProKeysDifficulty> proKeys = new();

        public readonly LeadVocalTrack    leadVocals = new();
        public readonly HarmonyVocalTrack harmonyVocals = new(3);
        public NoteTracks() { }

        public bool LoadFromMidi(MidiTrackType trackType, ref MidiFileReader reader)
        {
            byte multiplierNote = reader.GetMultiplierNote();
            switch (trackType)
            {
                case MidiTrackType.Guitar_5: return Midi_Loader.Load(new Midi_FiveFret_Loader(multiplierNote), lead_5, ref reader);
                case MidiTrackType.Bass_5:   return Midi_Loader.Load(new Midi_FiveFret_Loader(multiplierNote), bass_5, ref reader);
                case MidiTrackType.Keys:     return Midi_Loader.Load(new Midi_Keys_Loader(multiplierNote), keys, ref reader);
                case MidiTrackType.Drums:
                    {
                        LegacyDrumTrackLoader legacy = new(ref reader);
                        if (legacy.Type == DrumType.FIVE_LANE)
                            legacy.Transfer(drums5);
                        else
                            legacy.Transfer(drums_4pro);
                        return true;
                    }
                case MidiTrackType.Vocals:         return Midi_Loader.Load(new Midi_Vocal_Loader(multiplierNote, 1), 0, leadVocals, ref reader);
                case MidiTrackType.Harm1:          return Midi_Loader.Load(new Midi_Vocal_Loader(multiplierNote, (uint)harmonyVocals.vocals.Length), 0, harmonyVocals, ref reader);
                case MidiTrackType.Harm2:          return Midi_Loader.Load(new Midi_Vocal_Loader(multiplierNote, (uint)harmonyVocals.vocals.Length), 1, harmonyVocals, ref reader);
                case MidiTrackType.Harm3:          return Midi_Loader.Load(new Midi_Vocal_Loader(multiplierNote, (uint)harmonyVocals.vocals.Length), 2, harmonyVocals, ref reader);
                case MidiTrackType.Rhythm:         return Midi_Loader.Load(new Midi_FiveFret_Loader(multiplierNote), rhythm, ref reader);
                case MidiTrackType.Coop:           return Midi_Loader.Load(new Midi_FiveFret_Loader(multiplierNote), coop, ref reader);
                case MidiTrackType.Real_Guitar:    return Midi_Loader.Load(new Midi_ProGuitar_Loader<Fret_17>(multiplierNote), proguitar_17, ref reader);
                case MidiTrackType.Real_Guitar_22: return Midi_Loader.Load(new Midi_ProGuitar_Loader<Fret_22>(multiplierNote), proguitar_22, ref reader);
                case MidiTrackType.Real_Bass:      return Midi_Loader.Load(new Midi_ProGuitar_Loader<Fret_17>(multiplierNote), probass_17, ref reader);
                case MidiTrackType.Real_Bass_22:   return Midi_Loader.Load(new Midi_ProGuitar_Loader<Fret_22>(multiplierNote), probass_22, ref reader);
                case MidiTrackType.Real_Keys_X:    return Midi_Loader.Load(new Midi_ProKeys_Loader(multiplierNote), proKeys[3], ref reader);
                case MidiTrackType.Real_Keys_H:    return Midi_Loader.Load(new Midi_ProKeys_Loader(multiplierNote), proKeys[2], ref reader);
                case MidiTrackType.Real_Keys_M:    return Midi_Loader.Load(new Midi_ProKeys_Loader(multiplierNote), proKeys[1], ref reader);
                case MidiTrackType.Real_Keys_E:    return Midi_Loader.Load(new Midi_ProKeys_Loader(multiplierNote), proKeys[0], ref reader);
                case MidiTrackType.Guitar_6:       return Midi_Loader.Load(new Midi_SixFret_Loader(multiplierNote), lead_6, ref reader);
                case MidiTrackType.Bass_6:         return Midi_Loader.Load(new Midi_SixFret_Loader(multiplierNote), bass_6, ref reader);
            }
            return true;
        }

        public bool LoadFromDotChart(ref LegacyDrumTrackLoader legacy, ref ChartFileReader reader)
        {
            switch (reader.Instrument)
            {
                case NoteTracks_Chart.Single:       return DotChart_Loader.Load(ref lead_5[reader.Difficulty], ref reader);
                case NoteTracks_Chart.DoubleGuitar: return DotChart_Loader.Load(ref coop[reader.Difficulty], ref reader);
                case NoteTracks_Chart.DoubleBass:   return DotChart_Loader.Load(ref bass_5[reader.Difficulty], ref reader);
                case NoteTracks_Chart.DoubleRhythm: return DotChart_Loader.Load(ref rhythm[reader.Difficulty], ref reader);
                case NoteTracks_Chart.Drums:
                    {
                        switch (legacy.Type)
                        {
                            case DrumType.FOUR_PRO:  return DotChart_Loader.Load(ref drums_4pro[reader.Difficulty], ref reader);
                            case DrumType.FIVE_LANE: return DotChart_Loader.Load(ref drums5[reader.Difficulty], ref reader);
                            case DrumType.UNKNOWN:   return legacy.LoadDotChart(ref reader);
                        }
                        break;
                    }
                case NoteTracks_Chart.Keys:      return DotChart_Loader.Load(ref keys[reader.Difficulty], ref reader);
                case NoteTracks_Chart.GHLGuitar: return DotChart_Loader.Load(ref lead_6[reader.Difficulty], ref reader);
                case NoteTracks_Chart.GHLBass:   return DotChart_Loader.Load(ref bass_6[reader.Difficulty], ref reader);
            }
            return true;
        }

        public void FinalizeProKeys()
        {
            if (!proKeys.IsOccupied())
                return;

            proKeys.specialPhrases = proKeys[3].specialPhrases;
            proKeys.events = proKeys[3].events;
            proKeys[3].specialPhrases = new();
            proKeys[3].events = new();
            for (uint i = 0; i < 3; ++i)
            {
                proKeys[i].specialPhrases.Clear();
                proKeys[i].events.Clear();
            }
        }
    }
}
