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

        private Dictionary<string, Modifier> m_modifiers = new();

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

            Dictionary<string, List<Modifier>> modifiers = IniHandler.ReadSongIniFile(iniFile);
            if (modifiers.Remove("name", out List<Modifier>? names))
                for (int i = 0; i < names.Count; ++i)
                {
                    m_name = names[i].SORTSTR.Str;
                    if (m_name != "Unknown Title")
                        break;
                }

            if (modifiers.Remove("artist", out List<Modifier>? artists))
                for (int i = 0; i < artists.Count; ++i)
                {
                    m_artist = artists[i].SORTSTR.Str;
                    if (m_artist != "Unknown Artist")
                        break;
                }

            if (modifiers.Remove("album", out List<Modifier>? albums))
                for (int i = 0; i < albums.Count; ++i)
                {
                    m_album = albums[i].SORTSTR.Str;
                    if (m_album != "Unknown Album")
                        break;
                }

            if (modifiers.Remove("genre", out List<Modifier>? genres))
                for (int i = 0; i < genres.Count; ++i)
                {
                    m_genre = genres[i].SORTSTR.Str;
                    if (m_genre != "Unknown Genre")
                        break;
                }

            if (modifiers.Remove("year", out List<Modifier>? years))
                for (int i = 0; i < years.Count; ++i)
                {
                    m_year = years[i].SORTSTR.Str;
                    if (m_year != "Unknown Year")
                        break;
                }

            if (modifiers.Remove("charter", out List<Modifier>? charters))
                for (int i = 0; i < charters.Count; ++i)
                {
                    m_charter = charters[i].SORTSTR.Str;
                    if (m_charter != "Unknown Charter")
                        break;
                }

            if (modifiers.Remove("playlist", out List<Modifier>? playlists))
            {
                string parentPlaylist = Path.GetDirectoryName(m_directory)!;
                for (int i = 0; i < playlists.Count; ++i)
                {
                    m_playlist = playlists[i].SORTSTR.Str;
                    if (m_playlist != parentPlaylist)
                        break;
                }
            }

            if (modifiers.Remove("five_lane_drums", out List<Modifier>? fivelanes))
                m_baseDrumType = fivelanes[0].BOOL ? DrumType.FIVE_LANE : DrumType.FOUR_PRO;

            if (modifiers.Remove("hopo_frequency", out List<Modifier>? hopo_freq))
                m_hopo_frequency = hopo_freq[0].UINT64;

            if (modifiers.Remove("multiplier_note", out List<Modifier>? multiplier))
                if (multiplier[0].UINT16 == 103)
                    m_multiplier_note = 103;

            if (modifiers.Remove("eighthnote_hopo", out List<Modifier>? eighthnote))
                m_eighthnote_hopo = eighthnote[0].BOOL;

            if (modifiers.Remove("sustain_cutoff_threshold", out List<Modifier>? threshold))
                m_sustain_cutoff_threshold = threshold[0].UINT64;

            if (modifiers.Remove("hopofreq", out List<Modifier>? hopofreq_old))
                m_hopofreq_old = hopofreq_old[0].UINT16;

            foreach(var mod in modifiers)
                m_modifiers.Add(mod.Key, mod.Value[0]);
        }

        public void Load_Midi(string path, Encoding encoding)
        {
            Midi_Loader.encoding = encoding;
            MidiFileReader reader = new(path, m_multiplier_note);
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
    }
}
