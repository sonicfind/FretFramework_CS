using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Disassemblers;
using Framework.Hashes;
using Framework.Serialization;
using Framework.SongEntry.TrackScan;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Framework.SongEntry
{
    public class CONSongFileInfo
    {
        private readonly string _fullname;
        private readonly DateTime _lastWrite;

        public CONSongFileInfo(string file) : this(new FileInfo(file)) {}

        public CONSongFileInfo(FileInfo info)
        {
            _fullname = info.FullName;
            _lastWrite = info.LastWriteTime;
        }

        public static implicit operator CONSongFileInfo(FileInfo info) => new(info);

        public string FullName => _fullname;
        public DateTime LastWriteTime => _lastWrite;
    }

    public class ConSongEntry : SongEntry
    {
        internal static readonly float[,] emptyRatios = new float[0, 0];
        static ConSongEntry() { }

        private CONFile? conFile;
        private readonly FileListing? midiListing;
        private FileListing? moggListing;
        private FileListing? miloListing;
        private FileListing? imgListing;
        public string SongID { get; private set; } = string.Empty;
        public uint AnimTempo { get; private set; }
        public string DrumBank { get; private set; } = string.Empty;
        public string VocalPercussionBank { get; private set; } = string.Empty;
        public uint VocalSongScrollSpeed { get; private set; }
        public uint SongRating { get; private set; } // 1 = FF; 2 = SR; 3 = M; 4 = NR
        public bool VocalGender { get; private set; } = true;//true for male, false for female
        public bool HasAlbumArt { get; private set; }
        public bool IsFake { get; private set; }
        public uint VocalTonicNote { get; private set; }
        public bool SongTonality { get; private set; } // 0 = major, 1 = minor
        public int TuningOffsetCents { get; private set; }

        public Encoding MidiEncoding { get; private set; } = Encoding.Latin1;

        public string MidiPath { get; private set; } = string.Empty;
        public DateTime MidiLastWrite { get; private set; }

        // _update.mid info, if it exists
        public CONSongFileInfo? UpdateMidi { get; private set; } = null;

        // .mogg info
        public CONSongFileInfo? Mogg { get; private set; } = null;

        // .milo info
        public CONSongFileInfo? Milo { get; private set; } = null;
        public uint VenueVersion { get; private set; }

        // image info
        public CONSongFileInfo? Image { get; private set; } = null;

        private string location = string.Empty;


        public SongProUpgrade? Upgrade { get; set; }

        public string[] Soloes { get; private set; } = Array.Empty<string>();
        public string[] VideoVenues { get; private set; } = Array.Empty<string>();

        public short[] RealGuitarTuning { get; private set; } = Array.Empty<short>();
        public short[] RealBassTuning { get; private set; } = Array.Empty<short>();

        public ushort[] DrumIndices { get; private set; } = Array.Empty<ushort>();
        public ushort[] BassIndices { get; private set; } = Array.Empty<ushort>();
        public ushort[] GuitarIndices { get; private set; } = Array.Empty<ushort>();
        public ushort[] KeysIndices { get; private set; } = Array.Empty<ushort>();
        public ushort[] VocalsIndices { get; private set; } = Array.Empty<ushort>();
        public ushort[] CrowdIndices { get; private set; } = Array.Empty<ushort>();

        public float[] Pan { get; private set; } = Array.Empty<float>();
        public float[] Volume { get; private set; } = Array.Empty<float>();
        public float[] Core { get; private set; } = Array.Empty<float>();

        public ConSongEntry(CONFile file, string nodeName, FileListing? midi, FileListing? moggListing, FileInfo? moggInfo, FileInfo? updateInfo, BinaryReader reader) : base(reader)
        {
            conFile = file;
            midiListing = midi;
            if (moggListing != null)
                this.moggListing = moggListing;
            else if (moggInfo != null)
                Mogg = moggInfo;

            if (updateInfo != null)
                UpdateMidi = updateInfo;

            if (midiListing != null && !midiListing.Filename.StartsWith($"songs/{nodeName}"))
                nodeName = conFile[midiListing.PathIndex].Filename.Split('/')[1];

            string genPAth = $"songs/{nodeName}/gen/{nodeName}";
            if (reader.ReadBoolean())
                miloListing = conFile[genPAth + ".milo_xbox"];
            else
            {
                string milopath = reader.ReadString();
                if (milopath != string.Empty)
                {
                    FileInfo info = new(milopath);
                    if (info.Exists)
                        Milo = info;
                }
            }

            if (reader.ReadBoolean())
                imgListing = conFile[genPAth + "_keep.png_xbox"];
            else
            {
                string imgpath = reader.ReadString();
                if (imgpath != string.Empty)
                {
                    FileInfo info = new(imgpath);
                    if (info.Exists)
                        Image = info;
                }
            }

            FinishCacheRead(reader);
        }

        public ConSongEntry(FileInfo midi, FileInfo mogg, FileInfo? updateInfo, BinaryReader reader) : base(reader)
        {
            MidiPath = midi.FullName;
            MidiLastWrite = midi.LastWriteTime;

            Mogg = mogg;

            if (updateInfo != null)
                UpdateMidi = updateInfo;

            Milo = new(reader.ReadString());
            Image = new(reader.ReadString());
            FinishCacheRead(reader);
        }

        private void FinishCacheRead(BinaryReader reader)
        {
            AnimTempo = reader.ReadUInt32();
            SongID = reader.ReadString();
            VocalPercussionBank = reader.ReadString();
            VocalSongScrollSpeed = reader.ReadUInt32();
            SongRating = reader.ReadUInt32();
            VocalGender = reader.ReadBoolean();
            VocalTonicNote = reader.ReadUInt32();
            SongTonality = reader.ReadBoolean();
            TuningOffsetCents = reader.ReadInt32();
            VenueVersion = reader.ReadUInt32();

            RealGuitarTuning =  ReadShortArray(reader);
            RealBassTuning = ReadShortArray(reader);

            Pan = ReadFloatArray(reader);
            Volume = ReadFloatArray(reader);
            Core = ReadFloatArray(reader);

            DrumIndices = ReadUShortArray(reader);
            BassIndices = ReadUShortArray(reader);
            GuitarIndices = ReadUShortArray(reader);
            KeysIndices = ReadUShortArray(reader);
            VocalsIndices = ReadUShortArray(reader);
            CrowdIndices = ReadUShortArray(reader);
        }

        private static short[] ReadShortArray(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length == 0)
                return Array.Empty<short>();

            short[] values = new short[length];
            for (int i = 0; i < length; ++i)
                values[i] = reader.ReadInt16();
            return values;
        }

        private static ushort[] ReadUShortArray(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length == 0)
                return Array.Empty<ushort>();

            ushort[] values = new ushort[length];
            for (int i = 0; i < length; ++i)
                values[i] = reader.ReadUInt16();
            return values;
        }

        private static float[] ReadFloatArray(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length == 0)
                return Array.Empty<float>();

            float[] values = new float[length];
            for (int i = 0; i < length; ++i)
                values[i] = reader.ReadSingle();
            return values;
        }

        public ConSongEntry(CONFile conFile, string nodeName, DTAFileReader reader, ushort nodeIndex)
        {
            this.conFile = conFile;
            SetFromDTA(nodeName, reader);

            if (MidiPath == string.Empty)
                MidiPath = location + ".mid";

            midiListing = conFile[MidiPath];
            if (midiListing == null)
                throw new Exception($"Required midi file '{MidiPath}' was not located");
            moggListing = conFile[location + ".mogg"];

            if (!location.StartsWith($"songs/{nodeName}"))
                nodeName = conFile[midiListing.PathIndex].Filename.Split('/')[1];

            string genPAth = $"songs/{nodeName}/gen/{nodeName}";
            miloListing = conFile[genPAth + ".milo_xbox"];
            imgListing =  conFile[genPAth + "_keep.png_xbox"];

            if (m_playlist.Str == string.Empty)
                m_playlist = conFile.Filename;
            m_playlist_track = nodeIndex;
        }

        public ConSongEntry(string folder, string nodeName, DTAFileReader reader, ushort nodeIndex)
        {
            SetFromDTA(nodeName, reader);
            
            string file = Path.Combine(folder, location);

            if (MidiPath == string.Empty)
                MidiPath = file + ".mid";

            FileInfo midiInfo = new(MidiPath);
            if (!midiInfo.Exists)
                throw new Exception($"Required midi file '{MidiPath}' was not located");
            MidiLastWrite = midiInfo.LastWriteTime;

            Mogg = new(file + ".mogg");

            if (!location.StartsWith($"songs/{nodeName}"))
                nodeName = location.Split('/')[1];

            file = Path.Combine(folder, $"songs/{nodeName}/gen/{nodeName}");
            Milo = new(file + ".milo_xbox");
            Image = new(file + "_keep.png_xbox");

            if (m_playlist.Str == string.Empty)
                m_playlist = folder;
            m_playlist_track = nodeIndex;
        }

        public (bool, bool) SetFromDTA(string nodeName, DTAFileReader reader)
        {
            bool alternatePath = false;
            bool discUpdate = false;
            while (reader.StartNode())
            {
                string name = reader.GetNameOfNode();
                switch (name)
                {
                    case "name": m_name = reader.ExtractText(); break;
                    case "artist": m_artist = reader.ExtractText(); break;
                    case "master": IsMaster = reader.ReadBoolean(); break;
                    case "context": /*Context = reader.ReadUInt32();*/ break;
                    case "song": SongLoop(ref reader); break;
                    case "song_vocals": while (reader.StartNode()) reader.EndNode(); break;
                    case "song_scroll_speed": VocalSongScrollSpeed = reader.ReadUInt32(); break;
                    case "tuning_offset_cents": TuningOffsetCents = reader.ReadInt32(); break;
                    case "bank": VocalPercussionBank = reader.ExtractText(); break;
                    case "anim_tempo":
                        {
                            string val = reader.ExtractText();
                            AnimTempo = val switch
                            {
                                "kTempoSlow" => 16,
                                "kTempoMedium" => 32,
                                "kTempoFast" => 64,
                                _ => uint.Parse(val)
                            };
                            break;
                        }
                    case "preview":
                        m_previewStart = reader.ReadUInt32();
                        m_previewEnd = reader.ReadUInt32();
                        break;
                    case "rank": RankLoop(ref reader); break;
                    case "solo": Soloes = reader.ExtractList_String().ToArray(); break;
                    case "genre": m_genre = reader.ExtractText(); break;
                    case "decade": /*Decade = reader.ExtractText();*/ break;
                    case "vocal_gender": VocalGender = reader.ExtractText() == "male"; break;
                    case "format": /*Format = reader.ReadUInt32();*/ break;
                    case "version": VenueVersion = reader.ReadUInt32(); break;
                    case "fake": /*IsFake = reader.ExtractText();*/ break;
                    case "downloaded": /*Downloaded = reader.ExtractText();*/ break;
                    case "game_origin":
                        {
                            m_source = reader.ExtractText();
                            if ((m_source == "ugc" || m_source == "ugc_plus"))
                            {
                                if (!nodeName.StartsWith("UGC_"))
                                    m_source = "customs";
                            }
                            else if (m_source == "rb1" || m_source == "rb1_dlc" || m_source == "rb1dlc" ||
                                m_source == "gdrb" || m_source == "greenday" || m_source == "beatles" ||
                                m_source == "tbrb" || m_source == "lego" || m_source == "lrb" ||
                                m_source == "rb2" || m_source == "rb3" || m_source == "rb3_dlc" || m_source == "rb3dlc")
                            {
                                m_source = "Harmonix";
                            }
                            break;
                        }
                    case "song_id": SongID = reader.ExtractText(); break;
                    case "rating": SongRating = reader.ReadUInt32(); break;
                    case "short_version": /*ShortVersion = reader.ReadUInt32();*/ break;
                    case "album_art": HasAlbumArt = reader.ReadBoolean(); break;
                    case "year_released": m_year = reader.ReadUInt32().ToString(); break;
                    case "year_recorded": m_year = reader.ReadUInt32().ToString(); break;
                    case "album_name": m_album = reader.ExtractText(); break;
                    case "album_track_number": m_album_track = reader.ReadUInt16(); break;
                    case "pack_name": m_playlist = reader.ExtractText(); break;
                    case "base_points": /*BasePoints = reader.ReadUInt32();*/ break;
                    case "band_fail_cue": /*BandFailCue = reader.ExtractText();*/ break;
                    case "drum_bank": DrumBank = reader.ExtractText(); break;
                    case "song_length": m_song_length = reader.ReadUInt32(); break;
                    case "sub_genre": /*Subgenre = reader.ExtractText();*/ break;
                    case "author": m_charter = reader.ExtractText(); break;
                    case "guide_pitch_volume": /*GuidePitchVolume = reader.ReadFloat();*/ break;
                    case "encoding":
                        MidiEncoding = reader.ExtractText() switch
                        {
                            "Latin1" => Encoding.Latin1,
                            "UTF8" => Encoding.UTF8,
                            _ => MidiEncoding
                        };
                        break;
                    case "vocal_tonic_note": VocalTonicNote = reader.ReadUInt32(); break;
                    case "song_tonality": SongTonality = reader.ReadBoolean(); break;
                    case "alternate_path": alternatePath = reader.ReadBoolean(); break;
                    case "real_guitar_tuning":
                        {
                            if (reader.StartNode())
                            {
                                RealGuitarTuning = reader.ExtractList_Int16().ToArray();
                                reader.EndNode();
                            }
                            break;
                        }
                    case "real_bass_tuning":
                        {
                            if (reader.StartNode())
                            {
                                RealBassTuning = reader.ExtractList_Int16().ToArray();
                                reader.EndNode();
                            }
                            break;
                        }
                    case "video_venues":
                        {
                            if (reader.StartNode())
                            {
                                VideoVenues = reader.ExtractList_String().ToArray();
                                reader.EndNode();
                            }
                            break;
                        }
                    case "extra_authoring":
                        foreach (string str in reader.ExtractList_String())
                        {
                            if (str == "disc_update")
                            {
                                discUpdate = true;
                                break;
                            }
                        }
                        break;
                }
                reader.EndNode();
            }

            return new(discUpdate, alternatePath);
        }

        private void SongLoop(ref DTAFileReader reader)
        {
            while (reader.StartNode())
            {
                string descriptor = reader.GetNameOfNode();
                switch (descriptor)
                {
                    case "name": location = reader.ExtractText(); break;
                    case "tracks": TracksLoop(ref reader); break;
                    case "crowd_channels": CrowdIndices = reader.ExtractList_UInt16().ToArray(); break;
                    case "vocal_parts": VocalParts = reader.ReadUInt16(); break;
                    case "pans":
                        if (reader.StartNode())
                        {
                            Pan = reader.ExtractList_Float().ToArray();
                            reader.EndNode();
                        }
                        break;
                    case "vols":
                        if (reader.StartNode())
                        {
                            Volume = reader.ExtractList_Float().ToArray();
                            reader.EndNode();
                        }
                        break;
                    case "cores":
                        if (reader.StartNode())
                        {
                            Core = reader.ExtractList_Float().ToArray();
                            reader.EndNode();
                        }
                        break;
                    case "hopo_threshold": m_hopo_frequency = reader.ReadUInt32(); break;
                    case "midi_file": MidiPath = reader.ExtractText(); break;
                }
                reader.EndNode();
            }
        }

        private void TracksLoop(ref DTAFileReader reader)
        {
            while (reader.StartNode())
            {
                while (reader.StartNode())
                {
                    switch (reader.GetNameOfNode())
                    {
                        case "drum":
                            {
                                if (reader.StartNode())
                                {
                                    DrumIndices = reader.ExtractList_UInt16().ToArray();
                                    reader.EndNode();
                                }
                                break;
                            }
                        case "bass":
                            {
                                if (reader.StartNode())
                                {
                                    BassIndices = reader.ExtractList_UInt16().ToArray();
                                    reader.EndNode();
                                }
                                break;
                            }
                        case "guitar":
                            {
                                if (reader.StartNode())
                                {
                                    GuitarIndices = reader.ExtractList_UInt16().ToArray();
                                    reader.EndNode();
                                }
                                break;
                            }
                        case "keys":
                            {
                                if (reader.StartNode())
                                {
                                    KeysIndices = reader.ExtractList_UInt16().ToArray();
                                    reader.EndNode();
                                }
                                break;
                            }
                        case "vocals":
                            {
                                if (reader.StartNode())
                                {
                                    VocalsIndices = reader.ExtractList_UInt16().ToArray();
                                    reader.EndNode();
                                }
                                break;
                            }
                    }
                    reader.EndNode();
                }
                reader.EndNode();
            }
        }

        private static readonly int[] BandDiffMap = { 163, 215, 243, 267, 292, 345 };
        private static readonly int[] GuitarDiffMap = { 139, 176, 221, 267, 333, 409 };
        private static readonly int[] BassDiffMap = { 135, 181, 228, 293, 364, 436 };
        private static readonly int[] DrumDiffMap = { 124, 151, 178, 242, 345, 448 };
        private static readonly int[] KeysDiffMap = { 153, 211, 269, 327, 385, 443 };
        private static readonly int[] VocalsDiffMap = { 132, 175, 218, 279, 353, 427 };
        private static readonly int[] RealGuitarDiffMap = { 150, 205, 264, 323, 382, 442 };
        private static readonly int[] RealBassDiffMap = { 150, 208, 267, 325, 384, 442 };
        private static readonly int[] RealDrumsDiffMap = { 124, 151, 178, 242, 345, 448 };
        private static readonly int[] RealKeysDiffMap = { 153, 211, 269, 327, 385, 443 };
        private static readonly int[] HarmonyDiffMap = { 132, 175, 218, 279, 353, 427 };

        private void RankLoop(ref DTAFileReader reader)
        {
            while (reader.StartNode())
            {
                switch (reader.GetNameOfNode())
                {
                    case "drum":
                    case "drums":
                        {
                            SetRank(ref m_scans.drums_4.intensity, reader.ReadUInt16(), DrumDiffMap);
                            if (m_scans.drums_4pro.intensity == -1)
                                m_scans.drums_4pro.intensity = m_scans.drums_4.intensity;
                            break;
                        }
                    case "guitar": SetRank(ref m_scans.lead_5.intensity, reader.ReadUInt16(), GuitarDiffMap); break;
                    case "bass": SetRank(ref m_scans.bass_5.intensity, reader.ReadUInt16(), BassDiffMap); break;
                    case "vocals": SetRank(ref m_scans.leadVocals.intensity, reader.ReadUInt16(), VocalsDiffMap); break;
                    case "keys": SetRank(ref m_scans.keys.intensity, reader.ReadUInt16(), KeysDiffMap); break;
                    case "realGuitar":
                    case "real_guitar":
                        {
                            SetRank(ref m_scans.proguitar_17.intensity, reader.ReadUInt16(), RealGuitarDiffMap);
                            m_scans.proguitar_22.intensity = m_scans.proguitar_17.intensity;
                            break;
                        }
                    case "realBass":
                    case "real_bass":
                        {
                            SetRank(ref m_scans.probass_17.intensity, reader.ReadUInt16(), RealBassDiffMap);
                            m_scans.probass_22.intensity = m_scans.probass_17.intensity;
                            break;
                        }
                    case "realKeys":
                    case "real_keys": SetRank(ref m_scans.proKeys.intensity, reader.ReadUInt16(), RealKeysDiffMap); break;
                    case "realDrums":
                    case "real_drums":
                        {
                            SetRank(ref m_scans.drums_4pro.intensity, reader.ReadUInt16(), RealDrumsDiffMap);
                            if (m_scans.drums_4.intensity == -1)
                                m_scans.drums_4.intensity = m_scans.drums_4pro.intensity;
                            break;
                        }
                    case "harmVocals":
                    case "vocal_harm": SetRank(ref m_scans.harmonyVocals.intensity, reader.ReadUInt16(), HarmonyDiffMap); break;
                    case "band": SetRank(ref m_bandIntensity, reader.ReadUInt16(), BandDiffMap); break;
                }
                reader.EndNode();
            }
        }

        private static void SetRank(ref sbyte intensity, ushort rank, int[] values)
        {
            sbyte i = 6;
            while (i > 0 && rank < values[i - 1])
                --i;
            intensity = i;
        }

        public unsafe bool Scan(out SHA1Wrapper? hash, string nodeName)
        {
            hash = null;

            if (Mogg == null && moggListing == null)
            {
                Debug.WriteLine($"{nodeName} - Mogg not defined");
                return false;
            }

            if (!IsMoggUnencrypted())
            {
                Debug.WriteLine($"{nodeName} - Mogg encrypted");
                return false;
            }

            try
            {
                using FrameworkFile? file = LoadMidiFile();
                using FrameworkFile? updateFile = LoadMidiUpdateFile();
                using FrameworkFile? upgradeFile = Upgrade?.GetUpgradeMidi();

                int bufLength = 0;
                m_scans = Scan_Midi(file);
                bufLength += file!.Length;

                if (UpdateMidi != null)
                {
                    m_scans.Update(Scan_Midi(updateFile));
                    bufLength += updateFile!.Length;
                }

                if (Upgrade != null)
                {
                    m_scans.Update(Scan_Midi(upgradeFile));
                    bufLength += upgradeFile!.Length;
                }

                using PointerHandler buffer = new(bufLength);
                Copier.MemCpy(buffer.Data, file.ptr, (nuint)file.Length);
                int offset = file.Length;
                if (UpdateMidi != null)
                {
                    Copier.MemCpy(buffer.Data + offset, updateFile!.ptr, (nuint)updateFile.Length);
                    offset += updateFile!.Length;
                }

                if (Upgrade != null)
                {
                    Copier.MemCpy(buffer.Data + offset, upgradeFile!.ptr, (nuint)upgradeFile.Length);
                    offset += upgradeFile!.Length;
                }

                hash = new(buffer.CalcSHA1());
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Update(string folder, string nodeName, DTAFileReader reader)
        {
            var results = SetFromDTA(nodeName, reader);

            string dir = Path.Combine(folder, nodeName);
            FileInfo info;
            if (results.Item1)
            {
                string path = Path.Combine(dir, $"{nodeName}_update.mid");
                info = new(path);
                if (info.Exists)
                {
                    if (UpdateMidi == null || UpdateMidi.LastWriteTime < info.LastWriteTime)
                        UpdateMidi = info;
                }
                else if (UpdateMidi == null)
                    Debug.WriteLine($"Couldn't update song {nodeName} - update file {path} not found!");
            }

            info = new(Path.Combine(dir, $"{nodeName}_update.mogg"));
            if (info.Exists && (Mogg == null || Mogg.LastWriteTime < info.LastWriteTime))
                Mogg = info;

            dir = Path.Combine(dir, "gen");

            info = new(Path.Combine(dir, $"{nodeName}.milo_xbox"));
            if (info.Exists && (Milo == null || Milo.LastWriteTime < info.LastWriteTime))
                Milo = info;

            if (HasAlbumArt && results.Item2)
            {
                info = new(Path.Combine(dir, $"{nodeName}_keep.png_xbox"));
                if (info.Exists && (Image == null || Image.LastWriteTime < info.LastWriteTime))
                    Image = info;
            }
        }

        public byte[] FormatCacheData()
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            if (conFile != null)
            {
                writer.Write(midiListing!.Filename);
                writer.Write(midiListing.LastWrite);

                if (Mogg == null)
                {
                    writer.Write(true);
                    writer.Write(moggListing!.Filename);
                    writer.Write(moggListing.LastWrite);
                }
                else
                {
                    writer.Write(false);
                    writer.Write(Mogg.FullName);
                    writer.Write(Mogg.LastWriteTime.ToBinary());
                }
            }
            else
            {
                writer.Write(MidiPath);
                writer.Write(MidiLastWrite.ToBinary());
                writer.Write(Mogg!.FullName);
                writer.Write(Mogg.LastWriteTime.ToBinary());
            }

            if (UpdateMidi != null)
            {
                writer.Write(true);
                writer.Write(UpdateMidi.FullName);
                writer.Write(UpdateMidi.LastWriteTime.ToBinary());
            }
            else
                writer.Write(false);

            FormatCacheData(writer);

            if (conFile != null)
            {
                writer.Write(Milo == null);
                if (Milo != null)
                    WriteFileInfo(Milo, writer);

                writer.Write(Image == null);
                if (Image != null)
                    WriteFileInfo(Image, writer);
            }
            else
            {
                WriteFileInfo(Milo, writer);
                WriteFileInfo(Image, writer);
            }

            writer.Write(AnimTempo);
            writer.Write(SongID);
            writer.Write(VocalPercussionBank);
            writer.Write(VocalSongScrollSpeed);
            writer.Write(SongRating);
            writer.Write(VocalGender);
            writer.Write(VocalTonicNote);
            writer.Write(SongTonality);
            writer.Write(TuningOffsetCents);
            writer.Write(VenueVersion);

            WriteArray(RealGuitarTuning, writer);
            WriteArray(RealBassTuning, writer);
            WriteArray(Pan, writer);
            WriteArray(Volume, writer);
            WriteArray(Core, writer);
            WriteArray(DrumIndices, writer);
            WriteArray(BassIndices, writer);
            WriteArray(GuitarIndices, writer);
            WriteArray(KeysIndices, writer);
            WriteArray(VocalsIndices, writer);
            WriteArray(CrowdIndices, writer);

            return ms.ToArray();
        }

        private static void WriteFileInfo(CONSongFileInfo? info, BinaryWriter writer)
        {
            if (info != null)
                writer.Write(info.FullName);
            else
                writer.Write(string.Empty);
        }

        private static void WriteArray(short[] values, BinaryWriter writer)
        {
            int length = values.Length;
            writer.Write(length);
            for (int i = 0; i < length; ++i)
                writer.Write(values[i]);
        }

        private static void WriteArray(ushort[] values, BinaryWriter writer)
        {
            int length = values.Length;
            writer.Write(length);
            for (int i = 0; i < length; ++i)
                writer.Write(values[i]);
        }

        private static void WriteArray(float[] values, BinaryWriter writer)
        {
            int length = values.Length;
            writer.Write(length);
            for (int i = 0; i < length; ++i)
                writer.Write(values[i]);
        }

        public FrameworkFile? LoadMidiFile()
        {
            if (conFile != null)
            {
                if (midiListing == null)
                    return null;
                return new FrameworkFile_Pointer(conFile.LoadSubFile(midiListing)!, true);
            }

            FileInfo info = new(MidiPath);
            if (!info.Exists || info.LastWriteTime != MidiLastWrite)
                return null;
            return new FrameworkFile_Alloc(MidiPath);
        }

        public FrameworkFile_Alloc? LoadMidiUpdateFile()
        {
            if (UpdateMidi == null)
                return null;

            FileInfo info = new(UpdateMidi.FullName);
            if (!info.Exists || info.LastWriteTime != UpdateMidi.LastWriteTime)
                return null;
            return new(UpdateMidi.FullName);
        }

        public FrameworkFile? LoadMoggFile()
        {
            if (Mogg != null && File.Exists(Mogg.FullName))
                return new FrameworkFile_Alloc(Mogg.FullName);

            if (moggListing != null)
                return new FrameworkFile_Pointer(conFile!.LoadSubFile(moggListing)!, true);

            return null;
        }

        public FrameworkFile? LoadMiloFile()
        {
            if (Milo != null && File.Exists(Milo.FullName))
                return new FrameworkFile_Alloc(Milo.FullName);

            if (miloListing != null)
                return new FrameworkFile_Pointer(conFile!.LoadSubFile(miloListing)!, true);

            return null;
        }

        public FrameworkFile? LoadImgFile()
        {
            if (Image != null && File.Exists(Image.FullName))
                return new FrameworkFile_Alloc(Image.FullName);

            if (imgListing != null)
                return new FrameworkFile_Pointer(conFile!.LoadSubFile(imgListing)!, true);

            return null;
        }

        public bool IsMoggUnencrypted()
        {
            if (Mogg != null && File.Exists(Mogg.FullName))
            {
                using var fs = new FileStream(Mogg.FullName, FileMode.Open, FileAccess.Read);
                return fs.ReadInt32LE() == 0xA;
            }
            else if (conFile != null)
                return conFile.GetMoggVersion(moggListing!) == 0xA;

            throw new Exception("Mogg file not present");
        }

        private TrackScans Scan_Midi(FrameworkFile? file)
        {
            if (file == null)
                throw new Exception("A midi file was changed mid-scan");

            using MidiFileReader reader = new(file);
            TrackScans scans = new();
            while (reader.StartTrack())
            {
                if (reader.GetTrackNumber() > 1 && reader.GetEvent().type == MidiEventType.Text_TrackName)
                {
                    string name = Encoding.ASCII.GetString(reader.ExtractTextOrSysEx());
                    if (MidiFileReader.TRACKNAMES.TryGetValue(name, out MidiTrackType type) && type != MidiTrackType.Events && type != MidiTrackType.Beats)
                        scans.ScanFromMidi(type, DrumType.FOUR_PRO, reader);
                }
            }
            return scans;
        }
    }
}
