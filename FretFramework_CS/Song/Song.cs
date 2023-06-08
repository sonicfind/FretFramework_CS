﻿using Framework.Serialization;
using Framework.Song.Tracks;
using Framework.Song.Tracks.Instrument;
using Framework.Song.Tracks.Instrument.DrumTrack;
using Framework.Song.Tracks.Instrument.GuitarTrack;
using Framework.Song.Tracks.Instrument.KeysTrack;
using Framework.Song.Tracks.Instrument.ProGuitarTrack;
using Framework.Song.Tracks.Instrument.ProKeysTrack;
using Framework.Song.Tracks.Notes.Drums;
using Framework.Song.Tracks.Notes.Guitar_Pro;
using Framework.Song.Tracks.Notes.Guitar;
using Framework.Song.Tracks.Notes.Interfaces;
using Framework.Song.Tracks.Notes.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Framework.Song.Tracks.Notes.Keys_Pro;
using Framework.Song.Tracks.Vocals;
using CommandLine;
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

        public bool LoadFromMidi(MidiTrackType trackType, ref MidiFileReader reader)
        {
            byte multiplierNote = reader.GetMultiplierNote();
            switch (trackType)
            {
                case MidiTrackType.Guitar_5:       return Midi_Loader.Load(new Midi_Instrument_FiveFret(multiplierNote), lead_5, ref reader);
                case MidiTrackType.Bass_5:         return Midi_Loader.Load(new Midi_Instrument_FiveFret(multiplierNote), bass_5, ref reader);
                case MidiTrackType.Keys:           return Midi_Loader.Load(new Midi_Instrument_Keys(multiplierNote), keys, ref reader);
                case MidiTrackType.Drums:
                    {
                        LegacyDrumTrackHandler legacy = new(ref reader);
                        if (legacy.Type == DrumType.FIVE_LANE)
                            legacy.Transfer(drums5);
                        else
                            legacy.Transfer(drums_4pro);
                        return true;
                    }
                case MidiTrackType.Vocals:         return Midi_Loader.Load(new Midi_Vocals(multiplierNote, 1), 0, leadVocals, ref reader);
                case MidiTrackType.Harm1:          return Midi_Loader.Load(new Midi_Vocals(multiplierNote, (uint)harmonyVocals.vocals.Length), 0, harmonyVocals, ref reader);
                case MidiTrackType.Harm2:          return Midi_Loader.Load(new Midi_Vocals(multiplierNote, (uint)harmonyVocals.vocals.Length), 1, harmonyVocals, ref reader);
                case MidiTrackType.Harm3:          return Midi_Loader.Load(new Midi_Vocals(multiplierNote, (uint)harmonyVocals.vocals.Length), 2, harmonyVocals, ref reader);
                case MidiTrackType.Rhythm:         return Midi_Loader.Load(new Midi_Instrument_FiveFret(multiplierNote), rhythm, ref reader);
                case MidiTrackType.Coop:           return Midi_Loader.Load(new Midi_Instrument_FiveFret(multiplierNote), coop, ref reader);
                case MidiTrackType.Real_Guitar:    return Midi_Loader.Load(new Midi_Instrument_ProGuitar<Fret_17>(multiplierNote), proguitar_17, ref reader);
                case MidiTrackType.Real_Guitar_22: return Midi_Loader.Load(new Midi_Instrument_ProGuitar<Fret_22>(multiplierNote), proguitar_22, ref reader);
                case MidiTrackType.Real_Bass:      return Midi_Loader.Load(new Midi_Instrument_ProGuitar<Fret_17>(multiplierNote), probass_17, ref reader);
                case MidiTrackType.Real_Bass_22:   return Midi_Loader.Load(new Midi_Instrument_ProGuitar<Fret_22>(multiplierNote), probass_22, ref reader);
                case MidiTrackType.Real_Keys_X:    return Midi_Loader.Load(new Midi_Instrument_ProKeys(multiplierNote), proKeys[3], ref reader);
                case MidiTrackType.Real_Keys_H:    return Midi_Loader.Load(new Midi_Instrument_ProKeys(multiplierNote), proKeys[2], ref reader);
                case MidiTrackType.Real_Keys_M:    return Midi_Loader.Load(new Midi_Instrument_ProKeys(multiplierNote), proKeys[1], ref reader);
                case MidiTrackType.Real_Keys_E:    return Midi_Loader.Load(new Midi_Instrument_ProKeys(multiplierNote), proKeys[0], ref reader);
                case MidiTrackType.Guitar_6:       return Midi_Loader.Load(new Midi_Instrument_SixFret(multiplierNote), lead_6, ref reader);
                case MidiTrackType.Bass_6:         return Midi_Loader.Load(new Midi_Instrument_SixFret(multiplierNote), bass_6, ref reader);
            }
            return true;
        }

        public bool LoadFromDotChart(ref LegacyDrumTrackHandler legacy, ref ChartFileReader reader)
        {
            switch (reader.Instrument)
            {
                case NoteTracks_Chart.Single:       return DotChart_Loader.Load(ref lead_5[reader.Difficulty], ref reader);
                case NoteTracks_Chart.DoubleGuitar: return DotChart_Loader.Load(ref coop[reader.Difficulty], ref reader);
                case NoteTracks_Chart.DoubleBass:   return DotChart_Loader.Load(ref bass_5[reader.Difficulty], ref reader);
                case NoteTracks_Chart.DoubleRhythm: return DotChart_Loader.Load(ref rhythm[reader.Difficulty], ref reader);
                case NoteTracks_Chart.Drums:
                    {
                        switch(legacy.Type)
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

    public class Song
    {
        public ushort Tickrate { get; private set; }
        public string midiSequenceName = string.Empty;

        public readonly SyncTrack  m_sync = new();
        public readonly SongEvents m_events = new();
        public readonly NoteTracks m_tracks = new();

        public void Load_Midi(string path, Encoding encoding)
        {
            MidiFileReader reader = new(path, 116, encoding);
            Tickrate = reader.GetTickRate();

            while (reader.StartTrack())
            {
                if (reader.GetTrackNumber() == 1)
                {
                    if (reader.GetEvent().type == MidiEventType.Text_TrackName)
                        midiSequenceName = reader.ExtractString();
                    m_sync.AddFromMidi(ref reader);
                }
                else if (reader.GetEvent().type == MidiEventType.Text_TrackName)
                {
                    ReadOnlySpan<byte> name = reader.ExtractTextOrSysEx();
                    MidiTrackType type = MidiFileReader.GetTrackType(name);
                    if (type == MidiTrackType.Events)
                    {
                        if (!m_events.AddFromMidi(ref reader, encoding))
                            Console.WriteLine($"EVENTS track appeared previously");
                    }
                    else if (!m_tracks.LoadFromMidi(type, ref reader))
                        Console.WriteLine($"Track '{Encoding.ASCII.GetString(name)}' failed to load or was already loaded previously");
                }
            }
            m_tracks.FinalizeProKeys();
        }

        internal static readonly byte[] SECTION =      Encoding.ASCII.GetBytes("section ");
        internal static readonly byte[] LYRIC =        Encoding.ASCII.GetBytes("lyric ");
        internal static readonly byte[] PHRASE_START = Encoding.ASCII.GetBytes("phrase_start");
        internal static readonly byte[] PHRASE_END =   Encoding.ASCII.GetBytes("phrase_end ");

        public void Load_Chart(string path, bool fullLoad)
        {
            ChartFileReader reader = new(path);
            if (!reader.ValidateHeaderTrack())
                throw new Exception("[Song] track expected at the start of the file");
            // Add [Song] parsing later
            reader.SkipTrack();

            LegacyDrumTrackHandler legacy = new();
            while (reader.IsStartOfTrack())
	        {
                if (reader.ValidateSyncTrack())
                    m_sync.AddFromDotChart(ref reader);
                else if (reader.ValidateEventsTrack())
                {
                    ulong phrase = ulong.MaxValue;
                    while (reader.IsStillCurrentTrack())
                    {
                        var trackEvent = reader.ParseEvent();
                        if (trackEvent.Item2 == ChartEvent.EVENT)
                        {
                            var str = reader.ExtractText();
                            if (str.StartsWith(SECTION))    m_events.sections.Get_Or_Add_Back(trackEvent.Item1) = Encoding.UTF8.GetString(str[8..]);
                            else if (str.StartsWith(LYRIC)) m_tracks.leadVocals[0][trackEvent.Item1].lyric = Encoding.UTF8.GetString(str[6..]);
                            else if (str.StartsWith(PHRASE_START))
                            {
                                if (phrase < ulong.MaxValue)
                                    m_tracks.leadVocals.specialPhrases[phrase].Add(new(SpecialPhraseType.LyricLine, trackEvent.Item1 - phrase));
                                phrase = trackEvent.Item1;
                            }
                            else if (str.StartsWith(PHRASE_END))
                            {
                                if (phrase < ulong.MaxValue)
                                {
                                    m_tracks.leadVocals.specialPhrases[phrase].Add(new(SpecialPhraseType.LyricLine, trackEvent.Item1 - phrase));
                                    phrase = ulong.MaxValue;
                                }
                            }
                            else
                                m_events.globals.Get_Or_Add_Back(trackEvent.Item1).Add(str.ToArray());
                        }
                        reader.NextEvent();
                    }
                }
                else if (!reader.ValidateDifficulty() || !reader.ValidateInstrument() || !m_tracks.LoadFromDotChart(ref legacy, ref reader))
                    reader.SkipTrack();
            }

            if (legacy.WasLoaded())
            {
                if (legacy.Type == DrumType.FIVE_LANE)
                    legacy.Transfer(m_tracks.drums5);
                else
                    legacy.Transfer(m_tracks.drums_4pro);
            }
        }
    }
}
