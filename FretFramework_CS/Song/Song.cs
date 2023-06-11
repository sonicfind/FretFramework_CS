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
using Framework.Modifiers;
using System.IO;
using Framework.Ini;

namespace Framework.Song
{
    public class Song
    {
        private string m_directory = string.Empty;
        private string m_name = string.Empty;
        private string m_artist = string.Empty;
        private string m_album = string.Empty;
        private string m_genre = string.Empty;
        private string m_year = string.Empty;
        private string m_charter = string.Empty;
        private string m_playlist = string.Empty;

        private ulong m_hopo_frequency = 0;
        private ulong m_sustain_cutoff_threshold = 0;
        private ushort m_hopofreq_old = ushort.MaxValue;
        private bool m_eighthnote_hopo = false;
        private byte m_multiplier_note = 116;
        private DrumType m_baseDrumType = DrumType.UNKNOWN;

        private List<Modifier> m_modifiers = new();

        public ushort Tickrate { get; private set; }
        public string m_midiSequenceName = string.Empty;

        public readonly SyncTrack  m_sync = new();
        public readonly SongEvents m_events = new();
        public readonly NoteTracks m_tracks = new();

        public Song() { }
        public Song(string directory)
        {
            m_directory = Path.GetFullPath(directory);
        }

        public void Load_Ini()
        {
            string iniFile = Path.Combine(m_directory, "song.ini");
            if (!File.Exists(iniFile))
                return;

            bool five_lane_drumsSet = false;
            bool hopo_frequencySet = false;
            bool multiplier_noteSet = false;
            bool eighthnote_hopoSet = false;
            bool sustain_thresholdSet = false;
            bool hopofreqSet = false;
            var modifiers = IniHandler.ReadSongIniFile(iniFile);
            if (modifiers == null)
                return;

            for (int i = 0; i < modifiers.Count; ++i)
            {
                Modifier mod = modifiers[i];
                if (mod.Name == "name")
                {
                    if (m_name.Length == 0 || m_name == "Unknown Title")
				        m_name = mod.SORTSTR.Str;
                }
                else if (mod.Name == "artist")
                {
                    if (m_artist.Length == 0 || m_artist == "Unknown Artist")
				        m_artist = mod.SORTSTR.Str;
                }
                else if (mod.Name == "album")
                {
                    if (m_album.Length == 0 || m_album == "Unknown Album")
				        m_album = mod.SORTSTR.Str;
                }
                else if (mod.Name == "genre")
                {
                    if (m_genre.Length == 0 || m_genre == "Unknown Genre")
				        m_genre = mod.SORTSTR.Str;
                }
                else if (mod.Name == "year")
                {
                    if (m_year.Length == 0 || m_year == "Unknown Year")
				        m_year = mod.SORTSTR.Str;
                }
                else if (mod.Name == "charter")
                {
                    if (m_charter.Length == 0 || m_charter == "Unknown Charter")
				        m_charter = mod.SORTSTR.Str;
                }
                else if (mod.Name == "playlist")
                {
                    if (m_playlist.Length == 0 || m_playlist == Path.GetDirectoryName(m_directory))
                        m_playlist = mod.SORTSTR.Str;
                }
                else if (mod.Name == "five_lane_drums")
                {
                    if (!five_lane_drumsSet && m_baseDrumType == DrumType.UNKNOWN)
                        m_baseDrumType = mod.BOOL ? DrumType.FIVE_LANE : DrumType.FOUR_PRO;
                    five_lane_drumsSet = true;
                }
                else if (mod.Name == "hopo_frequency")
                {
                    if (!hopo_frequencySet)
                        m_hopo_frequency = mod.UINT64;
                    hopo_frequencySet = true;
                }
                else if (mod.Name == "multiplier_note")
                {
                    if (!multiplier_noteSet && mod.UINT16 == 103)
                        m_multiplier_note = 103;
                    multiplier_noteSet = true;
                }
                else if (mod.Name == "eighthnote_hopo")
                {
                    if (!eighthnote_hopoSet)
                        m_eighthnote_hopo = mod.BOOL;
                    eighthnote_hopoSet = true;
                }
                else if (mod.Name == "sustain_cutoff_threshold")
                {
                    if (!sustain_thresholdSet)
                        m_sustain_cutoff_threshold = mod.UINT64;
                    sustain_thresholdSet = true;
                }
                else if (mod.Name == "hopofreq")
                {
                    if (!hopofreqSet)
                        m_hopofreq_old = mod.UINT16;
                    hopofreqSet = true;
                }
                else if (GetModifier(mod.Name) == null)
                    m_modifiers.Add(mod);
            }
        }

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
                        m_midiSequenceName = encoding.GetString(reader.ExtractTextOrSysEx());
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
                        else if (!m_tracks.LoadFromMidi(type, m_baseDrumType, ref reader))
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

            LegacyDrumTrack legacy = new(m_baseDrumType);
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
                            else if (str.SequenceEqual(PHRASE_START))
                            {
                                if (phrase < ulong.MaxValue)
                                    m_tracks.leadVocals.specialPhrases[phrase].Add(new(SpecialPhraseType.LyricLine, trackEvent.Item1 - phrase));
                                phrase = trackEvent.Item1;
                            }
                            else if (str.SequenceEqual(PHRASE_END))
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

        private Modifier? GetModifier(string name)
        {
            foreach (var modifier in m_modifiers)
                if (modifier.Name == name)
                    return modifier;
            return null;
        }
    }
}
