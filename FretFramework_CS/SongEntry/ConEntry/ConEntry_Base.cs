using BenchmarkDotNet.Disassemblers;
using Framework.Hashes;
using Framework.Serialization;
using Framework.Serialization.XboxSTFS;
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

namespace Framework.SongEntry.ConEntry
{
    public abstract class CONEntry_Base : SongEntry
    {
        internal static readonly float[,] emptyRatios = new float[0, 0];
        static CONEntry_Base() { }

        public string ShortName { get; protected set; } = string.Empty;
        public string SongID { get; protected set; } = string.Empty;
        public uint AnimTempo { get; protected set; }
        public string VocalPercussionBank { get; protected set; } = string.Empty;
        public uint VocalSongScrollSpeed { get; protected set; }
        public uint SongRating { get; protected set; } // 1 = FF; 2 = SR; 3 = M; 4 = NR
        public bool VocalGender { get; protected set; } //true for male, false for female
        public bool HasAlbumArt { get; protected set; }
        public bool IsFake { get; protected set; }
        public int VocalTonicNote { get; protected set; }
        public bool SongTonality { get; protected set; } // 0 = major, 1 = minor
        public int TuningOffsetCents { get; protected set; }

        public string MidiFile { get; protected set; } = string.Empty;

        public Encoding MidiEncoding { get; protected set; } = Encoding.Latin1;

        // _update.mid info, if it exists
        public bool DiscUpdate { get; protected set; } = false;
        public string UpdateMidiPath { get; protected set; } = string.Empty;

        public int[] RealGuitarTuning { get; protected set; } = Array.Empty<int>();
        public int[] RealBassTuning { get; protected set; } = Array.Empty<int>();

        // .mogg info
        public bool UsingUpdateMogg { get; protected set; } = false;
        public string MoggPath { get; protected set; } = string.Empty;

        public float[,] MatrixRatios { get; protected set; } = emptyRatios;

        // .milo info
        public bool UsingUpdateMilo { get; protected set; } = false;
        public string MiloPath { get; protected set; } = string.Empty;
        public int VenueVersion { get; protected set; }

        // image info
        public bool AlternatePath { get; protected set; } = false;
        public string ImagePath { get; protected set; } = string.Empty;

        protected string location = string.Empty;

        public override string Directory { get; protected set; } = string.Empty;

        protected CONEntry_Base(DTAFileNode dta)
        {
            SetFromDTA(dta);
        }

        public void SetFromDTA(DTAFileNode dta)
        {
            ShortName = dta.NodeName;

            if (dta.Name != string.Empty)
                m_name = dta.Name;

            if (dta.Artist != string.Empty)
                m_artist = dta.Artist;

            if (dta.Album != string.Empty)
                m_album = dta.Album;

            if (dta.AlbumTrack != ushort.MaxValue)
                m_album_track = dta.AlbumTrack;

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

            if (dta.ranks != null)
                SetRanks(dta.ranks);

            IsMaster = IsMaster || dta.IsMaster;
            AlternatePath = AlternatePath || dta.AlternatePath;

            if (dta.Length != uint.MaxValue)
                m_song_length = dta.Length;

            if (dta.Rating != uint.MaxValue)
                SongRating = dta.Rating;
        }

        public bool Scan(out SHA1Wrapper hash)
        {
            if (!IsMoggUnencrypted())
            {
                Debug.WriteLine($"{ShortName} - Mogg encrypted");
                return false;
            }

            using FrameworkFile midiFile = LoadMidiFile();
            Scan_Midi(midiFile, DrumType.FOUR_PRO);

            bool result = false;
            if (DiscUpdate)
            {
                using FrameworkFile_Alloc updateFile = LoadMidiUpdateFile();
                Scan_Midi(updateFile, DrumType.FOUR_PRO);

                if (m_scans.CheckForValidScans())
                {
                    unsafe
                    {
                        PointerHandler finalFile = new(midiFile.Length + updateFile.Length);
                        Copier.MemCpy(finalFile.GetData(), midiFile.ptr, (nuint)midiFile.Length);
                        Copier.MemCpy(finalFile.GetData() + midiFile.Length, updateFile.ptr, (nuint)updateFile.Length);
                        hash = new(SHA1.HashData(finalFile.AsReadOnlySpan()));
                    }
                    result = true;
                }
            }
            else if (m_scans.CheckForValidScans())
            {
                hash = new(midiFile.CalcSHA1());
                result = true;
            }

            return result;
        }

        public abstract FrameworkFile LoadMidiFile();

        public FrameworkFile_Alloc LoadMidiUpdateFile()
        {
            return new(UpdateMidiPath);
        }

        public virtual FrameworkFile LoadMoggFile()
        {
            return new FrameworkFile_Alloc(MoggPath);
        }

        public virtual FrameworkFile? LoadMiloFile()
        {
            if (MiloPath.Length == 0)
                return null;
            return new FrameworkFile_Alloc(MiloPath);
        }

        public virtual FrameworkFile? LoadImgFile()
        {
            if (!HasAlbumArt || ImagePath.Length == 0)
                return null;
            return new FrameworkFile_Alloc(ImagePath);
        }

        public virtual bool IsMoggUnencrypted()
        {
            var fs = new FileStream(MoggPath, FileMode.Open, FileAccess.Read);
            return fs.ReadInt32LE() == 0xA;
        }

        private static readonly int[] BandDiffMap =       { 163, 215, 243, 267, 292, 345 };
        private static readonly int[] GuitarDiffMap =     { 139, 176, 221, 267, 333, 409 };
        private static readonly int[] BassDiffMap =       { 135, 181, 228, 293, 364, 436 };
        private static readonly int[] DrumDiffMap =       { 124, 151, 178, 242, 345, 448 };
        private static readonly int[] KeysDiffMap =       { 153, 211, 269, 327, 385, 443 };
        private static readonly int[] VocalsDiffMap =     { 132, 175, 218, 279, 353, 427 };
        private static readonly int[] RealGuitarDiffMap = { 150, 205, 264, 323, 382, 442 };
        private static readonly int[] RealBassDiffMap =   { 150, 208, 267, 325, 384, 442 };
        private static readonly int[] RealDrumsDiffMap =  { 124, 151, 178, 242, 345, 448 };
        private static readonly int[] RealKeysDiffMap =   { 153, 211, 269, 327, 385, 443 };
        private static readonly int[] HarmonyDiffMap =    { 132, 175, 218, 279, 353, 427 };

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
    }
}
