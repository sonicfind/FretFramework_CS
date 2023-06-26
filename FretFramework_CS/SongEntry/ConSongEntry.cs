using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Disassemblers;
using Framework.Hashes;
using Framework.Serialization;
using Framework.Serialization.XboxSTFS;
using Framework.SongEntry.CONProUpgrades;
using Framework.SongEntry.TrackScan;
using Framework.Types;
using Iced.Intel;
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
using static System.Net.Mime.MediaTypeNames;

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

        public short[] RealGuitarTuning { get; private set; } = Array.Empty<short>();
        public short[] RealBassTuning { get; private set; } = Array.Empty<short>();

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

        public ushort[] DrumIndices { get; private set; } = Array.Empty<ushort>();
        public ushort[] BassIndices { get; private set; } = Array.Empty<ushort>();
        public ushort[] GuitarIndices { get; private set; } = Array.Empty<ushort>();
        public ushort[] KeysIndices { get; private set; } = Array.Empty<ushort>();
        public ushort[] VocalsIndices { get; private set; } = Array.Empty<ushort>();
        public ushort[] CrowdIndices { get; private set; } = Array.Empty<ushort>();

        public float[] Pan { get; private set; } = Array.Empty<float>();
        public float[] Volume { get; private set; } = Array.Empty<float>();
        public float[] Core { get; private set; } = Array.Empty<float>();

        private ConSongEntry(string name, DTAFileNode dta)
        {
            ShortName = name;
            SetFromDTA(dta);
        }

        public ConSongEntry(string name, CONFile conFile, DTAFileNode dta) : this(name, dta)
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

        public ConSongEntry(string name, string folder, DTAFileNode dta) : this(name, dta)
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

        public void SetFromDTA(DTAFileNode dta)
        {
            if (dta.Name != string.Empty)
                m_name = dta.Name;

            if (dta.Artist != string.Empty)
                m_artist = dta.Artist;

            if (dta.Album != string.Empty)
                m_album = dta.Album;

            if (dta.AlbumTrack != ushort.MaxValue)
                m_album_track = dta.AlbumTrack;

            if (dta.Year_Recorded != ushort.MaxValue)
                m_year = dta.Year_Recorded.ToString();
            else if (dta.Year_Released != ushort.MaxValue)
                m_year = dta.Year_Released.ToString();

            if (dta.Charter != string.Empty)
                m_charter = dta.Charter;

            if (dta.Genre != string.Empty)
                m_genre = dta.Genre;

            if (dta.Source != string.Empty)
            {
                string src = dta.Source;
                if (!ShortName.StartsWith("UGC_") && (src == "ugc" || src == "ugc_plus"))
                    m_source = "customs";
                else
                    m_source = src;
            }

            if (dta.Hopo_Threshold != uint.MaxValue)
                m_hopo_frequency = dta.Hopo_Threshold;

            if (dta.AnimTempo != uint.MaxValue)
                AnimTempo = dta.AnimTempo;

            if (dta.NumVocalParts != ushort.MaxValue)
                VocalParts = dta.NumVocalParts;

            if (dta.SongID != string.Empty)
                SongID = dta.SongID;

            if (dta.Location != string.Empty)
                location = dta.Location;

            if (dta.PreviewStart != uint.MaxValue)
            {
                m_previewStart = dta.PreviewStart;
                m_previewEnd = dta.PreviewEnd;
            }

            if (dta.VocalPercBank != string.Empty)
                VocalPercussionBank = dta.VocalPercBank;

            if (dta.ScrollSpeed != uint.MaxValue)
                VocalSongScrollSpeed = dta.ScrollSpeed;

            if (dta.MidiFile != string.Empty)
                MidiFile = dta.MidiFile;

            if (dta.Ranks != null)
                SetRanks(dta.Ranks);

            IsMaster = IsMaster || dta.IsMaster;

            if (dta.Length != uint.MaxValue)
                m_song_length = dta.Length;

            if (dta.Rating != uint.MaxValue)
                SongRating = dta.Rating;

            if (VocalGender)
                VocalGender = dta.VocalIsMale;

            if (dta.VocalTonic != uint.MaxValue)
                VocalTonicNote = dta.VocalTonic;

            if (!SongTonality)
                SongTonality = dta.Tonality;

            if (dta.TuningOffsetCents != int.MaxValue)
                TuningOffsetCents = dta.TuningOffsetCents;

            if (dta.RealGuitarTuning != Array.Empty<short>())
                RealGuitarTuning = dta.RealGuitarTuning;

            if (dta.RealBassTuning != Array.Empty<short>())
                RealBassTuning = dta.RealBassTuning;

            if (dta.Version != uint.MaxValue)
                VenueVersion = dta.Version;

            ushort[] indices = dta.DrumIndices;
            if (indices != Array.Empty<ushort>())
                DrumIndices = dta.DrumIndices;

            indices = dta.BassIndices;
            if (indices != Array.Empty<ushort>())
                BassIndices = indices;

            indices = dta.GuitarIndices;
            if (indices != Array.Empty<ushort>())
                GuitarIndices = indices;

            indices = dta.KeysIndices;
            if (indices != Array.Empty<ushort>())
                KeysIndices = indices;

            indices = dta.VocalsIndices;
            if (indices != Array.Empty<ushort>())
                VocalsIndices = indices;

            indices = dta.CrowdIndices;
            if (indices != Array.Empty<ushort>())
                CrowdIndices = indices;

            float[] values = dta.Pan;
            if (values != Array.Empty<float>())
                Pan = values;

            values = dta.Volume;
            if (values != Array.Empty<float>())
                Volume = values;

            values = dta.Core;
            if (values != Array.Empty<float>())
                Core = values;
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

        public void Update(string folder, DTAFileNode dta)
        {
            SetFromDTA(dta);
            string dir = Path.Combine(folder, ShortName);
            FileInfo info;
            if (dta.DiscUpdate)
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

            if (HasAlbumArt && dta.AlternatePath)
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

        private void SetRanks(DTARanks ranks)
        {
            SetRank(ref m_scans.lead_5, ranks.guitar5, GuitarDiffMap);
            SetRank(ref m_scans.bass_5, ranks.bass5, BassDiffMap);
            SetRank(ref m_scans.drums_4, ranks.drum4, DrumDiffMap);
            SetRank(ref m_scans.keys, ranks.keys, KeysDiffMap);
            SetRank(ref m_scans.leadVocals, ranks.vocals, VocalsDiffMap);
            if (SetRank(ref m_scans.proguitar_17, ranks.real_guitar, RealGuitarDiffMap))
                m_scans.proguitar_22.intensity = m_scans.proguitar_17.intensity;

            if (SetRank(ref m_scans.probass_17, ranks.real_bass, RealBassDiffMap))
                m_scans.probass_22.intensity = m_scans.probass_17.intensity;

            if (SetRank(ref m_scans.drums_4pro, ranks.drum4_pro, RealDrumsDiffMap))
            {
                if (m_scans.drums_4.intensity == -1)
                    m_scans.drums_4.intensity = m_scans.drums_4pro.intensity;
            }
            else if (m_scans.drums_4.intensity != -1)
                m_scans.drums_4pro.intensity = m_scans.drums_4.intensity;

            SetRank(ref m_scans.proKeys, ranks.real_keys, RealKeysDiffMap);
            if (SetRank(ref m_scans.harmonyVocals, ranks.harmony, HarmonyDiffMap))
            {
                if (m_scans.leadVocals.intensity == -1)
                    m_scans.leadVocals.intensity = m_scans.harmonyVocals.intensity;
            }
            else if (m_scans.leadVocals.intensity != -1)
                m_scans.harmonyVocals.intensity = m_scans.leadVocals.intensity;
        }

        private static bool SetRank(ref ScanValues scan, ushort rank, int[] values)
        {
            if (rank == ushort.MaxValue)
                return false;

            sbyte i = 6;
            while (i > 0 && rank < values[i - 1])
                --i;
            scan.intensity = i;
            return true;
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
