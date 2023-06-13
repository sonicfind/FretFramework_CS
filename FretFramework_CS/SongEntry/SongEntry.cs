using Framework.Serialization;
using Framework.Song.Tracks.Instrument.DrumTrack;
using Framework.Song;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Framework.SongEntry.TrackScan.Instrument.Drums;
using Framework.Song.Tracks.Notes.Drums;
using Framework.Ini;
using Framework.Modifiers;
using System.Runtime.InteropServices;
using Framework.SongEntry.TrackScan;
using System.IO;
using System.Xml.Linq;
using System.Diagnostics;

namespace Framework.SongEntry
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class SongEntry
    {
        private static readonly SortString s_DEFAULT_ARTIST =  new("Unknown Artist");
        private static readonly SortString s_DEFAULT_ALBUM =   new("Unknown Album");
        private static readonly SortString s_DEFAULT_GENRE =   new("Unknown Genre");
        private static readonly SortString s_DEFAULT_YEAR =    new("Unknown Year");
        private static readonly SortString s_DEFAULT_CHARTER = new("Unknown Charter");

        private SortString m_name;
        private SortString m_artist;
        private SortString m_album;
        private SortString m_genre;
        private SortString m_year;
        private SortString m_charter;
        private SortString m_playlist;

        private ulong m_song_length = 0;
        private float m_previewStart = 0.0f;
        private float m_previewEnd = 0.0f;
        private ushort m_album_track = ushort.MaxValue;
        private ushort m_playlist_track = ushort.MaxValue;
        private string m_icon = string.Empty;
        private string m_source = string.Empty;
    
        private ulong m_hopo_frequency = 0;
        private ulong m_sustain_cutoff_threshold = 0;
        private ushort m_hopofreq_Old = ushort.MaxValue;
        private bool m_eighthnote_hopo = false;
        private byte m_multiplier_note = 116;

        private SortString m_directory_playlist;

        private Dictionary<string, List<Modifier>> m_modifiers = new();
        private TrackScans m_scans = new();

        private (string, ChartType) m_chartFile;
        private readonly DateTime m_chartWriteTime;
        private DateTime m_iniWriteTime;

        public SortString Artist => m_artist;
        public SortString Name => m_name;
        public SortString Album => m_album;
        public SortString Genre => m_genre;
        public SortString Year => m_year;
        public SortString Charter => m_charter;
        public SortString Playlist => m_playlist;
        public ulong SongLength => m_song_length;
        public ushort Hopofreq_Old => m_hopofreq_Old;
        public ulong HopoFrequency => m_hopo_frequency;
        public byte MultiplierNote => m_multiplier_note;
        public ulong SustainCutoffThreshold => m_sustain_cutoff_threshold;
        public bool EightNoteHopo => m_eighthnote_hopo;

        public string Directory => Path.GetDirectoryName(m_chartFile.Item1)!;

        public SongEntry((string, ChartType) chartPath, DateTime chartLastWrite)
        {
            m_chartFile = chartPath;
            m_chartWriteTime = chartLastWrite;
            m_directory_playlist.Str = Path.GetDirectoryName(Directory)!;
        }

        public ScanValues GetValues(NoteTrackType track)
        {
            switch (track)
            {
                case NoteTrackType.Lead:         return m_scans.lead_5;
                case NoteTrackType.Lead_6:       return m_scans.lead_6;
                case NoteTrackType.Bass:         return m_scans.lead_5;
                case NoteTrackType.Bass_6:       return m_scans.lead_6;
                case NoteTrackType.Rhythm:       return m_scans.rhythm;
                case NoteTrackType.Coop:         return m_scans.coop;
                case NoteTrackType.Keys:         return m_scans.keys;
                case NoteTrackType.Drums_4:      return m_scans.drums_4;
                case NoteTrackType.Drums_4Pro:   return m_scans.drums_4pro;
                case NoteTrackType.Drums_5:      return m_scans.drums_5;
                case NoteTrackType.Vocals:       return m_scans.leadVocals;
                case NoteTrackType.Harmonies:    return m_scans.harmonyVocals;
                case NoteTrackType.ProGuitar_17: return m_scans.proguitar_17;
                case NoteTrackType.ProGuitar_22: return m_scans.proguitar_22;
                case NoteTrackType.ProBass_17:   return m_scans.probass_17;
                case NoteTrackType.ProBass_22:   return m_scans.probass_22;
                default:
                    throw new ArgumentException("track value is not of a valid type");
            }
        }

        public void Load_Ini(string iniPath, DateTime iniLastWrite)
        {
            m_modifiers = IniHandler.ReadSongIniFile(iniPath);
            m_iniWriteTime = iniLastWrite;
        }

        public bool Scan(out FrameworkFile? file)
        {
            try
            {
                file = new(m_chartFile.Item1);
                if (m_chartFile.Item2 == ChartType.CHART)
                    Scan_Chart(file);

                if (GetModifier("name") == null)
                    return false;

                if (m_chartFile.Item2 == ChartType.MID)
                    Scan_Midi(file);
                return m_scans.CheckForValidScans();
            }
            catch (Exception)
            {
                file = null;
                return false;
            }
        }

        public Modifier? GetModifier(string name)
        {
            if (m_modifiers.TryGetValue(name, out List<Modifier>? nameMods))
                return nameMods[0];
            return null;
        }

        public void FinishScan()
        {
            MapModifierVariables();
        }

        private void Scan_Midi(FrameworkFile file)
        {
            MidiFileReader reader = new(file);
            DrumType drumType = GetDrumTypeFromModifier();
            while (reader.StartTrack())
            {
                if (reader.GetTrackNumber() > 1 && reader.GetEvent().type == MidiEventType.Text_TrackName)
                {
                    string name = Encoding.ASCII.GetString(reader.ExtractTextOrSysEx());
                    if (MidiFileReader.TRACKNAMES.TryGetValue(name, out MidiTrackType type) && type != MidiTrackType.Events && type != MidiTrackType.Beats)
                        m_scans.ScanFromMidi(type, drumType, ref reader);
                }
            }
        }

        private void Scan_Chart(FrameworkFile file)
        {
            ChartFileReader reader = new(file);
            if (!reader.ValidateHeaderTrack())
                throw new Exception("[Song] track expected at the start of the file");
            // Add [Song] parsing later
            reader.SkipTrack();

            LegacyDrumScan legacy = new(GetDrumTypeFromModifier());
            while (reader.IsStartOfTrack())
            {
                if (!reader.ValidateDifficulty() || !reader.ValidateInstrument() || !m_scans.ScanFromDotChart(ref legacy, ref reader))
                    reader.SkipTrack();
            }

            if (legacy.Type == DrumType.FIVE_LANE)
                m_scans.drums_5 |= legacy.Values;
            else
                m_scans.drums_4pro |= legacy.Values;
        }

        private void MapModifierVariables()
        {
            if (m_modifiers.TryGetValue("name", out List<Modifier>? names))
            {
                for (int i = 0; i < names.Count; ++i)
                {
                    m_name = names[i].SORTSTR;
                    if (m_name.Str != "Unknown Title")
                        break;
                }
            }

            if (m_modifiers.TryGetValue("artist", out List<Modifier>? artists))
            {
                for (int i = 0; i < artists.Count; ++i)
                {
                    m_artist = artists[i].SORTSTR;
                    if (m_artist.Str != "Unknown Artist")
                        break; 
                }
            }
            else
                m_artist = s_DEFAULT_ARTIST;

            if (m_modifiers.TryGetValue("album", out List<Modifier>? albums))
            {
                for (int i = 0; i < albums.Count; ++i)
                {
                    m_album = albums[i].SORTSTR;
                    if (m_album.Str != "Unknown Album")
                        break;
                }
            }
            else
                m_album = s_DEFAULT_ALBUM;

            if (m_modifiers.TryGetValue("genre", out List<Modifier>? genres))
            {
                for (int i = 0; i < genres.Count; ++i)
                {
                    m_genre = genres[i].SORTSTR;
                    if (m_genre.Str != "Unknown Genre")
                        break;
                }
            }
            else
                m_genre = s_DEFAULT_GENRE;

            if (m_modifiers.TryGetValue("year", out List<Modifier>? years))
            {
                for (int i = 0; i < years.Count; ++i)
                {
                    m_year = years[i].SORTSTR;
                    if (m_year.Str != "Unknown Year")
                        break;
                }
            }
            else
                m_year = s_DEFAULT_YEAR;

            if (m_modifiers.TryGetValue("charter", out List<Modifier>? charters))
            {
                for (int i = 0; i < charters.Count; ++i)
                {
                    m_charter = charters[i].SORTSTR;
                    if (m_charter.Str != "Unknown Charter")
                        break;
                }
            }
            else
                m_charter = s_DEFAULT_CHARTER;

            if (m_modifiers.TryGetValue("playlist", out List<Modifier>? playlists))
            {
                for (int i = 0; i < playlists.Count; ++i)
                {
                    m_playlist = playlists[i].SORTSTR;
                    if (m_playlist.Str != m_directory_playlist.Str)
                        break;
                }
            }
            else
                m_playlist = m_directory_playlist;

            if (m_modifiers.TryGetValue("song_length", out List<Modifier>? songLengths))
                m_song_length = songLengths[0].UINT64;

            if (m_modifiers.TryGetValue("preview_start_time", out List<Modifier>? preview_start))
                m_previewStart = preview_start[0].FLOAT;

            if (m_modifiers.TryGetValue("preview_end_time", out List<Modifier>? preview_end))
                m_previewEnd = preview_end[0].FLOAT;

            if (m_modifiers.TryGetValue("album_track", out List<Modifier>? album_track))
                m_album_track = album_track[0].UINT16;

            if (m_modifiers.TryGetValue("playlist_track", out List<Modifier>? playlist_track))
                m_playlist_track = playlist_track[0].UINT16;

            if (m_modifiers.TryGetValue("icon", out List<Modifier>? icon))
                m_icon = icon[0].STR;

            if (m_modifiers.TryGetValue("source", out List<Modifier>? source))
                m_source = source[0].STR;

            if (m_modifiers.TryGetValue("hopo_frequency", out List<Modifier>? hopo_freq))
                m_hopo_frequency = hopo_freq[0].UINT64;

            if (m_modifiers.TryGetValue("multiplier_note", out List<Modifier>? multiplier))
                if (multiplier[0].UINT16 == 103)
                    m_multiplier_note = 103;

            if (m_modifiers.TryGetValue("eighthnote_hopo", out List<Modifier>? eighthnote))
                m_eighthnote_hopo = eighthnote[0].BOOL;

            if (m_modifiers.TryGetValue("sustain_cutoff_threshold", out List<Modifier>? threshold))
                m_sustain_cutoff_threshold = threshold[0].UINT64;

            if (m_modifiers.TryGetValue("hopofreq", out List<Modifier>? hopofreq_old))
                m_hopofreq_Old = hopofreq_old[0].UINT16;

            {
                if (m_modifiers.TryGetValue("diff_guitar", out List<Modifier>? intensities))
                    m_scans.lead_5.intensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_guitarghl", out List<Modifier>? intensities))
                    m_scans.lead_6.intensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_bass", out List<Modifier>? intensities))
                    m_scans.bass_5.intensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_bassghl", out List<Modifier>? intensities))
                    m_scans.bass_6.intensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_rhythm", out List<Modifier>? intensities))
                    m_scans.rhythm.intensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_guitar_coop", out List<Modifier>? intensities))
                    m_scans.coop.intensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_keys", out List<Modifier>? intensities))
                    m_scans.keys.intensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_drums", out List<Modifier>? intensities))
	            {
                    sbyte intensity = (sbyte)intensities[0].INT32;
                    m_scans.drums_4.intensity = intensity;
                    m_scans.drums_4pro.intensity = intensity;
                    m_scans.drums_5.intensity = intensity;
                }
            }

            {
                if (m_modifiers.TryGetValue("diff_drums_real", out List<Modifier>? intensities))
                    m_scans.drums_4pro.intensity = (sbyte)intensities[0].INT32;
            }

	        m_scans.drums_4 = m_scans.drums_4pro;

            {
                if (m_modifiers.TryGetValue("pro_drums", out List<Modifier>? proDrums) && !proDrums[0].BOOL)
                    m_scans.drums_4pro.subTracks = 0;
            }
	        

            {
                if (m_modifiers.TryGetValue("diff_guitar_real", out List<Modifier>? intensities))
                    m_scans.proguitar_17.intensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_guitar_real_22", out List<Modifier>? intensities))
                    m_scans.proguitar_22.intensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_bass_real", out List<Modifier>? intensities))
                    m_scans.probass_17.intensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_bass_real_22", out List<Modifier>? intensities))
                    m_scans.probass_22.intensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_vocals", out List<Modifier>? intensities))
                    m_scans.leadVocals.intensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_vocals_harm", out List<Modifier>? intensities))
                    m_scans.harmonyVocals.intensity = (sbyte)intensities[0].INT32;
            }
        }

        

        internal static Modifier? nullMod = null;

        private ref Modifier? GetModifier_internal(string name)
        {
            if (m_modifiers.TryGetValue(name, out List<Modifier>? nameMods))
            {
                var span = CollectionsMarshal.AsSpan(nameMods);
                return ref span[0]!;
            }
            return ref nullMod;
        }

        private DrumType GetDrumTypeFromModifier()
        {
            if (m_modifiers.TryGetValue("five_lane_drums", out List<Modifier>? fivelanes))
                return fivelanes[0].BOOL ? DrumType.FIVE_LANE : DrumType.FOUR_PRO;
            return DrumType.UNKNOWN;
        }

        private string GetDebuggerDisplay()
        {
            return $"{Artist.Str} | {Name.Str}";
        }
    }
}
