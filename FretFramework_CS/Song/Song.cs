using Framework.Serialization;
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
    public class Song
    {
        public ushort Tickrate { get; private set; }
        public string midiSequenceName = string.Empty;

        public readonly SyncTrack  m_sync = new();
        public readonly SongEvents m_events = new();
        public readonly NoteTracks m_tracks = new();

        public void Load_Midi(string path, Encoding encoding)
        {
            Midi_Loader.encoding = encoding;
            MidiFileReader reader = new(path, 116);
            Tickrate = reader.GetTickRate();

            while (reader.StartTrack())
            {
                if (reader.GetTrackNumber() == 1)
                {
                    if (reader.GetEvent().type == MidiEventType.Text_TrackName)
                        midiSequenceName = encoding.GetString(reader.ExtractTextOrSysEx());
                    m_sync.AddFromMidi(ref reader);
                }
                else if (reader.GetEvent().type == MidiEventType.Text_TrackName)
                {
                    string name = Encoding.ASCII.GetString(reader.ExtractTextOrSysEx());
                    if (MidiFileReader.TRACKNAMES.TryGetValue(name, out MidiTrackType type))
                    {
                        if (type == MidiTrackType.Events)
                        {
                            if (!m_events.AddFromMidi(ref reader, encoding))
                                Console.WriteLine($"EVENTS track appeared previously");
                        }
                        else if (!m_tracks.LoadFromMidi(type, ref reader))
                            Console.WriteLine($"Track '{name}' failed to load or was already loaded previously");
                    }
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

            LegacyDrumTrack legacy = new();
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

            if (legacy.IsOccupied())
            {
                if (legacy.Type == DrumType.FIVE_LANE)
                    legacy.Transfer(m_tracks.drums5);
                else
                    legacy.Transfer(m_tracks.drums_4pro);
            }
        }
    }
}
