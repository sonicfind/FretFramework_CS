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

        protected SongEntry() { }

        protected SongEntry(BinaryReader reader)
        {
            m_scans.ReadFromCache(reader);
            m_name.Str = reader.ReadString();
            m_artist.Str = reader.ReadString();
            m_album.Str = reader.ReadString();
            m_genre.Str = reader.ReadString();
            m_year.Str = reader.ReadString();
            m_charter.Str = reader.ReadString();
            m_playlist.Str = reader.ReadString();
            
            m_previewStart   = reader.ReadSingle();
            m_previewEnd     = reader.ReadSingle();
            m_album_track    = reader.ReadUInt16();
            m_playlist_track = reader.ReadUInt16();
            m_song_length    = reader.ReadUInt64();
            m_icon           = reader.ReadString();
            m_source         = reader.ReadString();
            m_hopo_frequency = reader.ReadUInt64();
            VocalParts       = reader.ReadInt32();
            IsMaster         = reader.ReadBoolean();
        }

        protected void FormatCacheData(BinaryWriter writer)
        {
            m_scans.WriteToCache(writer);

            writer.Write(m_name.Str);
            writer.Write(m_artist.Str);
            writer.Write(m_album.Str);
            writer.Write(m_genre.Str);
            writer.Write(m_year.Str);
            writer.Write(m_charter.Str);
            writer.Write(m_playlist.Str);

            writer.Write(m_previewStart);
            writer.Write(m_previewEnd);
            writer.Write(m_album_track);
            writer.Write(m_playlist_track);
            writer.Write(m_song_length);
            writer.Write(m_icon);
            writer.Write(m_source);
            writer.Write(m_hopo_frequency);
            writer.Write(VocalParts);
            writer.Write(IsMaster);
        }

        private string GetDebuggerDisplay()
        {
            return $"{Artist.Str} | {Name.Str}";
        }
    }
}
