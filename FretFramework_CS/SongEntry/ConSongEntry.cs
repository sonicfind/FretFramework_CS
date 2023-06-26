using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Disassemblers;
using Framework.Hashes;
using Framework.Serialization;
using Framework.Serialization.XboxSTFS;
using Framework.SongEntry.CONProUpgrades;
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

        public CONSongFileInfo(string file)
        {
            _fullname = file;
            _lastWrite = File.GetLastWriteTime(file);
        }

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
        private readonly int midiIndex = -1;
        private int moggIndex = -1;
        private int miloIndex = -1;
        private int imgIndex = -1;

        public string ShortName { get; private set; } = string.Empty;
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

        public string MidiFile { get; private set; } = string.Empty;
        public DateTime MidiFileLastWrite { get; private set; }

        public Encoding MidiEncoding { get; private set; } = Encoding.Latin1;

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

        public override string Directory { get; protected set; } = string.Empty;

        private List<string>? soloes;
        private List<string>? videoVenues;

        public string[] Soloes
        {
            get
            {
                if (soloes == null)
                    return Array.Empty<string>();
                return soloes.ToArray();
            }
        }

        public string[] VideoVenues
        {
            get
            {
                if (videoVenues == null)
                    return Array.Empty<string>();
                return videoVenues.ToArray();
            }
        }

        private List<short>? realGuitarTuning;
        private List<short>? realBassTuning;

        public short[] RealGuitarTuning
        {
            get
            {
                if (realGuitarTuning == null)
                    return Array.Empty<short>();
                return realGuitarTuning.ToArray();
            }
        }

        public short[] RealBassTuning
        {
            get
            {
                if (realBassTuning == null)
                    return Array.Empty<short>();
                return realBassTuning.ToArray();
            }
        }

        private List<ushort>? drumIndices;
        private List<ushort>? bassIndices;
        private List<ushort>? guitarIndices;
        private List<ushort>? keysIndices;
        private List<ushort>? vocalsIndices;
        private List<ushort>? crowdIndices;

        public ushort[] DrumIndices
        {
            get
            {
                if (drumIndices == null)
                    return Array.Empty<ushort>();
                return drumIndices.ToArray();
            }
        }
        public ushort[] BassIndices
        {
            get
            {
                if (bassIndices == null)
                    return Array.Empty<ushort>();
                return bassIndices.ToArray();
            }
        }
        public ushort[] GuitarIndices
        {
            get
            {
                if (guitarIndices == null)
                    return Array.Empty<ushort>();
                return guitarIndices.ToArray();
            }
        }
        public ushort[] KeysIndices
        {
            get
            {
                if (keysIndices == null)
                    return Array.Empty<ushort>();
                return keysIndices.ToArray();
            }
        }
        public ushort[] VocalsIndices
        {
            get
            {
                if (vocalsIndices == null)
                    return Array.Empty<ushort>();
                return vocalsIndices.ToArray();
            }
        }
        public ushort[] CrowdIndices
        {
            get
            {
                if (crowdIndices == null)
                    return Array.Empty<ushort>();
                return crowdIndices.ToArray();
            }
        }

        private List<float>? pan;
        private List<float>? volume;
        private List<float>? core;

        public float[] Pan
        {
            get
            {
                if (pan == null)
                    return Array.Empty<float>();
                return pan.ToArray();
            }
        }
        public float[] Volume
        {
            get
            {
                if (volume == null)
                    return Array.Empty<float>();
                return volume.ToArray();
            }
        }
        public float[] Core
        {
            get
            {
                if (core == null)
                    return Array.Empty<float>();
                return core.ToArray();
            }
        }

        private ConSongEntry(string name, DTAFileReader reader)
        {
            ShortName = name;
            SetFromDTA(reader);
        }

        public ConSongEntry(string name, CONFile conFile, DTAFileReader reader) : this(name, reader)
        {
            this.conFile = conFile;
            if (MidiFile == string.Empty)
                MidiFile = location + ".mid";

            midiIndex = conFile.GetFileIndex(MidiFile);
            if (midiIndex == -1)
                throw new Exception($"Required midi file '{MidiFile}' was not located");
            MidiFileLastWrite = DateTime.FromBinary(conFile[midiIndex].LastWrite);

            moggIndex = conFile.GetFileIndex(location + ".mogg");

            string genPAth = $"songs/{ShortName}/gen/{ShortName}";
            miloIndex = conFile.GetFileIndex(genPAth + ".milo_xbox");
            imgIndex = conFile.GetFileIndex(genPAth + "_keep.png_xbox");
            Directory = conFile.Filename;
        }

        public ConSongEntry(string name, string folder, DTAFileReader reader) : this(name, reader)
        {
            Directory = folder;
            string dir = Path.Combine(folder, location);
            string file = Path.Combine(dir, location);
            MidiFile = file + ".mid";

            FileInfo midiInfo = new(MidiFile);
            if (!midiInfo.Exists)
                throw new Exception($"Required midi file '{MidiFile}' was not located");
            MidiFileLastWrite = midiInfo.LastWriteTime;

            Mogg = new(file + ".mogg");
            file = Path.Combine(dir, $"gen{location}");
            Milo = new(file + ".milo_xbox");
            Image = new(file + "_keep.png_xbox");
            location = dir;
        }

        public (bool, bool) SetFromDTA(DTAFileReader reader)
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
                    case "solo": soloes = reader.ExtractList_String(); break;
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
                                if (!ShortName.StartsWith("UGC_"))
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
                    case "pack_name": /*Packname = reader.ExtractText();*/ break;
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
                                realGuitarTuning = reader.ExtractList_Int16();
                                reader.EndNode();
                            }
                            break;
                        }
                    case "real_bass_tuning":
                        {
                            if (reader.StartNode())
                            {
                                realBassTuning = reader.ExtractList_Int16();
                                reader.EndNode();
                            }
                            break;
                        }
                    case "video_venues":
                        {
                            if (reader.StartNode())
                            {
                                videoVenues = reader.ExtractList_String();
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
                    case "crowd_channels": crowdIndices = reader.ExtractList_UInt16(); break;
                    case "vocal_parts": VocalParts = reader.ReadUInt16(); break;
                    case "pans":
                        if (reader.StartNode())
                        {
                            pan = reader.ExtractList_Float();
                            reader.EndNode();
                        }
                        break;
                    case "vols":
                        if (reader.StartNode())
                        {
                            volume = reader.ExtractList_Float();
                            reader.EndNode();
                        }
                        break;
                    case "cores":
                        if (reader.StartNode())
                        {
                            core = reader.ExtractList_Float();
                            reader.EndNode();
                        }
                        break;
                    case "hopo_threshold": m_hopo_frequency = reader.ReadUInt32(); break;
                    case "midi_file": MidiFile = reader.ExtractText(); break;
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
                                    drumIndices = reader.ExtractList_UInt16();
                                    reader.EndNode();
                                }
                                break;
                            }
                        case "bass":
                            {
                                if (reader.StartNode())
                                {
                                    bassIndices = reader.ExtractList_UInt16();
                                    reader.EndNode();
                                }
                                break;
                            }
                        case "guitar":
                            {
                                if (reader.StartNode())
                                {
                                    guitarIndices = reader.ExtractList_UInt16();
                                    reader.EndNode();
                                }
                                break;
                            }
                        case "keys":
                            {
                                if (reader.StartNode())
                                {
                                    keysIndices = reader.ExtractList_UInt16();
                                    reader.EndNode();
                                }
                                break;
                            }
                        case "vocals":
                            {
                                if (reader.StartNode())
                                {
                                    vocalsIndices = reader.ExtractList_UInt16();
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
                            SetRank(ref m_scans.drums_4, reader.ReadUInt16(), DrumDiffMap);
                            if (m_scans.drums_4pro.intensity == -1)
                                m_scans.drums_4pro.intensity = m_scans.drums_4.intensity;
                            break;
                        }
                    case "guitar": SetRank(ref m_scans.lead_5, reader.ReadUInt16(), GuitarDiffMap); break;
                    case "bass": SetRank(ref m_scans.bass_5, reader.ReadUInt16(), BassDiffMap); break;
                    case "vocals": SetRank(ref m_scans.leadVocals, reader.ReadUInt16(), VocalsDiffMap); break;
                    case "keys": SetRank(ref m_scans.keys, reader.ReadUInt16(), KeysDiffMap); break;
                    case "realGuitar":
                    case "real_guitar":
                        {
                            SetRank(ref m_scans.proguitar_17, reader.ReadUInt16(), RealGuitarDiffMap);
                            m_scans.proguitar_22.intensity = m_scans.proguitar_17.intensity;
                            break;
                        }
                    case "realBass":
                    case "real_bass":
                        {
                            SetRank(ref m_scans.probass_17, reader.ReadUInt16(), RealBassDiffMap);
                            m_scans.probass_22.intensity = m_scans.probass_17.intensity;
                            break;
                        }
                    case "realKeys":
                    case "real_keys": SetRank(ref m_scans.proKeys, reader.ReadUInt16(), RealKeysDiffMap); break;
                    case "realDrums":
                    case "real_drums":
                        {
                            SetRank(ref m_scans.drums_4pro, reader.ReadUInt16(), RealDrumsDiffMap);
                            if (m_scans.drums_4.intensity == -1)
                                m_scans.drums_4.intensity = m_scans.drums_4pro.intensity;
                            break;
                        }
                    case "harmVocals":
                    case "vocal_harm": SetRank(ref m_scans.harmonyVocals, reader.ReadUInt16(), HarmonyDiffMap); break;
                    //case "band": SetRank(ref m_scans.drums_4pro, reader.ReadUInt16(), DrumDiffMap); break;
                }
                reader.EndNode();
            }
        }

        private static void SetRank(ref ScanValues scan, ushort rank, int[] values)
        {
            sbyte i = 6;
            while (i > 0 && rank < values[i - 1])
                --i;
            scan.intensity = i;
        }

        public bool Scan(out byte[] hash)
        {
            hash = Array.Empty<byte>();

            if (!IsMoggUnencrypted())
            {
                Debug.WriteLine($"{ShortName} - Mogg encrypted");
                return false;
            }

            using FrameworkFile midiFile = LoadMidiFile();
            Scan_Midi(midiFile, DrumType.FOUR_PRO);

            PointerHandler hashBuffer = new(midiFile.Length);
            unsafe { Copier.MemCpy(hashBuffer.GetData(), midiFile.ptr, (nuint)midiFile.Length); }

            if (UpdateMidi != null)
                hashBuffer = ScanExtraMidi(LoadMidiUpdateFile(), hashBuffer);

            if (Upgrade != null)
                hashBuffer = ScanExtraMidi(Upgrade.GetUpgradeMidi(), hashBuffer);

            if (!m_scans.CheckForValidScans())
                return false;

            hash = hashBuffer.CalcSHA1();
            hashBuffer.Dispose();
            return true;
        }

        public void Update(string folder, DTAFileReader reader)
        {
            var results = SetFromDTA(reader);

            string dir = Path.Combine(folder, ShortName);
            FileInfo info;
            if (results.Item1)
            {
                string path = Path.Combine(dir, $"{ShortName}_update.mid");
                info = new(path);
                if (info.Exists)
                {
                    if (UpdateMidi == null || UpdateMidi.LastWriteTime < info.LastWriteTime)
                        UpdateMidi = info;
                }
                else if (UpdateMidi == null)
                    Debug.WriteLine($"Couldn't update song {ShortName} - update file {path} not found!");
            }

            info = new(Path.Combine(dir, $"{ShortName}_update.mogg"));
            if (info.Exists && (Mogg == null || Mogg.LastWriteTime < info.LastWriteTime))
            {
                Mogg = info;
                if (conFile != null)
                    moggIndex = -1;
            }

            dir = Path.Combine(dir, "gen");

            info = new(Path.Combine(dir, $"{ShortName}.milo_xbox"));
            if (info.Exists && (Milo == null || Milo.LastWriteTime < info.LastWriteTime))
            {
                Milo = info;
                if (conFile != null)
                    miloIndex = -1;
            }

            if (HasAlbumArt && results.Item2)
            {
                info = new(Path.Combine(dir, $"{ShortName}_keep.png_xbox"));
                if (info.Exists && (Image == null || Image.LastWriteTime < info.LastWriteTime))
                {
                    Image = info;
                    if (conFile != null)
                        imgIndex = -1;
                }
            }
        }

        public override byte[] FormatCacheData()
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(ShortName);
            if (conFile != null)
            {
                writer.Write(midiIndex);
                writer.Write(conFile[midiIndex].LastWrite);
                writer.Write(moggIndex);
                if (moggIndex == -1)
                    writer.Write(Mogg!.FullName);

                writer.Write(miloIndex);
                if (miloIndex == -1)
                    WriteFileInfo(Milo, writer);

                writer.Write(imgIndex);
                if (imgIndex == -1)
                    WriteFileInfo(Image, writer);
            }
            else
            {
                writer.Write(MidiFile);
                writer.Write(Mogg!.FullName);
                WriteFileInfo(Milo, writer);
                WriteFileInfo(Image, writer);
            }

            if (UpdateMidi != null)
            {
                writer.Write(true);
                writer.Write(UpdateMidi.FullName);
                writer.Write(UpdateMidi.LastWriteTime.ToBinary());
            }

            FormatCacheData(writer);

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

        private static void WriteArray(short[] array, BinaryWriter writer)
        {
            int length = array.Length;
            writer.Write(length);
            for (int i = 0; i < length; ++i)
                writer.Write(array[i]);
        }

        private static void WriteArray(ushort[] array, BinaryWriter writer)
        {
            int length = array.Length;
            writer.Write(length);
            for (int i = 0; i < length; ++i)
                writer.Write(array[i]);
        }

        private static void WriteArray(float[] array, BinaryWriter writer)
        {
            int length = array.Length;
            writer.Write(length);
            for (int i = 0; i < length; ++i)
                writer.Write(array[i]);
        }

        public FrameworkFile LoadMidiFile()
        {
            if (conFile != null)
                return new FrameworkFile_Pointer(conFile.LoadSubFile(midiIndex)!, true);
            return new FrameworkFile_Alloc(MidiFile);
        }

        public FrameworkFile_Alloc LoadMidiUpdateFile()
        {
            return new(UpdateMidi!.FullName);
        }

        public FrameworkFile? LoadMoggFile()
        {
            if (Mogg != null)
                return new FrameworkFile_Alloc(Mogg.FullName);

            if (moggIndex != -1)
                return new FrameworkFile_Pointer(conFile!.LoadSubFile(moggIndex)!, true);

            return null;
        }

        public FrameworkFile? LoadMiloFile()
        {
            if (Milo != null)
                return new FrameworkFile_Alloc(Milo.FullName);

            if (miloIndex != -1)
                return new FrameworkFile_Pointer(conFile!.LoadSubFile(miloIndex)!, true);

            return null;
        }

        public FrameworkFile? LoadImgFile()
        {
            if (Image != null)
                return new FrameworkFile_Alloc(Image.FullName);

            if (imgIndex != -1)
                return new FrameworkFile_Pointer(conFile!.LoadSubFile(imgIndex)!, true);

            return null;
        }

        public bool IsMoggUnencrypted()
        {
            if (Mogg != null)
            {
                using var fs = new FileStream(Mogg!.FullName, FileMode.Open, FileAccess.Read);
                return fs.ReadInt32LE() == 0xA;
            }
            else
                return conFile!.GetMoggVersion(moggIndex) == 0xA;
        }

        private bool IsMoggDefined()
        {
            return Mogg != null || conFile != null && moggIndex != -1;
        }

        private PointerHandler ScanExtraMidi(FrameworkFile file, PointerHandler hashBuffer)
        {
            using MidiFileReader reader = new(file, true);
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
            m_scans.Update(ref scans);

            PointerHandler newBuffer = new(hashBuffer.length + file.Length);
            unsafe
            {
                Copier.MemCpy(newBuffer.GetData(), hashBuffer.GetData(), (nuint)hashBuffer.length);
                Copier.MemCpy(newBuffer.GetData() + hashBuffer.length, file.ptr, (nuint)file.Length);
            }
            return newBuffer;
        }
    }
}
