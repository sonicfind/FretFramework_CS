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
    public abstract class SongEntry
    {
        protected SortString m_name = new();
        protected SortString m_artist = new();
        protected SortString m_album = new();
        protected SortString m_genre = new();
        protected SortString m_year = new();
        protected SortString m_charter = new();
        protected SortString m_playlist = new();

        protected ulong m_song_length = 0;
        protected float m_previewStart = 0.0f;
        protected float m_previewEnd = 0.0f;
        protected ushort m_album_track = ushort.MaxValue;
        protected ushort m_playlist_track = ushort.MaxValue;
        protected string m_icon = string.Empty;
        protected string m_source = string.Empty;

        protected ulong m_hopo_frequency = 0;

        protected TrackScans m_scans = new();

        public SortString Artist => m_artist;
        public SortString Name => m_name;
        public SortString Album => m_album;
        public SortString Genre => m_genre;
        public SortString Year => m_year;
        public SortString Charter => m_charter;
        public SortString Playlist => m_playlist;
        public ulong SongLength => m_song_length;
        public ulong HopoFrequency => m_hopo_frequency;
        public bool IsMaster { get; protected set; }
        public int VocalParts { get; protected set; }

        public abstract string Directory { get; protected set; }

        public ScanValues GetValues(NoteTrackType track)
        {
            return track switch
            {
                NoteTrackType.Lead         => m_scans.lead_5,
                NoteTrackType.Lead_6       => m_scans.lead_6,
                NoteTrackType.Bass         => m_scans.lead_5,
                NoteTrackType.Bass_6       => m_scans.lead_6,
                NoteTrackType.Rhythm       => m_scans.rhythm,
                NoteTrackType.Coop         => m_scans.coop,
                NoteTrackType.Keys         => m_scans.keys,
                NoteTrackType.Drums_4      => m_scans.drums_4,
                NoteTrackType.Drums_4Pro   => m_scans.drums_4pro,
                NoteTrackType.Drums_5      => m_scans.drums_5,
                NoteTrackType.Vocals       => m_scans.leadVocals,
                NoteTrackType.Harmonies    => m_scans.harmonyVocals,
                NoteTrackType.ProGuitar_17 => m_scans.proguitar_17,
                NoteTrackType.ProGuitar_22 => m_scans.proguitar_22,
                NoteTrackType.ProBass_17   => m_scans.probass_17,
                NoteTrackType.ProBass_22   => m_scans.probass_22,
                _ => throw new ArgumentException("track value is not of a valid type"),
            };
        }

        public abstract void FinishScan();

        protected void Scan_Midi(FrameworkFile file, DrumType drumType)
        {
            using MidiFileReader reader = new(file);
            while (reader.StartTrack())
            {
                if (reader.GetTrackNumber() > 1 && reader.GetEvent().type == MidiEventType.Text_TrackName)
                {
                    string name = Encoding.ASCII.GetString(reader.ExtractTextOrSysEx());
                    if (MidiFileReader.TRACKNAMES.TryGetValue(name, out MidiTrackType type) && type != MidiTrackType.Events && type != MidiTrackType.Beats)
                        m_scans.ScanFromMidi(type, drumType, reader);
                }
            }
        }

        private string GetDebuggerDisplay()
        {
            return $"{Artist.Str} | {Name.Str}";
        }
    }
}
