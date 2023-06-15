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
using Framework.Types;

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

        public bool LoadFromMidi(MidiTrackType trackType, DrumType drumType, MidiFileReader reader)
        {
            switch (trackType)
            {
                case MidiTrackType.Guitar_5: return new Midi_FiveFret_Loader(reader.GetMultiplierNote()).Load(lead_5, reader);
                case MidiTrackType.Bass_5:   return new Midi_FiveFret_Loader(reader.GetMultiplierNote()).Load(bass_5, reader);
                case MidiTrackType.Keys:     return new Midi_Keys_Loader(reader.GetMultiplierNote()).Load(keys, reader);
                case MidiTrackType.Drums:
                    {
                        if (drumType == DrumType.FOUR_PRO)
                            return new Midi_Drum4Pro_Loader(reader.GetMultiplierNote()).Load(drums_4pro, reader);

                        if (drumType == DrumType.FIVE_LANE)
                            return new Midi_Drum5_Loader(reader.GetMultiplierNote()).Load(drums5, reader);

                        LegacyDrumTrack legacy = new();
                        if (legacy.LoadMidi(reader) == DrumType.FIVE_LANE)
                            legacy.Transfer(drums5);
                        else
                            legacy.Transfer(drums_4pro);
                        return true;
                    }
                case MidiTrackType.Vocals:         return new Midi_Vocal_Loader(reader.GetMultiplierNote(), 0).Load(leadVocals, reader);
                case MidiTrackType.Harm1:          return new Midi_Vocal_Loader(reader.GetMultiplierNote(), 0).Load(harmonyVocals, reader);
                case MidiTrackType.Harm2:          return new Midi_Vocal_Loader(reader.GetMultiplierNote(), 1).Load(harmonyVocals, reader);
                case MidiTrackType.Harm3:          return new Midi_Vocal_Loader(reader.GetMultiplierNote(), 2).Load(harmonyVocals, reader);
                case MidiTrackType.Rhythm:         return new Midi_FiveFret_Loader(reader.GetMultiplierNote()).Load(rhythm, reader);
                case MidiTrackType.Coop:           return new Midi_FiveFret_Loader(reader.GetMultiplierNote()).Load(coop, reader);
                case MidiTrackType.Real_Guitar:    return new Midi_ProGuitar_Loader<Fret_17>(reader.GetMultiplierNote()).Load(proguitar_17, reader);
                case MidiTrackType.Real_Guitar_22: return new Midi_ProGuitar_Loader<Fret_22>(reader.GetMultiplierNote()).Load(proguitar_22, reader);
                case MidiTrackType.Real_Bass:      return new Midi_ProGuitar_Loader<Fret_17>(reader.GetMultiplierNote()).Load(probass_17, reader);
                case MidiTrackType.Real_Bass_22:   return new Midi_ProGuitar_Loader<Fret_22>(reader.GetMultiplierNote()).Load(probass_22, reader);
                case MidiTrackType.Real_Keys_X:    return new Midi_ProKeys_Loader(reader.GetMultiplierNote()).Load(proKeys[3], reader);
                case MidiTrackType.Real_Keys_H:    return new Midi_ProKeys_Loader(reader.GetMultiplierNote()).Load(proKeys[2], reader);
                case MidiTrackType.Real_Keys_M:    return new Midi_ProKeys_Loader(reader.GetMultiplierNote()).Load(proKeys[1], reader);
                case MidiTrackType.Real_Keys_E:    return new Midi_ProKeys_Loader(reader.GetMultiplierNote()).Load(proKeys[0], reader);
                case MidiTrackType.Guitar_6:       return new Midi_SixFret_Loader(reader.GetMultiplierNote()).Load(lead_6, reader);
                case MidiTrackType.Bass_6:         return new Midi_SixFret_Loader(reader.GetMultiplierNote()).Load(bass_6, reader);
            }
            return true;
        }

        public bool LoadFromDotChart(ref LegacyDrumTrack legacy, ChartFileReader reader)
        {
            switch (reader.Instrument)
            {
                case NoteTracks_Chart.Single:       return DotChart_Loader.Load(ref lead_5[reader.Difficulty], reader);
                case NoteTracks_Chart.DoubleGuitar: return DotChart_Loader.Load(ref coop[reader.Difficulty], reader);
                case NoteTracks_Chart.DoubleBass:   return DotChart_Loader.Load(ref bass_5[reader.Difficulty], reader);
                case NoteTracks_Chart.DoubleRhythm: return DotChart_Loader.Load(ref rhythm[reader.Difficulty], reader);
                case NoteTracks_Chart.Drums:
                    {
                        switch (legacy.Type)
                        {
                            case DrumType.FOUR_PRO:  return DotChart_Loader.Load(ref drums_4pro[reader.Difficulty], reader);
                            case DrumType.FIVE_LANE: return DotChart_Loader.Load(ref drums5[reader.Difficulty], reader);
                            case DrumType.UNKNOWN:   return legacy.LoadDotChart(reader);
                        }
                        break;
                    }
                case NoteTracks_Chart.Keys:      return DotChart_Loader.Load(ref keys[reader.Difficulty], reader);
                case NoteTracks_Chart.GHLGuitar: return DotChart_Loader.Load(ref lead_6[reader.Difficulty], reader);
                case NoteTracks_Chart.GHLBass:   return DotChart_Loader.Load(ref bass_6[reader.Difficulty], reader);
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
            for (int i = 0; i < 3; ++i)
            {
                proKeys[i].specialPhrases.Clear();
                proKeys[i].events.Clear();
            }
        }
    }
}
